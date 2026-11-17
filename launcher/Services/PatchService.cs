using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace StellaViet;

public sealed class PatchService
{
    public static readonly string Store =
        AppContext.GetData("StellaViet.Store") as string
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StellaViet"
        );
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string Hash(string p)
    {
        using var f = File.OpenRead(p);
        return Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant();
    }

    public static string Target(string game, string name) =>
        Path.Combine(
            game,
            "Persistent_Store",
            name switch
            {
                "ui" => "Tables",
                "story" => "Scripts",
                _ => throw new InvalidDataException("Thành phần không hợp lệ."),
            },
            name == "ui" ? "data.arcx" : "lua.arcx"
        );

    static string StatePath(string game) =>
        Path.Combine(
            Store,
            Convert.ToHexString(
                SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(
                        Path.GetFullPath(game).TrimEnd('\\').ToUpperInvariant()
                    )
                )
            ) + ".json"
        );

    static string Backup(string sha) => Path.Combine(Store, "backups", sha + ".arcx");

    static bool Sha(string s) => s.Length == 64 && s.All(Uri.IsHexDigit);

    public static bool IsGame(string game) =>
        File.Exists(Path.Combine(game, "StellaSora.exe"))
        && File.Exists(Target(game, "ui"))
        && File.Exists(Target(game, "story"));

    public static void CheckClosed()
    {
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    var n = p.ProcessName;
                    if (
                        n.Contains("StellaSora", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Gamelauncher", StringComparison.OrdinalIgnoreCase)
                    )
                        throw new IOException("Hãy đóng game và launcher Yostar trước.");
                }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }
    }

    public static Release LoadRelease(string dir)
    {
        var manifest = Path.Combine(dir, "release.json");
        if (new FileInfo(manifest).Length > 16777216)
            throw new InvalidDataException("Thông tin gói quá lớn.");
        return ParseRelease(File.ReadAllText(manifest));
    }

    public static Release ParseRelease(string manifest)
    {
        var r =
            JsonSerializer.Deserialize<Release>(manifest, Json)
            ?? throw new InvalidDataException("Gói không hợp lệ.");
        if (
            r.Schema != 1
            || r.Components is null
            || r.Components.Length != 2
            || !r.Components.Select(c => c.Name).Order().SequenceEqual(new[] { "story", "ui" })
        )
            throw new InvalidDataException("Gói cần có đủ UI và thoại.");
        foreach (var c in r.Components)
        {
            if (
                !Sha(c.SourceSha256)
                || !Sha(c.OutputSha256)
                || !Sha(c.PayloadSha256)
                || c.OutputSize <= 0
                || c.OutputSize > 536870912
                || c.Operations is null
                || c.Operations.Length > 100000
                || c.Operations.Sum(o => (long)o.Length) != c.OutputSize
            )
                throw new InvalidDataException("Thông tin gói không hợp lệ.");
            if (
                c.Operations.Any(o =>
                    o.Kind is not ("copy" or "literal") || o.Offset < 0 || o.Length <= 0
                )
            )
                throw new InvalidDataException("Lệnh vá không hợp lệ.");
        }
        return r;
    }

    public static InstallState? State(string game)
    {
        var p = StatePath(game);
        return File.Exists(p)
            ? JsonSerializer.Deserialize<InstallState>(File.ReadAllText(p), Json)
            : null;
    }

    public static string Describe(string game)
    {
        if (!IsGame(game))
            return "Chưa tìm thấy dữ liệu game hợp lệ.";
        var s = State(game);
        if (s == null)
            return "Đã tìm thấy game · Chưa cài bằng launcher này.";
        return s.Components.All(c => Hash(Target(game, c.Name)) == c.Installed)
            ? "Đang dùng bản Việt hóa " + s.Version
            : "Dữ liệu đã thay đổi · cần kiểm tra trước khi cài.";
    }

    static void CopyRange(Stream from, Stream to, long offset, int length)
    {
        if (offset > from.Length || length > from.Length - offset)
            throw new InvalidDataException("Vùng dữ liệu vá vượt giới hạn.");
        from.Position = offset;
        var buf = new byte[Math.Min(length, 65536)];
        while (length > 0)
        {
            int n = from.Read(buf, 0, Math.Min(buf.Length, length));
            if (n == 0)
                throw new EndOfStreamException();
            to.Write(buf, 0, n);
            length -= n;
        }
    }

    public static void Install(
        string game,
        string dir,
        Action<string> progress,
        Action? checkClosed = null,
        string? publicKey = null
    )
    {
        using var gate = LockGame(game);
        var closed = checkClosed ?? CheckClosed;
        if (!IsGame(game))
            throw new IOException("Chọn đúng thư mục chứa StellaSora.exe.");
        closed();
        Directory.CreateDirectory(Store);
        Directory.CreateDirectory(Path.Combine(Store, "backups"));
        var r = ParseRelease(
            System.Text.Encoding.UTF8.GetString(UpdateService.VerifyPackage(dir, publicKey))
        );
        var state = State(game);
        var pending =
            new List<(
                string target,
                string staged,
                string rollback,
                string before,
                string after
            )>();
        try
        {
            foreach (var c in r.Components)
            {
                progress("Kiểm tra và dựng " + (c.Name == "ui" ? "UI" : "thoại") + "…");
                var target = Target(game, c.Name);
                var current = Hash(target);
                var previous = state?.Components.FirstOrDefault(x => x.Name == c.Name);
                if (
                    current != c.SourceSha256
                    && current != c.OutputSha256
                    && current != previous?.Installed
                )
                    throw new IOException(
                        "Dữ liệu game khác phiên bản được hỗ trợ. Hãy tắt mod cũ bằng công cụ đã cài nó trước."
                    );
                var original = Backup(c.SourceSha256);
                if (current == c.SourceSha256 && !File.Exists(original))
                    File.Copy(target, original);
                if (!File.Exists(original) || Hash(original) != c.SourceSha256)
                    throw new IOException("Thiếu bản gốc đúng phiên bản. Chưa thay đổi game.");
                var rollback = Backup(current);
                if (!File.Exists(rollback))
                    File.Copy(target, rollback);
                if (Hash(rollback) != current)
                    throw new IOException("Bản sao lưu không hợp lệ.");
                var payload = Path.Combine(dir, c.Name + ".bin");
                using var literal = File.OpenRead(payload);
                if (
                    !Convert
                        .ToHexString(SHA256.HashData(literal))
                        .Equals(c.PayloadSha256, StringComparison.OrdinalIgnoreCase)
                )
                    throw new IOException("Gói tải về bị lỗi: " + c.Name);
                var staged = target + ".stella-vi-" + Guid.NewGuid() + ".tmp";
                pending.Add((target, staged, rollback, current, c.OutputSha256));
                using (var source = File.OpenRead(original))
                using (var output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write))
                {
                    foreach (var op in c.Operations)
                        CopyRange(
                            op.Kind == "copy" ? source : literal,
                            output,
                            op.Offset,
                            op.Length
                        );
                    output.Flush(true);
                }
                if (Hash(staged) != c.OutputSha256)
                    throw new IOException("Kiểm tra gói dựng lại thất bại.");
            }
            Commit(
                pending,
                closed,
                () =>
                {
                    var newState = new InstallState(
                        Path.GetFullPath(game),
                        r.Version,
                        r.Components.Select(c => new InstalledComponent(
                                c.Name,
                                c.SourceSha256,
                                c.OutputSha256
                            ))
                            .ToArray()
                    );
                    var p = StatePath(game);
                    File.WriteAllText(p + ".tmp", JsonSerializer.Serialize(newState, Json));
                    File.Move(p + ".tmp", p, true);
                }
            );
        }
        finally
        {
            foreach (var t in pending)
                if (File.Exists(t.staged))
                    File.Delete(t.staged);
        }
    }

    static void Commit(
        List<(string target, string staged, string rollback, string before, string after)> tasks,
        Action closed,
        Action finish
    )
    {
        closed();
        foreach (var t in tasks)
            if (Hash(t.target) != t.before)
                throw new IOException("Dữ liệu vừa thay đổi. Hãy thử lại.");
        var done =
            new List<(
                string target,
                string staged,
                string rollback,
                string before,
                string after
            )>();
        try
        {
            foreach (var t in tasks)
            {
                File.Replace(t.staged, t.target, null);
                done.Add(t);
                if (Hash(t.target) != t.after)
                    throw new IOException("Kiểm tra sau khi cài thất bại.");
            }
            finish();
        }
        catch
        {
            foreach (var t in Enumerable.Reverse(done))
            {
                if (Hash(t.target) != t.after)
                    throw new IOException(
                        "File game vừa thay đổi, không tự ghi đè. Bản sao lưu tại: " + t.rollback
                    );
                File.Copy(t.rollback, t.staged, true);
                File.Replace(t.staged, t.target, null);
                if (Hash(t.target) != t.before)
                    throw new IOException("Khôi phục thất bại: " + t.rollback);
            }
            throw;
        }
    }

    sealed class Gate(Mutex mutex) : IDisposable
    {
        public void Dispose()
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }

    static IDisposable LockGame(string game)
    {
        var name =
            "Local\\StellaViet-"
            + Convert.ToHexString(
                SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(
                        Path.GetFullPath(game).TrimEnd('\\').ToUpperInvariant()
                    )
                )
            );
        var mutex = new Mutex(false, name);
        bool acquired;
        try
        {
            acquired = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
        }
        if (!acquired)
        {
            mutex.Dispose();
            throw new IOException("Một launcher khác đang xử lý game này.");
        }
        return new Gate(mutex);
    }

    public static void Restore(string game, Action<string> progress, Action? checkClosed = null)
    {
        using var gate = LockGame(game);
        var closed = checkClosed ?? CheckClosed;
        closed();
        var s = State(game) ?? throw new IOException("Launcher chưa có bản sao lưu cho game này.");
        var tasks =
            new List<(
                string target,
                string staged,
                string rollback,
                string before,
                string after
            )>();
        try
        {
            foreach (var c in s.Components)
            {
                progress("Khôi phục " + c.Name + "…");
                if (!Sha(c.Original) || !Sha(c.Installed))
                    throw new IOException("Lịch sử cài không hợp lệ.");
                var target = Target(game, c.Name);
                var before = Hash(target);
                if (before == c.Original)
                    continue;
                if (before != c.Installed)
                    throw new IOException("Dữ liệu đã đổi, không tự ghi đè bản cập nhật game.");
                var original = Backup(c.Original);
                if (Hash(original) != c.Original)
                    throw new IOException("Bản sao lưu gốc bị lỗi.");
                var rollback = Backup(before);
                if (!File.Exists(rollback))
                    File.Copy(target, rollback);
                if (Hash(rollback) != before)
                    throw new IOException("Bản sao lưu hiện tại bị lỗi.");
                var staged = target + ".stella-vi-" + Guid.NewGuid() + ".tmp";
                tasks.Add((target, staged, rollback, before, c.Original));
                File.Copy(original, staged);
                if (Hash(staged) != c.Original)
                    throw new IOException("Bản gốc tạm bị lỗi.");
            }
            Commit(tasks, closed, () => File.Delete(StatePath(game)));
        }
        finally
        {
            foreach (var t in tasks)
                if (File.Exists(t.staged))
                    File.Delete(t.staged);
        }
    }
}
