using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StellaViet;

public partial class App : Application
{
    Mutex? instance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 3 && e.Args[0] == "--watch-session")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                await Task.Run(() => LanguageSession.Watch(e.Args[1], e.Args[2]));
            }
            catch
            {
                Shutdown(1);
                return;
            }
            Shutdown();
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--verify-update")
        {
            try
            {
                var update =
                    await UpdateService.Check() ?? throw new IOException("No published update");
                var dir = await UpdateService.Download(update, _ => { });
                File.WriteAllText(
                    e.Args[1],
                    System.Text.Json.JsonSerializer.Serialize(
                        new
                        {
                            update.Version,
                            Directory = dir,
                            Verified = true,
                        }
                    )
                );
                Shutdown();
            }
            catch (Exception error)
            {
                File.WriteAllText(
                    e.Args[1],
                    System.Text.Json.JsonSerializer.Serialize(new { Error = error.Message })
                );
                Shutdown(1);
            }
            return;
        }
        bool preview =
            e.Args.Length == 2 && (e.Args[0] == "--smoke-test" || e.Args[0] == "--smoke-settings");
        if (!preview)
        {
            instance = new Mutex(false, "Local\\BeroLauncher-Interactive");
            bool acquired;
            try
            {
                acquired = instance.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }
            if (!acquired)
            {
                MessageBox.Show(
                    "BeroLauncher đang chạy. Hãy mở từ biểu tượng ở khay hệ thống.",
                    "BeroLauncher"
                );
                instance.Dispose();
                instance = null;
                Shutdown();
                return;
            }
            Exit += (_, _) =>
            {
                instance.ReleaseMutex();
                instance.Dispose();
            };
        }
        var window = new MainWindow();
        if (e.Args.Length == 2 && (e.Args[0] == "--smoke-test" || e.Args[0] == "--smoke-settings"))
        {
            await window.PreparePreview();
            if (e.Args[0] == "--smoke-settings")
                ((FrameworkElement)window.FindName("SettingsPanel")).Visibility =
                    Visibility.Visible;
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(1040, 700));
            content.Arrange(new Rect(0, 0, 1040, 700));
            content.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1040, 700, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var output = File.Create(e.Args[1]))
                encoder.Save(output);
            window.Close();
            Shutdown();
            return;
        }
        window.Show();
    }
}
