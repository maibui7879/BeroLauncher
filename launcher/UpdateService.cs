using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
namespace StellaViet;
public record UpdateFile(string Name,long Size,string Sha256,string Url);
public record LanguageUpdate(int Schema,string Repository,long Revision,string Version,string MinLauncherVersion,UpdateFile[] Files);
public record SignedUpdate(string Payload,string Signature);
public sealed class UpdateService
{
 public const string Feed="https://github.com/maibui7879/BeroLauncher/releases/download/lang-stable/channel.json";
 static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true};
 static readonly HttpClient Client=new(){Timeout=TimeSpan.FromMinutes(3)};
 public static string PublicKey {get{using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("BeroLauncher.UpdateKey")!;using var reader=new StreamReader(stream);return reader.ReadToEnd();}}
 public static LanguageUpdate Verify(string envelope,string publicKey){
  if(envelope.Length>65536)throw new InvalidDataException("Thông tin cập nhật quá lớn.");
  var signed=JsonSerializer.Deserialize<SignedUpdate>(envelope,Json)??throw new InvalidDataException();
  var payload=Convert.FromBase64String(signed.Payload);using var rsa=RSA.Create();rsa.ImportFromPem(publicKey);
  if(!rsa.VerifyData(payload,Convert.FromBase64String(signed.Signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))throw new InvalidDataException("Không xác minh được bản cập nhật.");
  var update=JsonSerializer.Deserialize<LanguageUpdate>(payload,Json)??throw new InvalidDataException();
  if(update.Schema!=1||update.Repository!="maibui7879/BeroLauncher"||update.Revision<=0||update.Version.Length!=13||!update.Version.All(char.IsAsciiDigit)||!Version.TryParse(update.MinLauncherVersion,out var minimum)||minimum>new Version(0,3,0))throw new InvalidDataException("Hãy cập nhật BeroLauncher trước.");
  if(update.Files is null||update.Files.Length!=3||!update.Files.Select(f=>f.Name).Order().SequenceEqual(new[]{"release.json","story.bin","ui.bin"}))throw new InvalidDataException("Bản cập nhật thiếu dữ liệu.");
  foreach(var f in update.Files){
   if(f.Size<=0||f.Size>268435456||f.Sha256.Length!=64||!f.Sha256.All(Uri.IsHexDigit)||f.Url!=$"https://github.com/maibui7879/BeroLauncher/releases/download/lang-v{update.Version}/{f.Name}")throw new InvalidDataException("Địa chỉ cập nhật không hợp lệ.");
  }
  return update;
 }
 static async Task<byte[]> FetchBounded(string url,long limit,CancellationToken cancellation){
  using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.UserAgent.ParseAdd("BeroLauncher/0.3.0");request.Headers.CacheControl=new(){NoCache=true};
  using var response=await Client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellation);response.EnsureSuccessStatusCode();
  if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("Tệp cập nhật vượt giới hạn.");
  await using var source=await response.Content.ReadAsStreamAsync(cancellation);using var output=new MemoryStream();var buffer=new byte[65536];int n;
  while((n=await source.ReadAsync(buffer,cancellation))!=0){if(output.Length+n>limit)throw new InvalidDataException("Tệp cập nhật vượt giới hạn.");await output.WriteAsync(buffer.AsMemory(0,n),cancellation);}return output.ToArray();
 }
 public static async Task<LanguageUpdate?> Check(CancellationToken cancellation=default){
  byte[] bytes;try{bytes=await FetchBounded(Feed,65536,cancellation);}catch(HttpRequestException e)when(e.StatusCode==HttpStatusCode.NotFound){return null;}
  var envelope=System.Text.Encoding.UTF8.GetString(bytes);var update=Verify(envelope,PublicKey);
  Directory.CreateDirectory(PatchService.Store);var high=Path.Combine(PatchService.Store,"update-revision.txt");
  if(File.Exists(high)&&long.TryParse(File.ReadAllText(high),out var previous)&&update.Revision<previous)throw new InvalidDataException("Kênh cập nhật cũ hơn bản đã kiểm tra.");
  File.WriteAllText(high,update.Revision.ToString());return update;
 }
 public static async Task<string> Download(LanguageUpdate update,Action<string> progress,CancellationToken cancellation=default){
  var packs=Path.Combine(PatchService.Store,"packs");Directory.CreateDirectory(packs);var temp=Path.Combine(packs,"download-"+Guid.NewGuid());Directory.CreateDirectory(temp);
  try{
   foreach(var f in update.Files){progress("Đang tải Việt hóa…");var bytes=await FetchBounded(f.Url,f.Size,cancellation);
    if(bytes.LongLength!=f.Size||!Convert.ToHexString(SHA256.HashData(bytes)).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Bản tải về bị lỗi. Hãy thử lại.");
    await File.WriteAllBytesAsync(Path.Combine(temp,f.Name),bytes,cancellation);
   }
   var release=PatchService.LoadRelease(temp);if(release.Version!=update.Version)throw new InvalidDataException("Phiên bản tải về không khớp.");
   var destination=Path.Combine(packs,update.Version+"-"+Guid.NewGuid());Directory.Move(temp,destination);return destination;
  }finally{if(Directory.Exists(temp))Directory.Delete(temp,true);}
 }
}
