using DirectoryWatcher.DirectoryWatcher;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    cts.Cancel();
    eventArgs.Cancel = true;
};

using var directoryWatcher = new DirectoryWatcherWithPolling(@"H:\Downloads");
directoryWatcher.RegisterCallback(Console.WriteLine);
await directoryWatcher.StartWatching(cts.Token);