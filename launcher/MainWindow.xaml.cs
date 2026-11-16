using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
namespace StellaViet;
public partial class MainWindow : Window
{
 string package=Path.Combine(AppContext.BaseDirectory,"release");bool busy;bool checking;LanguageUpdate? available;
 readonly string settings=Path.Combine(PatchService.Store,"game-path.txt");
 public MainWindow(){InitializeComponent();GamePath.Text=File.Exists(settings)?File.ReadAllText(settings):@"C:\YostarGames\StellaSora_EN";LoadCachedPackage();LoadPackage();Loaded+=async (_,_)=>await CheckUpdates();Closing+=(_,e)=>{if(busy){e.Cancel=true;Message("Đang cập nhật. Hãy chờ hoàn tất trước khi đóng launcher.");}};PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&!busy)Home(this,new RoutedEventArgs());};}
 void LoadPackage(){try{var r=PatchService.LoadRelease(package);ReleaseInfo.Text="Bản dịch "+r.Version+" đã sẵn sàng.";InstallButton.IsEnabled=!busy;}catch{ReleaseInfo.Text="Chưa có bản Việt hóa đi kèm. Vui lòng tải lại BeroLauncher đầy đủ.";InstallButton.IsEnabled=!busy&&available!=null;}}
 void Home(object sender,RoutedEventArgs e){SettingsPanel.Visibility=Visibility.Collapsed;PlayButton.Focus();}
 void OpenSettings(object sender,RoutedEventArgs e){SettingsPanel.Visibility=Visibility.Visible;CloseSettingsButton.Focus();}
 void ChooseGame(object sender,RoutedEventArgs e){var dialog=new OpenFolderDialog{Title="Chọn vị trí cài Stella Sora"};if(dialog.ShowDialog()==true)GamePath.Text=dialog.FolderName;}
 void PathChanged(object sender,System.Windows.Controls.TextChangedEventArgs e){if(GameStatus==null)return;try{var valid=PatchService.IsGame(GamePath.Text);GameStatus.Text=valid?"Sẵn sàng chơi":"Chưa tìm thấy game";PlayButton.IsEnabled=valid&&!busy;Status.Text=valid?"Stella Sora đã được cài trên máy.":"Vào Cài đặt để chọn vị trí cài game.";if(valid){try{Directory.CreateDirectory(PatchService.Store);File.WriteAllText(settings,GamePath.Text);}catch{}}}catch{GameStatus.Text="Chưa tìm thấy game";PlayButton.IsEnabled=false;}}
 void Message(string text){Dispatcher.Invoke(()=>{Status.Text=text;SettingsStatus.Text=text;});}
 async Task Run(Action<string,string> action,string success){if(busy)return;UpdateButton.IsEnabled=CheckUpdatesButton.IsEnabled=false;var game=GamePath.Text.Trim();busy=true;GamePath.IsEnabled=BrowseButton.IsEnabled=InstallButton.IsEnabled=RestoreButton.IsEnabled=PlayButton.IsEnabled=false;Progress.Visibility=Visibility.Visible;Progress.IsIndeterminate=true;Message("Đang cập nhật ngôn ngữ…");
  try{await Task.Run(()=>action(game,package));Message(success);GameStatus.Text="Sẵn sàng chơi";}
  catch(Exception ex){try{Directory.CreateDirectory(PatchService.Store);File.AppendAllText(Path.Combine(PatchService.Store,"launcher.log"),DateTime.Now+" "+ex+Environment.NewLine);}catch{}Message(FriendlyError(ex));}
  finally{UpdateButton.IsEnabled=CheckUpdatesButton.IsEnabled=true;busy=false;GamePath.IsEnabled=BrowseButton.IsEnabled=RestoreButton.IsEnabled=true;PlayButton.IsEnabled=PatchService.IsGame(game);Progress.Visibility=Visibility.Collapsed;Progress.IsIndeterminate=false;LoadPackage();}
 }
 static string FriendlyError(Exception ex){var text=ex.Message;if(text.Contains("đóng game",StringComparison.OrdinalIgnoreCase))return "Hãy đóng game và launcher Yostar rồi thử lại.";if(text.Contains("phiên bản"))return "Bản Việt hóa chưa phù hợp với phiên bản game đang cài.";if(text.Contains("sao lưu")||text.Contains("bản gốc"))return "Chưa thể khôi phục game. Nếu đã dùng công cụ Việt hóa khác, hãy tắt Việt hóa bằng công cụ đó trước.";if(ex is UnauthorizedAccessException)return "Không có quyền thay đổi thư mục game. Hãy kiểm tra quyền truy cập rồi thử lại.";if(text.Contains("Chọn đúng"))return "Chưa tìm thấy game. Hãy chọn lại vị trí cài trong Cài đặt.";return "Chưa thể hoàn tất. Hãy đóng game, kiểm tra thư mục cài và thử lại.";}
 async void Install(object sender,RoutedEventArgs e){if(available!=null)await ApplyUpdate();else await Run((g,p)=>PatchService.Install(g,p,_=>{}),"Đã bật Việt hóa. Bạn có thể vào game.");}
 async void Restore(object sender,RoutedEventArgs e)=>await Run((g,_)=>PatchService.Restore(g,_=>{}),"Đã tắt Việt hóa.");
 void Play(object sender,RoutedEventArgs e){try{var game=Path.GetFullPath(GamePath.Text.Trim());if(!PatchService.IsGame(game)){OpenSettings(sender,e);return;}Process.Start(new ProcessStartInfo(Path.Combine(game,"StellaSora.exe")){WorkingDirectory=game,UseShellExecute=true});Message("Đang mở Stella Sora…");}catch{Message("Chưa thể mở game. Hãy kiểm tra vị trí cài trong Cài đặt.");}}
}



