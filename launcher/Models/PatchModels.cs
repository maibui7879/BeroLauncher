namespace StellaViet;

public record PatchOp(string Kind, long Offset, int Length);

public record Component(
    string Name,
    string SourceSha256,
    string OutputSha256,
    long OutputSize,
    string PayloadSha256,
    PatchOp[] Operations
);

public record Release(int Schema, string Version, string CreatedAt, Component[] Components);

public record InstalledComponent(
    string Name,
    string Original,
    string Installed,
    string? Previous = null
);

public record InstallState(string GameDir, string Version, InstalledComponent[] Components);
