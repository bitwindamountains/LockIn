using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

namespace LockIn.Core;

public static class PipeTransport
{
    public static string PipeName => "LockIn.Simulation.v1." + (OperatingSystem.IsWindows()
        ? System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value
        : Environment.UserName);
    public const int MaxFrameBytes = 1_048_576;

    public static async Task WriteAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions.Default);
        if (bytes.Length > MaxFrameBytes) throw new InvalidDataException("Frame too large.");
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellationToken = default, int maxFrameBytes = MaxFrameBytes)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length < 1 || length > maxFrameBytes || length > MaxFrameBytes) throw new InvalidDataException("Invalid frame length.");
        var bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(bytes, JsonOptions.Default) ?? throw new InvalidDataException("Invalid JSON frame.");
    }
}

public static class ServiceClient
{
    public static async Task<Response> SendAsync(string type, object payload, CancellationToken cancellationToken = default, string? pipeName = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        // The host serves one connection at a time, so never resume on the caller's (possibly busy UI) thread.
        var pipe = new NamedPipeClientStream(".", pipeName ?? PipeTransport.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await using var disposePipe = pipe.ConfigureAwait(false);
        await pipe.ConnectAsync(2000, timeout.Token).ConfigureAwait(false);
        var request = new Request(1, Guid.NewGuid().ToString("N"), type, JsonSerializer.SerializeToElement(payload, JsonOptions.Default));
        await PipeTransport.WriteAsync(pipe, request, timeout.Token).ConfigureAwait(false);
        var response = await PipeTransport.ReadAsync<Response>(pipe, timeout.Token).ConfigureAwait(false);
        if (response.ProtocolVersion != 1 || response.RequestId != request.RequestId) throw new InvalidDataException("The host returned an unrelated protocol response.");
        return response;
    }
}
