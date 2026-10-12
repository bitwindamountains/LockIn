using System.IO.Pipes;
using LockIn.Core;
using LockIn.Infrastructure;

var dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LockIn", "simulation.db");
var pipeName = PipeTransport.PipeName;
for (var i = 0; i < args.Length; i += 2)
{
    if (i + 1 >= args.Length || args[i] is not ("--data-path" or "--pipe-name"))
        throw new ArgumentException("Usage: LockIn.Service [--data-path path] [--pipe-name name]");
    if (args[i] == "--data-path") dataPath = Path.GetFullPath(args[i + 1]);
    else pipeName = args[i + 1];
}
if (pipeName.Length > 160 || pipeName.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_')))
    throw new ArgumentException("Invalid pipe name.");
using var instance = new Mutex(false, "Local\\" + pipeName, out var created);
if (!created) { Console.Error.WriteLine("A local session host already owns this endpoint."); return; }
SimulationEngine engine;
try
{
    engine = new SimulationEngine(new LocalStore(dataPath), new SystemClock(), new NoOpSimulationProvider());
    await engine.ReconcileAsync();
}
catch (Exception error)
{
    Console.Error.WriteLine($"Session store could not open: {error.Message}\nNo new policy was applied. Existing browser caches may remain until expiry. Preserve the database; see README recovery instructions.");
    Environment.ExitCode = 1;
    return;
}
Console.WriteLine($"LockIn local session host. Data: {dataPath}");
Console.WriteLine($"Endpoint: {pipeName}. Simulation is the default. Explicitly enabled browser sessions can restrict selected sites. Desktop app enforcement is unavailable.");
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var reconciliation = Task.Run(async () =>
{
    while (!cancellation.IsCancellationRequested)
    {
        try { await engine.ReconcileAsync(); await Task.Delay(1000, cancellation.Token); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { break; }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Reconciliation error: {error.Message}");
            try { await Task.Delay(5000, cancellation.Token); } catch (OperationCanceledException) { break; }
        }
    }
});
while (!cancellation.IsCancellationRequested)
{
    NamedPipeServerStream pipe;
    try
    {
        pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
    {
        // Keep reconciling sessions while the endpoint is unavailable; retry instead of exiting.
        Console.Error.WriteLine($"Pipe endpoint unavailable: {error.Message}");
        try { await Task.Delay(1000, cancellation.Token); } catch (OperationCanceledException) { break; }
        continue;
    }
    await using var ownedPipe = pipe;
    try
    {
        await pipe.WaitForConnectionAsync(cancellation.Token);
        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        requestTimeout.CancelAfter(TimeSpan.FromSeconds(4));
        var request = await PipeTransport.ReadAsync<Request>(pipe, requestTimeout.Token);
        var response = await engine.HandleAsync(request);
        await PipeTransport.WriteAsync(pipe, response, requestTimeout.Token);
    }
    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { break; }
    catch (OperationCanceledException) { Console.Error.WriteLine("Incomplete pipe request timed out."); }
    catch (Exception error) { Console.Error.WriteLine($"Pipe request error: {error.Message}"); }
}
cancellation.Cancel();
await reconciliation;
