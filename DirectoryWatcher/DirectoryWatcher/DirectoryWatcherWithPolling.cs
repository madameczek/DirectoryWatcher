using System.Collections.Concurrent;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;

namespace DirectoryWatcher.DirectoryWatcher;

public class DirectoryWatcherWithPolling : IDirectoryWatcher, IDisposable
{
    private const int EventBufferSize = 100000;
    private const int DirectoryPollingInterval = 300;
    
    private readonly string _directoryToWatch;
    private static ulong _changeLevel;
    private Action<string>? _callback;
    private IEnumerable<IFileInfo>? _files;
    private BlockingCollection<IFileInfo>? _fileSystemEventBuffer;
    private PhysicalFileProvider? _fileWatcher;
    private IChangeToken? _changeToken;
    private IDisposable? _fileWatcherCallback;
    private PeriodicTimer? _timer;

    public DirectoryWatcherWithPolling(string directoryToWatch)
    {
        if (string.IsNullOrEmpty(directoryToWatch) || !Directory.Exists(directoryToWatch))
            throw new ArgumentException("Directory can not be empty string");
        
        _directoryToWatch = directoryToWatch;
    }

    public void RegisterCallback(Action<string>? callback) => _callback = callback;
    
    public async Task StartWatching(Action<string>? callback, CancellationToken ct = default)
    {
        RegisterCallback(callback);
        await StartWatching(ct);
    }

    public async Task StartWatching(CancellationToken ct = default)
    {
        try
        {
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(DirectoryPollingInterval));
            _fileSystemEventBuffer = new BlockingCollection<IFileInfo>(EventBufferSize);

            CreateFileWatcher();
            _fileWatcherCallback = WatchForFileChanges();

            var processBuffer = Task.Run(() =>
            {
                foreach (var fileInfo in _fileSystemEventBuffer.GetConsumingEnumerable(ct))
                {
                    _callback?.Invoke(fileInfo.PhysicalPath!);
                    LogWhenBufferIsEmpty();
                }
            }, ct);

            // TODO Implement resilience policy
            _files = GetFiles();
            BufferFiles();

            while (await _timer.WaitForNextTickAsync(ct))
            {
                if (Interlocked.Read(ref _changeLevel) != 0) continue;

                _fileWatcher?.Dispose();
                Console.WriteLine("Directory polling upon timer");

                BufferNewFiles();
                CreateFileWatcher();
                _fileWatcherCallback = WatchForFileChanges();
            }

            await processBuffer.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        finally
        {
            Dispose();
        }
    }

    private void LogWhenBufferIsEmpty()
    {
        if (_fileSystemEventBuffer?.Count == 0)
            Console.WriteLine("Synchronisation buffer is empty");
    }

    private List<IFileInfo> GetFiles() =>
        _fileWatcher!.GetDirectoryContents(string.Empty).ToList();

    private void CreateFileWatcher()
    {
        // TODO Implement resilience policy
        _fileWatcher = new PhysicalFileProvider(_directoryToWatch, ExclusionFilters.Sensitive)
        {
            UsePollingFileWatcher = true,
            UseActivePolling = true
        };
    }

    private IDisposable WatchForFileChanges()
    {
        _changeToken = _fileWatcher!.Watch("**/*.*");
        return _changeToken.RegisterChangeCallback(_ => NotifyFileChange(), default);
    }

    private void NotifyFileChange()
    {
        Console.WriteLine("Directory has changed. Callback invoked");

        _fileWatcherCallback = WatchForFileChanges();

        if (0 == Interlocked.CompareExchange(ref _changeLevel, 1, 0))
            BufferNewFiles();
        else
            Interlocked.Exchange(ref _changeLevel, 2);
    }
    
    private void BufferFiles()
    {
        foreach (var fileInfo in _files!)
        {
            if (!_fileSystemEventBuffer!.TryAdd(fileInfo))
                Console.WriteLine($"Buffer size exceeded ({EventBufferSize}) or buffer is disposed");
        }
    }
    
    private void BufferNewFiles()
    {
        do
        {
            Interlocked.CompareExchange(ref _changeLevel, 1, 2);
            
            // TODO Implement resilience policy
            var filesActual = GetFiles();
            var newFiles = filesActual.ExceptBy(_files!.Select(f => f.Name), fi => fi.Name);
            _files = filesActual;

            foreach (var fileInfo in newFiles)
            {
                if (!_fileSystemEventBuffer?.TryAdd(fileInfo) ?? false)
                    Console.WriteLine($"Buffer size exceeded ({EventBufferSize}) or buffer is disposed");
            }

        } while (1 < Interlocked.Read(ref _changeLevel));

        Interlocked.Exchange(ref _changeLevel, 0);
    }

    private bool _isDisposed;
    public void Dispose() => Dispose(true);
    private void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            _fileWatcherCallback?.Dispose();
            _fileSystemEventBuffer?.Dispose();
            _timer?.Dispose();
        }
        _isDisposed = true;
    }
}