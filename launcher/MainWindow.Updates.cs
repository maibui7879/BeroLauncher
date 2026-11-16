using System.IO;
using System.Windows;
namespace StellaViet;
public partial class MainWindow
{
 void LoadCachedPackage(){try{var saved=Path.Combine(PatchService.Store,"active-pack.txt");if(!File.Exists(saved))return;var cached=Path.GetFullPath(File.ReadAllText(saved));var root=Path.GetFullPath(Path.Combine(PatchService.Store,"packs"))+Path.DirectorySeparatorChar;if(cached.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&File.Exists(Path.Combine(cached,"release.json")))package=cached;}catch{}}
 async Task CheckUpdates(){
  if(checking||busy)return;checking=true;CheckUpdatesButton.IsEnabled=false;CheckLabel.Text="Đang kiểm tra…";
  try{
   await RefreshLanguageState();
   var update=await UpdateService.Check();
   if(update==null){ReleaseInfo.Text="Chưa có bản Việt hóa được phát hành trực tuyến.";return;}
   var installed=PatchService.IsGame(GamePath.Text)?PatchService.State(GamePath.Text):null;
   if(installed?.Version==update.Version&&languageActive){ReleaseInfo.Text="Bạn đang dùng bản Việt hóa mới nhất.";available=null;UpdateCard.Visibility=Visibility.Collapsed;return;}
   available=update;InstallLabel.Text=languageActive?"Cập nhật":"Bật Việt hóa";ReleaseInfo.Text="Có bản Việt hóa mới. Cập nhật để nhận những bản dịch mới nhất.";
   UpdateCard.Visibility=Visibility.Visible;UpdateButton.IsEnabled=true;InstallButton.IsEnabled=true;
   Message("Có bản Việt hóa mới.");
  }catch{ReleaseInfo.Text="Chưa kiểm tra được cập nhật. Bạn vẫn có thể dùng bản đang cài.";}
  finally{checking=false;CheckLabel.Text="Kiểm tra cập nhật";CheckUpdatesButton.IsEnabled=!busy;}
 }
 async void CheckUpdatesClick(object sender,RoutedEventArgs e)=>await CheckUpdates();
 async void UpdateClick(object sender,RoutedEventArgs e)=>await ApplyUpdate();
 async Task ApplyUpdate(){
  if(busy||available==null)return;
  var update=available;var game=GamePath.Text.Trim();
  if(!PatchService.IsGame(game)){OpenSettings(this,new RoutedEventArgs());Message("Hãy chọn vị trí cài game trước khi cập nhật.");return;}
  busy=true;UpdateButton.IsEnabled=CheckUpdatesButton.IsEnabled=GamePath.IsEnabled=BrowseButton.IsEnabled=InstallButton.IsEnabled=RestoreButton.IsEnabled=PlayButton.IsEnabled=false;
  Progress.Visibility=Visibility.Visible;Progress.IsIndeterminate=SystemParameters.ClientAreaAnimation;
  try{
   PatchService.CheckClosed();
   var downloaded=await UpdateService.Download(update,Message);
   Message("Đang cài Việt hóa…");
   await Task.Run(()=>PatchService.Install(game,downloaded,_=>{}));
   package=downloaded;try{File.WriteAllText(Path.Combine(PatchService.Store,"active-pack.txt"),downloaded);}catch{}available=null;UpdateCard.Visibility=Visibility.Collapsed;
   Message("Đã cập nhật Việt hóa. Bạn có thể vào game.");GameStatus.Text="Sẵn sàng chơi";ReleaseInfo.Text="Bạn đang dùng bản Việt hóa mới nhất.";
  }catch(Exception ex){
   try{Directory.CreateDirectory(PatchService.Store);File.AppendAllText(Path.Combine(PatchService.Store,"launcher.log"),DateTime.Now+" "+ex+Environment.NewLine);}catch{}
   Message(FriendlyError(ex));
  }finally{
   busy=false;UpdateButton.IsEnabled=CheckUpdatesButton.IsEnabled=GamePath.IsEnabled=BrowseButton.IsEnabled=RestoreButton.IsEnabled=true;
   InstallButton.IsEnabled=available!=null||File.Exists(Path.Combine(package,"release.json"));PlayButton.IsEnabled=PatchService.IsGame(game);
   Progress.Visibility=Visibility.Collapsed;Progress.IsIndeterminate=false;await RefreshLanguageState();
  }
 }
}


