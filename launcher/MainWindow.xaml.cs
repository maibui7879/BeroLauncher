using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace StellaViet;

public partial class MainWindow : Window
{
    string package = Path.Combine(AppContext.BaseDirectory, "release");
    bool busy;
    bool checking;
    bool languageActive;
    LanguageUpdate? available;
    readonly string settings = Path.Combine(PatchService.Store, "game-path.txt");

    public MainWindow()
    {
        InitializeComponent();
        FitWorkArea();
        GamePath.Text = File.Exists(settings)
            ? File.ReadAllText(settings)
            : @"C:\YostarGames\StellaSora_EN";
        LoadCachedPackage();
        LoadPackage();
        Loaded += async (_, _) =>
        {
            SetupTray();
            await RefreshGameState();
            await InitializeSession();
            gameTimer.Start();
            await RefreshLanguageState();
            await CheckUpdates();
        };
        gameTimer.Tick += async (_, _) =>
        {
            await RefreshGameState();
            await SessionTick();
        };
        Closed += (_, _) =>
        {
            gameTimer.Stop();
            tray?.Dispose();
            languageSession?.Dispose();
        };
        Closing += (_, e) =>
        {
            if (!allowClose && tray != null)
            {
                e.Cancel = true;
                Hide();
                tray.ShowBalloonTip(
                    3000,
                    "BeroLauncher",
                    "Launcher đang chạy nền. Chọn Thoát và dùng tiếng Anh ở khay hệ thống để thoát hoàn toàn.",
                    System.Windows.Forms.ToolTipIcon.Info
                );
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && !busy)
                Home(this, new RoutedEventArgs());
        };
    }

    void LoadPackage()
    {
        try
        {
            var r = PatchService.LoadRelease(package);
            ReleaseInfo.Text = "Bản Việt hóa đã sẵn sàng trên máy.";
            InstallButton.IsEnabled = !busy;
        }
        catch
        {
            ReleaseInfo.Text =
                available != null
                    ? "Có bản Việt hóa mới để tải về."
                    : "Kết nối Internet để nhận bản Việt hóa mới nhất.";
            InstallButton.IsEnabled = !busy && available != null;
        }
    }

    void Home(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
        MainShell.IsEnabled = Sidebar.IsEnabled = true;
        if (PlayButton.IsEnabled)
            PlayButton.Focus();
        else
            LibraryButton.Focus();
    }

    void OpenSettings(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = Visibility.Visible;
        MainShell.IsEnabled = Sidebar.IsEnabled = false;
        CloseSettingsButton.Focus();
    }

    void ChooseGame(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Chọn vị trí cài Stella Sora" };
        if (dialog.ShowDialog() == true)
            GamePath.Text = dialog.FolderName;
    }

