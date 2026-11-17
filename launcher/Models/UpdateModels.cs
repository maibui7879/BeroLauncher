using System.Text.Json.Serialization;

namespace StellaViet;

public record UpdateFile(string Name, long Size, string Sha256, string Url);

public record LanguageUpdate(
    int Schema,
    string Repository,
    long Revision,
    string Version,
    string MinLauncherVersion,
    UpdateFile[] Files
)
{
    [JsonIgnore]
    public string? SignedEnvelope { get; init; }
}

public record SignedUpdate(string Payload, string Signature);
