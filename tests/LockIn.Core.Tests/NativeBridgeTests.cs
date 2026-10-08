using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using LockIn.Core;

namespace LockIn.Core.Tests;

public class NativeBridgeTests
{
    [WindowsFact]
    [Trait("Category", "WindowsIntegration")]
    public async Task StdioFramesRejectDesktopCommandsAndUnlistedOrigins()
    {
        var folder = Path.Combine(Path.GetTempPath(), "lockin-native-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var origin = "chrome-extension://" + new string('a', 32) + "/";
        var config = Path.Combine(folder, "config.json");
        await File.WriteAllTextAsync(config, JsonSerializer.Serialize(new { allowedOrigins = new[] { origin } }));
        Process Start(string requestedOrigin)
        {
            var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
            var info = new ProcessStartInfo(dotnet) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add(typeof(NativeConfiguration).Assembly.Location); info.ArgumentList.Add(requestedOrigin);
            info.Environment["LOCKIN_NATIVE_CONFIG"] = config;
            return Process.Start(info)!;
        }
        try
        {
            using (var process = Start(origin))
            {
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    foreach (var command in new[] { "RecoverSession", "ConfirmBrowserRemoval" })
                    {
                    var request = new Request(1, Guid.NewGuid().ToString("N"), command, JsonSerializer.SerializeToElement(new { }));
                    await PipeTransport.WriteAsync(process.StandardInput.BaseStream, request, timeout.Token);
                    var response = await PipeTransport.ReadAsync<Response>(process.StandardOutput.BaseStream, timeout.Token);
                    Assert.False(response.Ok); Assert.Equal(request.RequestId, response.RequestId);
                    Assert.Contains("not permitted", response.Error);
                    }
                    process.StandardInput.Close(); await process.WaitForExitAsync(timeout.Token); Assert.Equal(0, process.ExitCode);
                }
                finally { if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(); } }
            }
            using (var process = Start("chrome-extension://" + new string('b', 32) + "/"))
            {
                try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.Equal(1, process.ExitCode); Assert.Equal("", await process.StandardOutput.ReadToEndAsync()); }
                finally { if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(); } }
            }
        }
        finally { Directory.Delete(folder, true); }
    }
}
