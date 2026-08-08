using System.IO;
using System.IO.Pipes;

namespace UI.Platform;

/// <summary>
/// Enforces Single Instance (INV-020): the first editor process acquires a named mutex and listens
/// on a named pipe for forwarded Startup Document paths; a later launch fails to acquire, forwards
/// its Startup Document to the holder, and exits. UI-agnostic, so the composition root decides how
/// a forwarded path reaches the Workspace.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>
    /// Tries to make this process the Single Instance for <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The application-wide instance name.</param>
    /// <returns>
    /// The guard when this process is the first holder, or <see langword="null"/> when another
    /// process already holds the instance.
    /// </returns>
    public static SingleInstanceGuard? TryAcquire(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var mutex = new Mutex(initiallyOwned: true, @"Local\" + name, out var createdNew);
        if (createdNew)
        {
            return new SingleInstanceGuard(mutex, name);
        }

        mutex.Dispose();
        return null;
    }

    /// <summary>
    /// Forwards a Startup Document path to the process holding <paramref name="name"/>. An empty
    /// path is a pure "activate yourself" signal (the later launch had no Startup Document).
    /// </summary>
    /// <param name="name">The application-wide instance name.</param>
    /// <param name="documentPath">The Startup Document path to forward, or an empty string.</param>
    /// <param name="timeoutMilliseconds">How long to wait for the holder's pipe.</param>
    /// <returns><see langword="true"/> when the holder received the path.</returns>
    public static bool ForwardDocumentPath(string name, string documentPath, int timeoutMilliseconds = 2000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(documentPath);

        try
        {
            using var client = new NamedPipeClientStream(".", PipeNameFor(name), PipeDirection.Out);
            client.Connect(timeoutMilliseconds);
            using var writer = new StreamWriter(client);
            writer.Write(documentPath);
            writer.Flush();
            return true;
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Starts listening for Startup Document paths forwarded by later launches. Idempotent — only
    /// the first call starts the listener.
    /// </summary>
    /// <param name="onDocumentPathReceived">
    /// Called on a background thread with each forwarded path (possibly empty, meaning "activate
    /// yourself"). The caller marshals onto its own thread as needed.
    /// </param>
    public void Listen(Action<string> onDocumentPathReceived)
    {
        ArgumentNullException.ThrowIfNull(onDocumentPathReceived);

        if (_listener is not null)
        {
            return;
        }

        NamedPipeServerStream server;
        try
        {
            // The pipe is opened here rather than on the listener's own thread: Listen must not return
            // before a later launch could connect, or a launch that forwards while this thread is still
            // being scheduled loses its Startup Document (INV-020).
            server = OpenPipe();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The pipe name is taken or barred; this process simply does not listen, exactly as a
            // failure inside the loop would have left it.
            return;
        }

        _listener = Task.Run(() => ListenLoopAsync(server, onDocumentPathReceived, _cancellation.Token));
    }

    /// <summary>Releases the instance so the next launch can acquire it, and stops listening.</summary>
    public void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Disposal on a thread other than the acquirer's cannot release; closing the handle
            // below still frees the name.
        }

        _mutex.Dispose();
        _cancellation.Dispose();
    }

    private SingleInstanceGuard(Mutex mutex, string name)
    {
        _mutex = mutex;
        _name = name;
    }

    // Serves one later-launch client at a time: accept, read its single path message, hand it to the
    // callback, and reopen the pipe for the next launch until disposed. The first pipe is opened by
    // Listen, so the holder is already reachable by the time this loop starts.
    private async Task ListenLoopAsync(
        NamedPipeServerStream opened, Action<string> onDocumentPathReceived, CancellationToken token)
    {
        var server = opened;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using (server.ConfigureAwait(false))
                {
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    using var reader = new StreamReader(server);
                    var path = await reader.ReadToEndAsync(token).ConfigureAwait(false);
                    onDocumentPathReceived(path);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // The client vanished mid-handshake; keep serving the next launch.
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            try
            {
                server = OpenPipe();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    private NamedPipeServerStream OpenPipe() => new(
        PipeNameFor(_name),
        PipeDirection.In,
        maxNumberOfServerInstances: 1,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous);

    // The pipe namespace is machine-wide where the mutex's Local\ namespace is per-session; scoping
    // the pipe by user keeps two users' editors from crossing paths.
    private static string PipeNameFor(string name) => name + "." + Environment.UserName;

    private readonly Mutex _mutex;
    private readonly string _name;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listener;
}
