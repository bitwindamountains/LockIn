using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using LockIn.Core;

namespace LockIn.Core.Tests;

public class NativeLifecycleTests
{
    [WindowsFact]
    [Trait("Category", "WindowsIntegration")]
    public async Task RealBridgePairsActivatesSurvivesHostRestartAndAcknowledgesRecovery()
    {
        var folder = Path.Combine(Path.GetTempPath(), "lockin-native-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var pipe = "LockIn.Test." + Guid.NewGuid().ToString("N");
        var origin = "chrome-extension://" + new string('a', 32) + "/";
        var config = Path.Combine(folder, "native.json");
        await File.WriteAllTextAsync(config, JsonSerializer.Serialize(new NativeConfiguration([origin], pipe), JsonOptions.Default));
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
        Process Start(string assembly, params string[] args)
        {
            var info = new ProcessStartInfo(dotnet) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add(assembly);
            foreach (var arg in args) info.ArgumentList.Add(arg);
            info.Environment["LOCKIN_NATIVE_CONFIG"] = config;
            return Process.Start(info)!;
        }
        async Task<Process> StartHost()
        {
            var process = Start(typeof(SimulationEngine).Assembly.Location, "--data-path", Path.Combine(folder, "state.db"), "--pipe-name", pipe);
            try
            {
                var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
                Assert.StartsWith("LockIn local session host", line);
                return process;
            }
            catch { await Stop(process); throw; }
        }
        async Task<T> Desktop<T>(string type, object payload)
        {
            var response = await ServiceClient.SendAsync(type, payload, pipeName: pipe);
            Assert.True(response.Ok, response.Error);
            return Assert.IsType<JsonElement>(response.Result).Deserialize<T>(JsonOptions.Default)!;
        }
        Process? host = null, bridge = null;
        try
        {
            host = await StartHost();
            bridge = Start(typeof(NativeConfiguration).Assembly.Location, origin);
            async Task<Response> Native(string type, object payload)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var request = new Request(1, Guid.NewGuid().ToString("N"), type, JsonSerializer.SerializeToElement(payload, JsonOptions.Default));
                await PipeTransport.WriteAsync(bridge.StandardInput.BaseStream, request, timeout.Token);
                var response = await PipeTransport.ReadAsync<Response>(bridge.StandardOutput.BaseStream, timeout.Token);
                Assert.Equal(request.RequestId, response.RequestId);
                Assert.Equal(1, response.ProtocolVersion);
                return response;
            }
            async Task<T> Browser<T>(string type, object payload)
            {
                var response = await Native(type, payload);
                Assert.True(response.Ok, response.Error);
                return Assert.IsType<JsonElement>(response.Result).Deserialize<T>(JsonOptions.Default)!;
            }
            var pairing = await Desktop<JsonElement>("BeginBrowserPairing", new { });
            var paired = await Browser<PairBrowserResult>("PairBrowser", new PairBrowserPayload(pairing.GetProperty("code").GetString()!, Guid.NewGuid().ToString("N"), "Process fixture"));
            var auth = new BrowserAuth(paired.InstallationId, paired.Credential);
            var profile = await Desktop<Profile>("SaveProfileDraft", new SaveProfilePayload(new("study", 1, "Native lifecycle", 60, CommitmentMode.Guided, [], [],
                new([new("tag", "visibleHashtags", "education")], [], DestinationKind: "focusPage", MatchingMode: "allowMatchingTags"), []), 0));
            await Browser<BrowserPollResult>("PollBrowser", new BrowserPollPayload(auth, ProfileRules.Hash(profile)));
            var preparation = await Desktop<Preparation>("PrepareSession", new PreparePayload(profile.Id, profile.Revision, auth.InstallationId));
            await Task.Delay(TimeSpan.FromSeconds(10.2));
            var session = await Desktop<Session>("CommitSession", new CommitPayload(preparation.CandidateId));
            Assert.Equal(SessionState.Activating, session.State);
            var policy = (await Browser<BrowserPollResult>("PollBrowser", new BrowserPollPayload(auth, null))).Policy!;
            Assert.Equal(session.PolicyHash, policy.PolicyHash);
            Assert.False((await Native("AcknowledgeBrowserPolicy", new BrowserAckPayload(auth, session.Id, new string('0', 64)))).Ok);
            await Browser<JsonElement>("AcknowledgeBrowserPolicy", new BrowserAckPayload(auth, session.Id, session.PolicyHash));
            Assert.Equal(SessionState.Active, (await Desktop<Session>("GetSessionStatus", new { })).State);
            await Stop(host); host = null;
            host = await StartHost();
            var resumed = await Desktop<Session>("GetSessionStatus", new { });
            Assert.Equal(session.Id, resumed.Id);
            Assert.Equal(session.EndsAtUtc, resumed.EndsAtUtc);
            Assert.NotNull((await Browser<BrowserPollResult>("PollBrowser", new BrowserPollPayload(auth, null))).Policy);
            Assert.False((await Native("RecoverSession", new { })).Ok);
            // A live browser session recovers through the Guided delay; the policy stays until release.
            Assert.NotNull((await Desktop<Session>("RecoverSession", new { })).EarlyEndRequestedAtUtc);
            Assert.NotNull((await Browser<BrowserPollResult>("PollBrowser", new BrowserPollPayload(auth, null))).Policy);
            // Like the extension, tolerate a transient unavailable host while waiting for release.
            BrowserPollResult? release = null;
            for (var waitUntil = DateTime.UtcNow.AddSeconds(75); release is not { Policy: null } && DateTime.UtcNow < waitUntil; await Task.Delay(1000))
            {
                var poll = await Native("PollBrowser", new BrowserPollPayload(auth, null));
                if (poll.Ok) release = Assert.IsType<JsonElement>(poll.Result).Deserialize<BrowserPollResult>(JsonOptions.Default);
            }
            Assert.NotNull(release);
            Assert.Null(release.Policy);
            Assert.Equal(session.Id, release.ReleaseSessionId);
            Assert.Equal(session.PolicyHash, release.ReleasePolicyHash);
            await Browser<JsonElement>("AcknowledgeBrowserPolicy", new BrowserAckPayload(auth, session.Id, session.PolicyHash, true));
            var history = await Desktop<SessionHistoryItem[]>("ListSessions", new { });
            Assert.Equal(SessionState.Completed, Assert.Single(history).State);
            Assert.Equal("browserAcknowledged", history[0].CleanupEvidence);
            bridge.StandardInput.Close();
            await bridge.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, bridge.ExitCode);
        }
        finally
        {
            if (bridge is not null) await Stop(bridge);
            if (host is not null) await Stop(host);
            // Only this test's unique directory; never touches installed registrations.
            Directory.Delete(folder, true);
        }
    }

    private static async Task Stop(Process process)
    {
        if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(); }
        process.Dispose();
    }
}
