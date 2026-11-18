using System.Security.Cryptography;
using System.Text.Json;
using StellaViet;

if (args.Length == 5 && args[0] == "--session-child" && args[2] == "--watch-session")
{
    AppContext.SetData("StellaViet.Store", args[1]);
    LanguageSession.Watch(args[3], args[4]);
    return;
}

var root = Path.Combine(Path.GetTempPath(), "StellaViet-test-" + Guid.NewGuid());
Directory.CreateDirectory(root);
AppContext.SetData("StellaViet.Store", Path.Combine(root, "state"));
string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
void Assert(bool ok, string message)
{
    if (!ok)
        throw new Exception(message);
}
void Fails(Action action)
{
    bool failed = false;
    try
    {
        action();
    }
    catch (IOException)
    {
        failed = true;
    }
    catch (InvalidDataException)
    {
        failed = true;
    }
    Assert(failed, "Expected install rejection");
}
try
{
    using (var rsa = RSA.Create(2048))
    {
        var version = "1794899999999";
        var files = new[] { "release.json", "story.bin", "ui.bin" }
            .Select(n => new UpdateFile(
                n,
                1,
                new string('a', 64),
                $"https://github.com/maibui7879/BeroLauncher/releases/download/lang-v{version}/{n}"
            ))
            .ToArray();
        string Envelope(LanguageUpdate u)
        {
            var b = JsonSerializer.SerializeToUtf8Bytes(u);
            return JsonSerializer.Serialize(
                new SignedUpdate(
                    Convert.ToBase64String(b),
                    Convert.ToBase64String(
                        rsa.SignData(b, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                    )
                )
            );
        }
        var update = new LanguageUpdate(1, "maibui7879/BeroLauncher", 42, version, "0.3.0", files);
        Assert(
            UpdateService.Verify(Envelope(update), rsa.ExportSubjectPublicKeyInfoPem()).Version
                == version,
            "Valid signature rejected"
        );
        var supportedMinimum = update with { MinLauncherVersion = "0.4.6" };
        Assert(
            UpdateService
                .Verify(Envelope(supportedMinimum), rsa.ExportSubjectPublicKeyInfoPem())
                .Version == version,
            "Current launcher minimum version rejected"
        );
        Fails(() =>
            UpdateService.Verify(
                Envelope(update with { MinLauncherVersion = "0.4.7" }),
                rsa.ExportSubjectPublicKeyInfoPem()
            )
        );
        using var wrong = RSA.Create(2048);
        bool rejected = false;
        try
        {
            UpdateService.Verify(Envelope(update), wrong.ExportSubjectPublicKeyInfoPem());
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            rejected = true;
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Assert(rejected, "Wrong signer accepted");
        var bad = update with
        {
            Files = files.Select(f => f with { Url = "https://example.com/evil" }).ToArray(),
        };
        rejected = false;
        try
        {
            UpdateService.Verify(Envelope(bad), rsa.ExportSubjectPublicKeyInfoPem());
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Assert(rejected, "Unexpected host accepted");
        Console.WriteLine("PASS: signed feed, wrong signer rejection, URL allowlist");

        var launcherPayload = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                schema = 1,
                repository = "maibui7879/BeroLauncher",
                version = "0.4.6",
                tag = "launcher-v0.4.6",
                asset = "BeroLauncher-win-x64.zip",
                size = 1000L,
                sha256 = new string('a', 64),
                notes = "Launcher update test",
            }
        );
        var launcherEnvelope = JsonSerializer.Serialize(
            new SignedUpdate(
                Convert.ToBase64String(launcherPayload),
                Convert.ToBase64String(
                    rsa.SignData(
                        launcherPayload,
                        HashAlgorithmName.SHA256,
                        RSASignaturePadding.Pkcs1
                    )
                )
            )
        );
        var launcherUpdate = LauncherUpdateService.Verify(
            launcherEnvelope,
            rsa.ExportSubjectPublicKeyInfoPem()
        );
        Assert(
            launcherUpdate.Url
                == "https://github.com/maibui7879/BeroLauncher/releases/download/launcher-v0.4.6/BeroLauncher-win-x64.zip",
            "Launcher update URL was not pinned to the release asset"
        );
        Fails(() =>
            LauncherUpdateService.Verify(launcherEnvelope, wrong.ExportSubjectPublicKeyInfoPem())
        );
        var wrongAsset = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                schema = 1,
                repository = "maibui7879/BeroLauncher",
                version = "0.4.6",
                tag = "launcher-v0.4.6",
                asset = "other.exe",
                size = 1000L,
                sha256 = new string('a', 64),
                notes = "Launcher update test",
            }
        );
        var wrongAssetEnvelope = JsonSerializer.Serialize(
            new SignedUpdate(
                Convert.ToBase64String(wrongAsset),
                Convert.ToBase64String(
                    rsa.SignData(wrongAsset, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                )
            )
        );
        Fails(() =>
            LauncherUpdateService.Verify(wrongAssetEnvelope, rsa.ExportSubjectPublicKeyInfoPem())
        );
        Console.WriteLine("PASS: launcher update signature, release URL and asset allowlist");
    }
    using var packSigner = RSA.Create(2048);
    var packKey = packSigner.ExportSubjectPublicKeyInfoPem();
    const string packVersion = "1794899999999";
    void SignPack(string dir)
    {
        var files = new[] { "release.json", "story.bin", "ui.bin" }
            .Select(n =>
            {
                var bytes = File.ReadAllBytes(Path.Combine(dir, n));
                return new UpdateFile(
                    n,
                    bytes.LongLength,
                    Sha(bytes),
                    $"https://github.com/maibui7879/BeroLauncher/releases/download/lang-v{packVersion}/{n}"
                );
            })
            .ToArray();
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new LanguageUpdate(1, "maibui7879/BeroLauncher", 42, packVersion, "0.3.0", files)
        );
        File.WriteAllText(
            Path.Combine(dir, "channel.json"),
            JsonSerializer.Serialize(
                new SignedUpdate(
                    Convert.ToBase64String(payload),
                    Convert.ToBase64String(
                        packSigner.SignData(
                            payload,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1
                        )
                    )
                )
            )
        );
    }
    var game = Path.Combine(root, "game");
    Directory.CreateDirectory(game);
    File.WriteAllText(Path.Combine(game, "StellaSora.exe"), "");
    var release = Path.Combine(root, "release");
    Directory.CreateDirectory(release);
    var components = new List<Component>();
    foreach (var name in new[] { "ui", "story" })
    {
        var original = System.Text.Encoding.UTF8.GetBytes("original-" + name);
        var payload = System.Text.Encoding.UTF8.GetBytes("đã dịch-" + name);
        var output = original.Concat(payload).ToArray();
        var target = PatchService.Target(game, name);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllBytes(target, original);
        File.WriteAllBytes(Path.Combine(release, name + ".bin"), payload);
        components.Add(
            new Component(
                name,
                Sha(original),
                Sha(output),
                output.Length,
                Sha(payload),
                [new PatchOp("copy", 0, original.Length), new PatchOp("literal", 0, payload.Length)]
            )
        );
    }
    File.WriteAllText(
        Path.Combine(release, "release.json"),
        JsonSerializer.Serialize(new Release(1, packVersion, "today", components.ToArray()))
    );
    SignPack(release);
    var trustedManifest = File.ReadAllText(Path.Combine(release, "release.json"));
    var trustedPayload = File.ReadAllBytes(Path.Combine(release, "ui.bin"));
    File.AppendAllText(Path.Combine(release, "ui.bin"), "injected");
    var altered = components
        .Select(c =>
            c.Name == "ui"
                ? c with
                {
                    PayloadSha256 = PatchService.Hash(Path.Combine(release, "ui.bin")),
                }
                : c
        )
        .ToArray();
    File.WriteAllText(
        Path.Combine(release, "release.json"),
        JsonSerializer.Serialize(new Release(1, packVersion, "today", altered))
    );
    Fails(() => PatchService.Install(game, release, _ => { }, () => { }, packKey));
    Assert(
        PatchService.Hash(PatchService.Target(game, "ui")) == components[0].SourceSha256,
        "Unsigned cache change wrote to game"
    );
    File.WriteAllText(Path.Combine(release, "release.json"), trustedManifest);
    File.WriteAllBytes(Path.Combine(release, "ui.bin"), trustedPayload);
    var envelope = File.ReadAllText(Path.Combine(release, "channel.json"));
    File.Delete(Path.Combine(release, "channel.json"));
    Fails(() => PatchService.Install(game, release, _ => { }, () => { }, packKey));
    File.WriteAllText(Path.Combine(release, "channel.json"), envelope);
    Console.WriteLine(
        "PASS: tampered manifest plus payload and unsigned cached packs rejected before writes"
    );
    PatchService.Install(game, release, _ => { }, () => { }, packKey);
    foreach (var c in components)
        Assert(
            PatchService.Hash(PatchService.Target(game, c.Name)) == c.OutputSha256,
            "Installed hash mismatch"
        );
    PatchService.Install(game, release, _ => { }, () => { }, packKey); // Idempotent.
    PatchService.Restore(game, _ => { }, () => { });
    foreach (var c in components)
        Assert(
            PatchService.Hash(PatchService.Target(game, c.Name)) == c.SourceSha256,
            "Restored hash mismatch"
        );
    Console.WriteLine("PASS: dual-archive install, repeated install, restore");
    PatchService.Install(game, release, _ => { }, () => { }, packKey);
    using (var session = new LanguageSession(game))
    {
        var recordPath = Directory
            .GetFiles(Path.Combine(PatchService.Store, "sessions"), "*.json")
            .Single();
        var sessionRecord = JsonSerializer.Deserialize<LanguageSession.Record>(
            File.ReadAllText(recordPath)
        )!;
        bool touched = false;
        Assert(
            LanguageSession.RecoverPending(_ => touched = true) && !touched,
            "Recovery touched a live owner's session"
        );
        File.WriteAllText(
            recordPath,
            JsonSerializer.Serialize(sessionRecord with { OwnerStarted = 0 })
        );
        Assert(
            !LanguageSession.RecoverPending(_ => throw new IOException("Game is running")),
            "Blocked restore was reported complete"
        );
        Assert(
            File.Exists(recordPath) && PatchService.State(game) != null,
            "Pending recovery was lost"
        );
        int attempts = 0;
        int waits = 0;
        LanguageSession.Watch(
            game,
            sessionRecord.Token,
            g =>
            {
                if (++attempts == 1)
                    throw new IOException("Game is running");
                PatchService.Restore(g, _ => { }, () => { });
            },
            () =>
            {
                waits++;
            },
            () => { }
        );
        Assert(
            attempts == 2 && waits == 1 && !File.Exists(recordPath),
            "Guardian did not wait and restore after owner exit"
        );
        Assert(PatchService.State(game) == null, "Guardian left installed state behind");
    }
    Console.WriteLine(
        "PASS: live session protected, blocked recovery retained, guardian restores after owner exit"
    );
    PatchService.Install(game, release, _ => { }, () => { }, packKey);
    var committedState = Directory.GetFiles(PatchService.Store, "*.json").Single();
    var interrupted = PatchService.State(game)!;
    File.WriteAllText(
        committedState + ".recovery",
        JsonSerializer.Serialize(
            interrupted with
            {
                Components = interrupted
                    .Components.Select(c => c with { Previous = c.Original })
                    .ToArray(),
            }
        )
    );
    File.Delete(committedState);
    File.Copy(
        Path.Combine(PatchService.Store, "backups", components[0].SourceSha256 + ".arcx"),
        PatchService.Target(game, "ui"),
        true
    );
    using (var session = new LanguageSession(game))
    {
        var recordPath = Directory
            .GetFiles(Path.Combine(PatchService.Store, "sessions"), "*.json")
            .Single();
        var sessionRecord = JsonSerializer.Deserialize<LanguageSession.Record>(
            File.ReadAllText(recordPath)
        )!;
        File.WriteAllText(
            recordPath,
            JsonSerializer.Serialize(sessionRecord with { OwnerStarted = 0 })
        );
        Assert(
            LanguageSession.RecoverPending(g => PatchService.Restore(g, _ => { }, () => { })),
            "Startup recovery failed"
        );
        foreach (var component in components)
            Assert(
                PatchService.Hash(PatchService.Target(game, component.Name))
                    == component.SourceSha256,
                "Partial install was not restored"
            );
        Assert(!File.Exists(committedState + ".recovery"), "Recovery journal was not cleared");
    }
    Console.WriteLine("PASS: interrupted two-file install recovered from journal on startup");
    using (var session = new LanguageSession(Path.Combine(root, "guardian-handshake")))
    {
        var recordPath = Directory
            .GetFiles(Path.Combine(PatchService.Store, "sessions"), "*.json")
            .Single();
        var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("--session-child");
        start.ArgumentList.Add(PatchService.Store);
        session.EnsureGuardian(start);
        Assert(File.Exists(recordPath), "Guardian cleaned up while owner was alive");
        session.Complete();
        Assert(!File.Exists(recordPath), "Completed session marker remained");
    }
    Console.WriteLine(
        "PASS: separate guardian process ready handshake and clean session completion"
    );
    var story = PatchService.Target(game, "story");
    File.AppendAllText(story, "game update");
    Fails(() => PatchService.Install(game, release, _ => { }, () => { }, packKey));
    Assert(
        PatchService.Hash(PatchService.Target(game, "ui")) == components[0].SourceSha256,
        "UI changed before rejecting unknown story"
    );
    File.WriteAllText(story, "original-story");
    Console.WriteLine("PASS: unknown game version rejected without UI mutation");
    using (var locked = new FileStream(story, FileMode.Open, FileAccess.Read, FileShare.Read))
        Fails(() => PatchService.Install(game, release, _ => { }, () => { }, packKey));
    Assert(
        PatchService.Hash(PatchService.Target(game, "ui")) == components[0].SourceSha256,
        "UI rollback failed after story replacement was blocked"
    );
    Assert(PatchService.State(game) == null, "Failed install left committed state");
    Console.WriteLine("PASS: second archive replacement failure rolls back the first");
    File.AppendAllText(Path.Combine(release, "story.bin"), "corrupt");
    Fails(() => PatchService.Install(game, release, _ => { }, () => { }, packKey));
    Assert(
        PatchService.Hash(PatchService.Target(game, "ui")) == components[0].SourceSha256,
        "UI changed despite corrupt payload"
    );
    Assert(
        !Directory.GetFiles(game, "*.tmp", SearchOption.AllDirectories).Any(),
        "Staged files left behind"
    );
    Console.WriteLine("PASS: corrupt payload rejected, staged files cleaned");
    if (args.Length == 3)
    {
        var livePack = args[0];
        var realGame = Path.Combine(root, "real-pack-fixture");
        Directory.CreateDirectory(realGame);
        File.WriteAllText(Path.Combine(realGame, "StellaSora.exe"), "");
        foreach (var (name, source) in new[] { ("ui", args[1]), ("story", args[2]) })
        {
            var target = PatchService.Target(realGame, name);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }
        var realRelease = PatchService.LoadRelease(livePack);
        PatchService.Install(realGame, livePack, _ => { }, () => { });
        foreach (var c in realRelease.Components)
            Assert(
                PatchService.Hash(PatchService.Target(realGame, c.Name)) == c.OutputSha256,
                "Real pack reconstruction failed"
            );
        PatchService.Restore(realGame, _ => { }, () => { });
        foreach (var c in realRelease.Components)
            Assert(
                PatchService.Hash(PatchService.Target(realGame, c.Name)) == c.SourceSha256,
                "Real pack restore failed"
            );
        Console.WriteLine(
            "PASS: GitHub-downloaded real language pack installs and restores on isolated game fixture"
        );
    }
}
finally
{
    Directory.Delete(root, true);
}
