using System.Net.Sockets;
using System.Text;
using BloxCode.Core;

namespace BloxCode.Linux.Platform;

/// <summary>
/// A lock held by keeping a file open exclusively (flock on Linux), released when the process ends,
/// even on a crash. Named semaphores don't exist in .NET on Linux, and a mutex must be released by
/// the thread that took it; a lock file has neither problem.
/// </summary>
public sealed class InstanceLock : IDisposable
{
    private readonly FileStream _stream;

    private InstanceLock(FileStream stream) => _stream = stream;

    public static string RuntimeDir
    {
        get
        {
            string dir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } run && Directory.Exists(run) && OperatingSystem.IsLinux()
                ? Path.Combine(run, "bloxcode")
                : Path.Combine(Paths.Cache, "run");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static InstanceLock? TryAcquire(string name)
    {
        try
        {
            var stream = new FileStream(Path.Combine(RuntimeDir, name + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new InstanceLock(stream);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.Warn("InstanceLock::TryAcquire", $"{name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Waits for the lock, polling; null if it's still held when the time is up.</summary>
    public static async Task<InstanceLock?> AcquireAsync(string name, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            if (TryAcquire(name) is { } acquired)
                return acquired;
            if (DateTime.UtcNow >= deadline)
                return null;
            await Task.Delay(500, ct);
        }
    }

    public void Dispose() => _stream.Dispose();
}

/// <summary>
/// One menu at a time. The first menu listens on a Unix socket; a second start asks it to come to
/// the front and exits, like the Windows version does with its window handle.
/// </summary>
public sealed class MenuInstance : IDisposable
{
    private readonly InstanceLock _lock;
    private readonly Socket? _listener;
    private readonly CancellationTokenSource _cts = new();

    private static string SocketPath => Path.Combine(InstanceLock.RuntimeDir, "menu.sock");

    private MenuInstance(InstanceLock held, Action onFocusRequest)
    {
        _lock = held;
        try
        {
            if (File.Exists(SocketPath))
                File.Delete(SocketPath);
            _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            _listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
            _listener.Listen(4);
            _ = ListenAsync(onFocusRequest);
        }
        catch (Exception ex) when (ex is SocketException or IOException or ArgumentException or PlatformNotSupportedException)
        {
            // Focusing the other window is a nicety; the lock alone already prevents two menus.
            Logger.Warn("MenuInstance", $"No focus socket: {ex.Message}");
            _listener?.Dispose();
            _listener = null;
        }
    }

    /// <summary>The lock for this menu, or null after asking the existing menu to show itself.</summary>
    public static MenuInstance? Claim(Action onFocusRequest)
    {
        if (InstanceLock.TryAcquire("menu") is { } held)
            return new MenuInstance(held, onFocusRequest);

        try
        {
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            client.Connect(new UnixDomainSocketEndPoint(SocketPath));
            client.Send(Encoding.ASCII.GetBytes("focus\n"));
        }
        catch (Exception ex) when (ex is SocketException or IOException or ArgumentException or PlatformNotSupportedException)
        {
            Logger.Warn("MenuInstance::Claim", $"Couldn't reach the open menu: {ex.Message}");
        }
        return null;
    }

    private async Task ListenAsync(Action onFocusRequest)
    {
        while (!_cts.IsCancellationRequested && _listener is not null)
        {
            try
            {
                using var client = await _listener.AcceptAsync(_cts.Token);
                var buffer = new byte[16];
                int read = await client.ReceiveAsync(buffer, SocketFlags.None, _cts.Token);
                if (Encoding.ASCII.GetString(buffer, 0, read).StartsWith("focus", StringComparison.Ordinal))
                    onFocusRequest();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
                if (_cts.IsCancellationRequested)
                    return;
                Logger.Warn("MenuInstance::Listen", ex.Message);
                await Task.Delay(200);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener?.Dispose();
        try { File.Delete(SocketPath); } catch { }
        _lock.Dispose();
    }
}
