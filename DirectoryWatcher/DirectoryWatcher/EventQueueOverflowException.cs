namespace DirectoryWatcher.DirectoryWatcher;

public class EventQueueOverflowException : Exception
{
    public EventQueueOverflowException()
        : base() { }

    public EventQueueOverflowException(string message)
        : base(message) { }
}