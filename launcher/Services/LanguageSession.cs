using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StellaViet;

public sealed class LanguageSession : IDisposable
{
    public sealed record Record(string Game, int OwnerId, long OwnerStarted, string Token);

    static string Sessions => Path.Combine(PatchService.Store, "sessions");
    public string Game { get; }
    readonly Record record;
    Process? guardian;
    readonly object guardianLock = new();
    bool guardianReady;

    static string Key(string game) =>
        Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(Path.GetFullPath(game).TrimEnd('\\').ToUpperInvariant())
            )
        );

    static string RecordPath(string game) => Path.Combine(Sessions, Key(game) + ".json");

    static string GateName(string game) => "Local\\BeroLauncher-Session-" + Key(game);

    static string ReadyName(string token) => "Local\\BeroLauncher-Ready-" + token;

    public LanguageSession(string game)
    {
        Game = Path.GetFullPath(game);
        using var owner = Process.GetCurrentProcess();
        record = new(
            Game,
            owner.Id,
            owner.StartTime.ToUniversalTime().Ticks,
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(Sessions);
        using (
            var stream = new FileStream(
                RecordPath(Game),
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read
            )
        )
        {
            JsonSerializer.Serialize(stream, record);
            stream.Flush(true);
        }
    }

    public void EnsureGuardian() => EnsureGuardian(null);

    internal void EnsureGuardian(ProcessStartInfo? start)
    {
        lock (guardianLock)
            StartGuardian(start);
    }

    void StartGuardian(ProcessStartInfo? start)
    {
        if (guardian is { HasExited: false })
        {
            if (guardianReady)
                return;
            throw new IOException("Giám sát phiên chưa sẵn sàng.");
        }
        guardian?.Dispose();
        guardianReady = false;
        using var ready = new EventWaitHandle(
            false,
            EventResetMode.ManualReset,
            ReadyName(record.Token)
        );
        start ??= new ProcessStartInfo(
            Environment.ProcessPath ?? throw new IOException("Không tìm thấy launcher.")
        )
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("--watch-session");
        start.ArgumentList.Add(Game);
        start.ArgumentList.Add(record.Token);
        guardian = Process.Start(start) ?? throw new IOException("Không thể bật giám sát phiên.");
        if (!ready.WaitOne(TimeSpan.FromSeconds(15)))
            throw new IOException("Chưa khởi động được giám sát phiên. Hãy thử lại.");
        guardianReady = true;
    }

    static bool OwnerAlive(Record record)
    {
        try
        {
            using var owner = Process.GetProcessById(record.OwnerId);
            return !owner.HasExited
                && owner.StartTime.ToUniversalTime().Ticks == record.OwnerStarted;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        // Access failures must not be treated as proof that the owner has exited.
    }

    static IDisposable? TryGate(string game)
    {
        var mutex = new Mutex(false, GateName(game));
        try
        {
            if (mutex.WaitOne(0))
                return new Gate(mutex);
        }
        catch (AbandonedMutexException)
        {
            return new Gate(mutex);
        }
        mutex.Dispose();
        return null;
    }

    sealed class Gate(Mutex mutex) : IDisposable
    {
        public void Dispose()
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }

    static Record Read(string path)
    {
        if (new FileInfo(path).Length > 32768)
            throw new IOException("Lịch sử phiên không hợp lệ.");
        var record =
            JsonSerializer.Deserialize<Record>(File.ReadAllText(path))
            ?? throw new IOException("Lịch sử phiên không hợp lệ.");
        if (
            !Path.GetFullPath(path)
                .Equals(RecordPath(record.Game), StringComparison.OrdinalIgnoreCase)
        )
            throw new IOException("Lịch sử phiên không hợp lệ.");
        return record;
    }

    public static void Watch(string game, string token)
    {
        Watch(
            game,
            token,
            g => TryRestore(g),
            () => Thread.Sleep(2000),
            () =>
            {
                using var ready = EventWaitHandle.OpenExisting(ReadyName(token));
                ready.Set();
            }
        );
    }

    internal static void Watch(
        string game,
        string token,
        Action<string> restore,
        Action wait,
        Action ready
    )
    {
        var path = RecordPath(game);
        var record = Read(path);
        if (record.Token != token)
            throw new IOException("Phiên giám sát không hợp lệ.");
        IDisposable? acquired = null;
        for (int attempt = 0; attempt < 50 && acquired == null; attempt++)
        {
            acquired = TryGate(game);
            if (acquired == null)
                Thread.Sleep(100);
        }
        using var gate = acquired ?? throw new IOException("Phiên đang được giám sát.");
        ready();
        while (File.Exists(path))
        {
            try
            {
                if (Read(path).Token != token)
                    return;
                if (!OwnerAlive(record))
                {
                    restore(record.Game);
                    File.Delete(path);
                    return;
                }
            }
            catch (Exception ex)
                when (ex
                        is IOException
                            or UnauthorizedAccessException
                            or System.ComponentModel.Win32Exception
                            or JsonException
                )
            {
                Log(ex);
            }
            wait();
        }
    }

    static bool TryRestore(string game)
    {
        if (PatchService.State(game) == null)
            return true;
        PatchService.Restore(game, _ => { });
        return true;
    }

    public static bool RecoverPending() => RecoverPending(g => TryRestore(g));

    internal static bool RecoverPending(Action<string> restore)
    {
        if (!Directory.Exists(Sessions))
            return true;
        bool complete = true;
        foreach (var path in Directory.EnumerateFiles(Sessions, "*.json"))
        {
            try
            {
                var record = Read(path);
                if (OwnerAlive(record))
                    continue;
                using var gate = TryGate(record.Game);
                if (gate == null)
                {
                    complete = false;
                    continue;
                }
                restore(record.Game);
                File.Delete(path);
            }
            catch (Exception ex)
                when (ex
                        is IOException
                            or UnauthorizedAccessException
                            or System.ComponentModel.Win32Exception
                            or JsonException
                )
            {
                Log(ex);
                complete = false;
            }
        }
        return complete;
    }

    public void Complete()
    {
        if (PatchService.State(Game) != null)
            throw new IOException("Cần khôi phục tiếng Anh trước khi kết thúc phiên.");
        File.Delete(RecordPath(Game));
        Dispose();
    }

    static void Log(Exception error)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(PatchService.Store, "session.log"),
                DateTime.UtcNow + " " + error.Message + Environment.NewLine
            );
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        guardian?.Dispose();
        guardian = null;
    }
}
