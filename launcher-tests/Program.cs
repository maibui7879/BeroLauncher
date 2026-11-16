using System.Security.Cryptography;
using System.Text.Json;
using StellaViet;
var root=Path.Combine(Path.GetTempPath(),"StellaViet-test-"+Guid.NewGuid());Directory.CreateDirectory(root);
AppContext.SetData("StellaViet.Store",Path.Combine(root,"state"));
string Sha(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
void Fails(Action action){bool failed=false;try{action();}catch(IOException){failed=true;}Assert(failed,"Expected install rejection");}
try{
 using(var rsa=RSA.Create(2048)){
  var version="1794899999999";
  var files=new[]{"release.json","story.bin","ui.bin"}.Select(n=>new UpdateFile(n,1,new string('a',64),$"https://github.com/maibui7879/BeroLauncher/releases/download/lang-v{version}/{n}")).ToArray();
  string Envelope(LanguageUpdate u){var b=JsonSerializer.SerializeToUtf8Bytes(u);return JsonSerializer.Serialize(new SignedUpdate(Convert.ToBase64String(b),Convert.ToBase64String(rsa.SignData(b,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))));}
  var update=new LanguageUpdate(1,"maibui7879/BeroLauncher",42,version,"0.3.0",files);
  Assert(UpdateService.Verify(Envelope(update),rsa.ExportSubjectPublicKeyInfoPem()).Version==version,"Valid signature rejected");
  using var wrong=RSA.Create(2048);bool rejected=false;try{UpdateService.Verify(Envelope(update),wrong.ExportSubjectPublicKeyInfoPem());}catch(System.Security.Cryptography.CryptographicException){rejected=true;}catch(InvalidDataException){rejected=true;}Assert(rejected,"Wrong signer accepted");
  var bad=update with{Files=files.Select(f=>f with{Url="https://example.com/evil"}).ToArray()};
  rejected=false;try{UpdateService.Verify(Envelope(bad),rsa.ExportSubjectPublicKeyInfoPem());}catch(InvalidDataException){rejected=true;}Assert(rejected,"Unexpected host accepted");
  Console.WriteLine("PASS: signed feed, wrong signer rejection, URL allowlist");
 }
 var game=Path.Combine(root,"game");Directory.CreateDirectory(game);File.WriteAllText(Path.Combine(game,"StellaSora.exe"),"");
 var release=Path.Combine(root,"release");Directory.CreateDirectory(release);var components=new List<Component>();
 foreach(var name in new[]{"ui","story"}){
  var original=System.Text.Encoding.UTF8.GetBytes("original-"+name);var payload=System.Text.Encoding.UTF8.GetBytes("đã dịch-"+name);var output=original.Concat(payload).ToArray();
  var target=PatchService.Target(game,name);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.WriteAllBytes(target,original);File.WriteAllBytes(Path.Combine(release,name+".bin"),payload);
  components.Add(new Component(name,Sha(original),Sha(output),output.Length,Sha(payload),[new PatchOp("copy",0,original.Length),new PatchOp("literal",0,payload.Length)]));
 }
 File.WriteAllText(Path.Combine(release,"release.json"),JsonSerializer.Serialize(new Release(1,"test","today",components.ToArray())));
 PatchService.Install(game,release,_=>{},()=>{});
 foreach(var c in components)Assert(PatchService.Hash(PatchService.Target(game,c.Name))==c.OutputSha256,"Installed hash mismatch");
 PatchService.Install(game,release,_=>{},()=>{}); // Idempotent.
 PatchService.Restore(game,_=>{},()=>{});
 foreach(var c in components)Assert(PatchService.Hash(PatchService.Target(game,c.Name))==c.SourceSha256,"Restored hash mismatch");
 Console.WriteLine("PASS: dual-archive install, repeated install, restore");
 var story=PatchService.Target(game,"story");File.AppendAllText(story,"game update");
 Fails(()=>PatchService.Install(game,release,_=>{},()=>{}));
 Assert(PatchService.Hash(PatchService.Target(game,"ui"))==components[0].SourceSha256,"UI changed before rejecting unknown story");
 File.WriteAllText(story,"original-story");
 Console.WriteLine("PASS: unknown game version rejected without UI mutation");
 using(var locked=new FileStream(story,FileMode.Open,FileAccess.Read,FileShare.Read))Fails(()=>PatchService.Install(game,release,_=>{},()=>{}));
 Assert(PatchService.Hash(PatchService.Target(game,"ui"))==components[0].SourceSha256,"UI rollback failed after story replacement was blocked");
 Assert(PatchService.State(game)==null,"Failed install left committed state");
 Console.WriteLine("PASS: second archive replacement failure rolls back the first");
 File.AppendAllText(Path.Combine(release,"story.bin"),"corrupt");Fails(()=>PatchService.Install(game,release,_=>{},()=>{}));
 Assert(PatchService.Hash(PatchService.Target(game,"ui"))==components[0].SourceSha256,"UI changed despite corrupt payload");
 Assert(!Directory.GetFiles(game,"*.tmp",SearchOption.AllDirectories).Any(),"Staged files left behind");
 Console.WriteLine("PASS: corrupt payload rejected, staged files cleaned");
 if(args.Length==3){
  var livePack=args[0];var realGame=Path.Combine(root,"real-pack-fixture");Directory.CreateDirectory(realGame);File.WriteAllText(Path.Combine(realGame,"StellaSora.exe"),"");
  foreach(var (name,source) in new[]{("ui",args[1]),("story",args[2])}){var target=PatchService.Target(realGame,name);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target);}
  var realRelease=PatchService.LoadRelease(livePack);
  PatchService.Install(realGame,livePack,_=>{},()=>{});
  foreach(var c in realRelease.Components)Assert(PatchService.Hash(PatchService.Target(realGame,c.Name))==c.OutputSha256,"Real pack reconstruction failed");
  PatchService.Restore(realGame,_=>{},()=>{});
  foreach(var c in realRelease.Components)Assert(PatchService.Hash(PatchService.Target(realGame,c.Name))==c.SourceSha256,"Real pack restore failed");
  Console.WriteLine("PASS: GitHub-downloaded real language pack installs and restores on isolated game fixture");
 }
}finally{Directory.Delete(root,true);}
