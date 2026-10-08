using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using LockIn.Core;
using LockIn.Infrastructure;

namespace LockIn.Core.Tests;

public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute() { if (!OperatingSystem.IsWindows()) Skip = "Requires Windows named-pipe and host process verification."; }
}

public class HostProcessTests
{
    [WindowsFact]
    [Trait("Category", "WindowsIntegration")]
    public async Task StalledClientTimesOutAndHostRestartKeepsDeadline()
    {
        var folder = Path.Combine(Path.GetTempPath(), "lockin-host-" + Guid.NewGuid().ToString("N"));
        var database = Path.Combine(folder, "simulation.db");
        var pipeName = "LockIn.Test." + Guid.NewGuid().ToString("N");
        var profile = new Profile("study", 1, "Restart test", 60, CommitmentMode.Guided, [], [], new([], []), []);
        var saved = SessionLogic.Start(profile, DateTimeOffset.UtcNow, "persisted-session");
        new LocalStore(database).SaveSession(saved);
        Process? process = null;
        try
        {
            process = await StartAsync(database, pipeName);
            using (var stalled = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
            {
                await stalled.ConnectAsync(2000);
                await stalled.WriteAsync(new byte[] { 20 }); // Incomplete header must not hold the only endpoint forever.
                await Task.Delay(4500);
                var capabilities = await ServiceClient.SendAsync("GetCapabilities", new { }, pipeName: pipeName);
                Assert.True(capabilities.Ok);
            }
            var before = await StatusAsync(pipeName);
            Assert.Equal(saved.EndsAtUtc, before.EndsAtUtc);
            process.Kill(); await process.WaitForExitAsync(); process.Dispose();
            process = await StartAsync(database, pipeName);
            var after = await StatusAsync(pipeName);
            Assert.Equal(before.Id, after.Id);
            Assert.Equal(before.EndsAtUtc, after.EndsAtUtc);
            Assert.True(after.EffectiveElapsedSeconds >= before.EffectiveElapsedSeconds);
        }
        finally
        {
            if (process is not null) { if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(); } process.Dispose(); }
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private static async Task<Session> StatusAsync(string pipeName)
    {
        var response = await ServiceClient.SendAsync("GetSessionStatus", new { }, pipeName: pipeName);
        Assert.True(response.Ok);
        return Assert.IsType<JsonElement>(response.Result).Deserialize<Session>(JsonOptions.Default)!;
    }

    private static async Task<Process> StartAsync(string database, string pipeName)
    {
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
        var info = new ProcessStartInfo(dotnet) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(typeof(SimulationEngine).Assembly.Location);
        info.ArgumentList.Add("--data-path"); info.ArgumentList.Add(database);
        info.ArgumentList.Add("--pipe-name"); info.ArgumentList.Add(pipeName);
        var process = Process.Start(info)!;
        try
        {
            var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
            if (line is null || !line.StartsWith("LockIn local session host", StringComparison.Ordinal)) throw new InvalidOperationException(await process.StandardError.ReadToEndAsync());
            return process;
        }
        catch { if (!process.HasExited) process.Kill(); process.Dispose(); throw; }
    }
}
