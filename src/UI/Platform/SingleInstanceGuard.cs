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
        _listener ??= Task.Run(() => ListenLoopAsync(onDocumentPathReceived, _cancellation.Token));
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
    // callback, and go back to listening until disposed.
    private async Task ListenLoopAsync(Action<string> onDocumentPathReceived, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeNameFor(_name),
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                using var reader = new StreamReader(server);
                var path = await reader.ReadToEndAsync(token).ConfigureAwait(false);
                onDocumentPathReceived(path);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // The client vanished mid-handshake; keep serving the next launch.
            }
        }
    }

    // The pipe namespace is machine-wide where the mutex's Local\ namespace is per-session; scoping
    // the pipe by user keeps two users' editors from crossing paths.
    private static string PipeNameFor(string name) => name + "." + Environment.UserName;

    private readonly Mutex _mutex;
    private readonly string _name;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listener;
}