    void PathChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (GameStatus == null)
            return;
        try
        {
            var valid = PatchService.IsGame(GamePath.Text);
            GameStatus.Text = valid ? "Sẵn sàng chơi" : "Chưa tìm thấy game";
            PlayButton.IsEnabled = valid && !busy;
            PathHint.Text = valid
                ? "Đã tìm thấy Stella Sora."
                : "Chưa tìm thấy game tại vị trí này.";
            Status.Text = valid
                ? "Stella Sora đã được cài trên máy."
                : "Vào Cài đặt để chọn vị trí cài game.";
            if (valid)
            {
                try
                {
                    Directory.CreateDirectory(PatchService.Store);
                    File.WriteAllText(settings, GamePath.Text);
                }
                catch { }
            }
        }
        catch
        {
            GameStatus.Text = "Chưa tìm thấy game";
            PlayButton.IsEnabled = false;
            PathHint.Text = "Đường dẫn chưa hợp lệ.";
        }
        _ = RefreshLanguageState();
        RefreshControls();
    }

    void Message(string text)
    {
        Dispatcher.Invoke(() =>
        {
            Status.Text = text;
            SettingsStatus.Text = text;
        });
    }

    async Task Run(Action<string, string> action, string success, string operation)
    {
        if (busy || checking || launching || gameRunning || !sessionReady || exitRequested)
            return;
        UpdateButton.IsEnabled = CheckUpdatesButton.IsEnabled = false;
        var game = GamePath.Text.Trim();
        busy = true;
        languageOperation = operation;
        GamePath.IsEnabled =
            BrowseButton.IsEnabled =
            InstallButton.IsEnabled =
            RestoreButton.IsEnabled =
            PlayButton.IsEnabled =
                false;
        Progress.Visibility = Visibility.Visible;
        Progress.IsIndeterminate = SystemParameters.ClientAreaAnimation;
        Message("Đang cập nhật ngôn ngữ…");
        RefreshControls();
        try
        {
            if (operation != "Đang khôi phục...")
                await EnsureLanguageSession();
            await Task.Run(() => action(game, package));
            if (operation == "Đang khôi phục...")
                FinishLanguageSession();
            Message(success);
            GameStatus.Text = "Sẵn sàng chơi";
        }
        catch (Exception ex)
        {
            try
            {
                Directory.CreateDirectory(PatchService.Store);
                File.AppendAllText(
                    Path.Combine(PatchService.Store, "launcher.log"),
                    DateTime.Now + " " + ex + Environment.NewLine
                );
            }
            catch { }
            Message(FriendlyError(ex));
        }
        finally
        {
            UpdateButton.IsEnabled = CheckUpdatesButton.IsEnabled = true;
            busy = false;
            languageOperation = null;
            GamePath.IsEnabled = BrowseButton.IsEnabled = RestoreButton.IsEnabled = true;
            PlayButton.IsEnabled = PatchService.IsGame(game);
            Progress.Visibility = Visibility.Collapsed;
            Progress.IsIndeterminate = false;
            LoadPackage();
            await RefreshLanguageState();
        }
    }

    static string FriendlyError(Exception ex)
    {
        var text = ex.Message;
        if (text.Contains("đóng game", StringComparison.OrdinalIgnoreCase))
            return "Hãy đóng game và launcher Yostar rồi thử lại.";
        if (text.Contains("phiên bản"))
            return "Bản Việt hóa chưa phù hợp với phiên bản game đang cài.";
        if (text.Contains("sao lưu") || text.Contains("bản gốc"))
            return "Chưa thể khôi phục game. Nếu đã dùng công cụ Việt hóa khác, hãy tắt Việt hóa bằng công cụ đó trước.";
        if (ex is UnauthorizedAccessException)
            return "Không có quyền thay đổi thư mục game. Hãy kiểm tra quyền truy cập rồi thử lại.";
        if (text.Contains("Chọn đúng"))
            return "Chưa tìm thấy game. Hãy chọn lại vị trí cài trong Cài đặt.";
        return "Chưa thể hoàn tất. Hãy đóng game, kiểm tra thư mục cài và thử lại.";
    }

    async void Install(object sender, RoutedEventArgs e)
    {
        if (available != null)
            await ApplyUpdate();
        else
            await Run(
                (g, p) => PatchService.Install(g, p, _ => { }),
                "Đã bật Việt hóa. Bạn có thể vào game.",
                "Đang cài đặt..."
            );
    }

    async void Restore(object sender, RoutedEventArgs e) =>
        await Run(
            (g, _) => PatchService.Restore(g, _ => { }),
            "Đã chuyển sang tiếng Anh.",
            "Đang khôi phục..."
        );

    async void Play(object sender, RoutedEventArgs e)
    {
        if (busy || launching || !sessionReady || exitRequested)
            return;
        await RefreshGameState();
        if (gameRunning || launching || busy)
            return;
        try
        {
            var game = Path.GetFullPath(GamePath.Text.Trim());
            if (!PatchService.IsGame(game))
            {
                OpenSettings(sender, e);
                return;
            }
            launching = true;
            launchDeadline = DateTime.UtcNow.AddSeconds(45);
            RefreshControls();
            Message("Đang khởi tạo Stella Sora...");
            using var process = await Task.Run(() =>
                Process.Start(
                    new ProcessStartInfo(Path.Combine(game, "StellaSora.exe"))
                    {
                        WorkingDirectory = game,
                        UseShellExecute = true,
                    }
                )
            );
        }
        catch
        {
            launching = false;
            Message("Chưa thể mở game. Hãy kiểm tra vị trí cài trong Cài đặt.");
        }
        finally
        {
            RefreshControls();
        }
    }
}
