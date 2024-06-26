# Directory Watcher

While in Docker container, .NET app doesn't receive inotify notifications. If your app relies on *FileSystemWatcher*, which relies on inotify, your app will not be notified about changes in filesysem of mounted volumes.

*DirectoryWatcherWithPolling* class shows how to go over this problem. It uses *PhysicalFileProvider* class. It gives reliable solution for a problem of watching a folder for new files by containerized application.
It is thread-safe and non blocking solution.  

Code is simplified for demo purposes. In production, you may want to make the *DirectoryWatcherWithPolling* DI friendly, change callback to async or implement better way to distinguish a new file from an old one. 

This is explained on [my blog](https://blog.adameczek.pl/index.php/2024/06/25/chyba-cos-wpadlo-nowego-czyli-nasluchiwanie-na-nowe-pliki-przez-kontener/). In Polish, intentionally :)