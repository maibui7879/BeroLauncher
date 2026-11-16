using System.IO;
namespace StellaViet;
public partial class MainWindow
{
 void FitWorkArea(){var area=System.Windows.SystemParameters.WorkArea;MinWidth=Math.Min(MinWidth,Math.Max(640,area.Width-24));MinHeight=Math.Min(MinHeight,Math.Max(480,area.Height-24));Width=Math.Max(MinWidth,Math.Min(Width,area.Width-24));Height=Math.Max(MinHeight,Math.Min(Height,area.Height-24));}
 internal Task PreparePreview()=>RefreshLanguageState();
 async Task RefreshLanguageState(){
  if(LanguageState==null||GamePath==null)return;
  var game=GamePath.Text.Trim();
  bool active=false;bool valid=false;
  try{(valid,active)=await Task.Run(()=>{
   if(!PatchService.IsGame(game))return (false,false);
   var state=PatchService.State(game);
   return (true,state!=null&&state.Components.All(c=>PatchService.Hash(PatchService.Target(game,c.Name))==c.Installed));
  });}catch{}
  if(game!=GamePath.Text.Trim())return;
  languageActive=active;
  LanguageState.Text=active?"Tiếng Việt đang bật":"Chưa thiết lập Việt hóa";
  if(!valid)LanguageState.Text="Chưa chọn game";
  RestoreButton.IsEnabled=active&&!busy;
  InstallLabel.Text=available!=null?"Cập nhật":active?"Đã bật":"Bật Việt hóa";
  InstallButton.IsEnabled=!busy&&(available!=null||(!active&&File.Exists(Path.Combine(package,"release.json"))));
 }
}
