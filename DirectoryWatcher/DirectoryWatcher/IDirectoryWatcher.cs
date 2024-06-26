namespace DirectoryWatcher.DirectoryWatcher;

public interface IDirectoryWatcher
{
    void RegisterCallback(Action<string> callback);
    Task StartWatching(CancellationToken cancellationToken);
    Task StartWatching(Action<string> callback, CancellationToken cancellationToken);
    void Dispose();
}