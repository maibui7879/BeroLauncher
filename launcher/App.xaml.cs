using System.Windows;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace StellaViet;
public partial class App : Application {
 protected override void OnStartup(StartupEventArgs e){
  base.OnStartup(e);
  var window=new MainWindow();
  if(e.Args.Length==2&&(e.Args[0]=="--smoke-test"||e.Args[0]=="--smoke-settings")){
   if(e.Args[0]=="--smoke-settings")((FrameworkElement)window.FindName("SettingsPanel")).Visibility=Visibility.Visible;
   var content=(FrameworkElement)window.Content;
   content.Measure(new Size(1040,700));content.Arrange(new Rect(0,0,1040,700));content.UpdateLayout();
   var bitmap=new RenderTargetBitmap(1040,700,96,96,PixelFormats.Pbgra32);bitmap.Render(content);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
   using(var output=File.Create(e.Args[1]))encoder.Save(output);
   window.Close();Shutdown();return;
  }
  window.Show();
 }
}
