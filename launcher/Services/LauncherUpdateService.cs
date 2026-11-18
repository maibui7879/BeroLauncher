using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StellaViet;

public sealed record LauncherUpdate(
    string Version,
    string Tag,
    string Url,
    long Size,
    string Sha256,
    string Notes,
    string SignedEnvelope
);

public static class LauncherUpdateService
{
    const string Repository = "maibui7879/BeroLauncher";
    const string Feed =
        "https://github.com/maibui7879/BeroLauncher/releases/download/launcher-stable/channel.json";
    const string AssetName = "BeroLauncher-win-x64.zip";
    const long MaxAssetSize = 200_000_000;
    const long MaxExpandedSize = 500_000_000;
    static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(5) };
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    sealed record Envelope(string Payload, string Signature);

    sealed record Payload(
        int Schema,
        string Repository,
        string Version,
        string Tag,
        string Asset,
        long Size,
        string Sha256,
        string Notes
    );

    public static LauncherUpdate Verify(string envelope, string publicKey)
    {
        if (envelope.Length > 65536)
            throw new InvalidDataException("Thông tin cập nhật launcher quá lớn.");
        var signed =
            JsonSerializer.Deserialize<Envelope>(envelope, Json)
            ?? throw new InvalidDataException("Thông tin cập nhật launcher không hợp lệ.");
        var payloadBytes = Convert.FromBase64String(signed.Payload);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKey);
        if (
            !rsa.VerifyData(
                payloadBytes,
                Convert.FromBase64String(signed.Signature),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            )
        )
            throw new InvalidDataException("Không xác minh được bản cập nhật launcher.");

        var payload =
            JsonSerializer.Deserialize<Payload>(payloadBytes, Json)
            ?? throw new InvalidDataException("Thông tin cập nhật launcher không hợp lệ.");
        if (
            payload.Schema != 1
            || payload.Repository != Repository
            || string.IsNullOrEmpty(payload.Version)
            || !Version.TryParse(payload.Version, out var version)
            || version.ToString() != payload.Version
            || string.IsNullOrEmpty(payload.Tag)
            || payload.Tag != "launcher-v" + payload.Version
            || string.IsNullOrEmpty(payload.Asset)
            || payload.Asset != AssetName
            || payload.Size <= 0
            || payload.Size > MaxAssetSize
            || string.IsNullOrEmpty(payload.Sha256)
            || payload.Sha256.Length != 64
            || !payload.Sha256.All(Uri.IsHexDigit)
            || payload.Notes is null
            || payload.Notes.Length > 4096
        )
            throw new InvalidDataException("Bản cập nhật launcher không hợp lệ.");

        var url = $"https://github.com/{Repository}/releases/download/{payload.Tag}/{AssetName}";
        return new(
            payload.Version,
            payload.Tag,
            url,
            payload.Size,
            payload.Sha256,
            payload.Notes,
            envelope
        );
    }

    public static async Task<LauncherUpdate?> Check(
        Version currentVersion,
        CancellationToken cancellation = default
    )
    {
        byte[] bytes;
        try
        {
            bytes = await Fetch(Feed, 65536, cancellation);
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var key = UpdateService.PublicKey;
        var update = Verify(Encoding.UTF8.GetString(bytes), key);
        var candidate = Version.Parse(update.Version);
        var highVersionPath = Path.Combine(PatchService.Store, "launcher-update-version.txt");
        var floor = currentVersion;
        if (
            File.Exists(highVersionPath)
            && Version.TryParse(File.ReadAllText(highVersionPath), out var previous)
            && previous > floor
        )
            floor = previous;
        if (candidate < floor)
            throw new InvalidDataException("Kênh cập nhật launcher cũ hơn bản đã kiểm tra.");

        Directory.CreateDirectory(PatchService.Store);
        var highest = candidate > currentVersion ? candidate : currentVersion;
        if (highest > floor || !File.Exists(highVersionPath))
            File.WriteAllText(highVersionPath, highest.ToString(3));
        return candidate > currentVersion ? update : null;
    }

    public static async Task<string> DownloadAndStage(
        LauncherUpdate update,
        Action<string> progress,
        CancellationToken cancellation = default
    )
    {
        update = Verify(update.SignedEnvelope, UpdateService.PublicKey);
        var staging = Path.Combine(
            Path.GetTempPath(),
            "BeroLauncher-update-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(staging);
        var archivePath = Path.Combine(staging, AssetName);
        try
        {
            progress("Đang tải bản cập nhật launcher…");
            await DownloadAsset(update, archivePath, cancellation);
            var payload = Path.Combine(staging, "payload");
            Directory.CreateDirectory(payload);
            ExtractSafely(archivePath, payload);
            if (!File.Exists(Path.Combine(payload, "BeroLauncher.exe")))
                throw new InvalidDataException("Gói cập nhật thiếu BeroLauncher.exe.");
            File.Delete(archivePath);
            return payload;
        }
        catch
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            throw;
        }
    }

    static async Task<byte[]> Fetch(string url, long limit, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("BeroLauncher/" + CurrentVersion());
        request.Headers.CacheControl = new() { NoCache = true };
        using var response = await Client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellation
        );
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > limit)
            throw new InvalidDataException("Thông tin cập nhật vượt giới hạn.");
        await using var source = await response.Content.ReadAsStreamAsync(cancellation);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
        {
            if (output.Length + read > limit)
                throw new InvalidDataException("Thông tin cập nhật vượt giới hạn.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
        }
        return output.ToArray();
    }

    static async Task DownloadAsset(
        LauncherUpdate update,
        string destination,
        CancellationToken cancellation
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, update.Url);
        request.Headers.UserAgent.ParseAdd("BeroLauncher/" + CurrentVersion());
        using var response = await Client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellation
        );
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > update.Size)
            throw new InvalidDataException("Gói cập nhật vượt kích thước đã ký.");

        await using var source = await response.Content.ReadAsStreamAsync(cancellation);
        await using var output = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            65536,
            FileOptions.Asynchronous
        );
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
        {
            total += read;
            if (total > update.Size)
                throw new InvalidDataException("Gói cập nhật vượt kích thước đã ký.");
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
        }
        await output.FlushAsync(cancellation);
        if (
            total != update.Size
            || !Convert
                .ToHexString(hash.GetHashAndReset())
                .Equals(update.Sha256, StringComparison.OrdinalIgnoreCase)
        )
            throw new InvalidDataException("Gói cập nhật không khớp chữ ký.");
    }

    static void ExtractSafely(string archivePath, string destination)
    {
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        long expanded = 0;
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            var path = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Gói cập nhật chứa đường dẫn không hợp lệ.");
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException("Gói cập nhật chứa liên kết không hợp lệ.");
            expanded += entry.Length;
            if (expanded > MaxExpandedSize)
                throw new InvalidDataException("Gói giải nén vượt giới hạn.");
        }
        ZipFile.ExtractToDirectory(archivePath, destination);
    }

    public static void StartApply(string stagingDirectory, string installDirectory)
    {
        using var stream =
            Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream("BeroLauncher.LauncherUpdateScript")
            ?? throw new InvalidOperationException("Không tìm thấy trình cập nhật launcher.");
        using var reader = new StreamReader(stream);
        var scriptPath = Path.Combine(
            Path.GetTempPath(),
            "BeroLauncher-apply-" + Guid.NewGuid().ToString("N") + ".ps1"
        );
        File.WriteAllText(scriptPath, reader.ReadToEnd(), new UTF8Encoding(false));

        var executable = Path.Combine(installDirectory, "BeroLauncher.exe");
        var powershell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe"
        );
        var start = new ProcessStartInfo(powershell)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(scriptPath);
        start.ArgumentList.Add(stagingDirectory);
        start.ArgumentList.Add(installDirectory);
        start.ArgumentList.Add(executable);
        using var process =
            Process.Start(start)
            ?? throw new IOException("Không thể khởi động trình cập nhật launcher.");
    }

    static string CurrentVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";
}
