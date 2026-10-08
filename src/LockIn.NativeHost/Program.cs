using System.Text.Json;
using System.Text.RegularExpressions;
using LockIn.Core;

// Stdout is exclusively the browser's framed protocol. Never print credentials or page data.
try
{
    var path = Environment.GetEnvironmentVariable("LOCKIN_NATIVE_CONFIG") ?? Path.Combine(AppContext.BaseDirectory, "native-host.config.json");
    var config = JsonSerializer.Deserialize<NativeConfiguration>(await File.ReadAllTextAsync(path), JsonOptions.Default) ?? throw new InvalidDataException();
    var origin = args.FirstOrDefault()?.TrimEnd('/') + "/";
    if (config.AllowedOrigins is null || !Regex.IsMatch(origin, "\\Achrome-extension://[a-p]{32}/\\z") || !config.AllowedOrigins.Contains(origin, StringComparer.Ordinal))
        throw new InvalidDataException("Native host origin is not enrolled.");
    using var input = Console.OpenStandardInput();
    using var output = Console.OpenStandardOutput();
    while (true)
    {
        Request request;
        try { request = await PipeTransport.ReadAsync<Request>(input, maxFrameBytes: 65536); }
        catch (EndOfStreamException) { break; }
        Response response;
        if (request.ProtocolVersion != 1 || request.RequestId is not { Length: > 0 and <= 100 } || request.Payload.ValueKind != JsonValueKind.Object ||
            request.Type is not ("GetBridgeStatus" or "PairBrowser" or "PollBrowser" or "AcknowledgeBrowserPolicy"))
            response = new(1, request.RequestId ?? "", false, null, "Browser command is not permitted.");
        else
        {
            try
            {
                var result = await ServiceClient.SendAsync(request.Type == "GetBridgeStatus" ? "GetCapabilities" : request.Type,
                    request.Type == "GetBridgeStatus" ? new { } : (object)request.Payload, pipeName: config.PipeName);
                response = result with { RequestId = request.RequestId };
            }
            catch (Exception) { response = new(1, request.RequestId, false, null, "The LockIn host is unavailable. Reopen the desktop host."); }
        }
        await PipeTransport.WriteAsync(output, response);
    }
}
catch (Exception) { Console.Error.WriteLine("LockIn native host stopped: invalid configuration, origin, or message framing."); Environment.ExitCode = 1; }

public sealed record NativeConfiguration(string[] AllowedOrigins, string? PipeName = null);
