using System.IO.Pipes;
using System.Text.Json;

namespace LockIn.Core.Tests;

public class ServiceClientTests
{
    // Stands in for a busy UI thread: posted continuations never run.
    private sealed class StalledContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) { }
    }

    [WindowsFact]
    public async Task RequestCompletesWhileTheCallersContextIsBusy()
    {
        var pipeName = "LockIn.Test." + Guid.NewGuid().ToString("N");
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var serve = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            var request = await PipeTransport.ReadAsync<Request>(server);
            await PipeTransport.WriteAsync(server, new Response(1, request.RequestId, true, null));
        });
        var previous = SynchronizationContext.Current;
        Task<Response> send;
        SynchronizationContext.SetSynchronizationContext(new StalledContext());
        try { send = ServiceClient.SendAsync("GetCapabilities", new { }, pipeName: pipeName); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        // The host serves one connection at a time; client I/O must not wait for the caller's thread.
        Assert.Same(send, await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(5))));
        Assert.True((await send).Ok);
        await serve;
    }
}
