namespace StellaViet;

public partial class MainWindow
{
    readonly System.Windows.Threading.DispatcherTimer gameTimer = new()
    {
        Interval = TimeSpan.FromSeconds(2),
    };
    bool pollingGame;
    bool gameRunning;
    bool launching;
    DateTime launchDeadline;
    string? languageOperation;

    async Task RefreshGameState()
    {
        if (pollingGame)
            return;
        pollingGame = true;
        try
        {
            gameRunning = await Task.Run(() =>
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("StellaSora");
                try
                {
                    return processes.Length > 0;
                }
                finally
                {
                    foreach (var process in processes)
                        process.Dispose();
                }
            });
            if (launching && (gameRunning || DateTime.UtcNow >= launchDeadline))
            {
                launching = false;
                if (!gameRunning)
                    Message("Chưa thấy game khởi động. Hãy thử lại.");
            }
        }
        catch { }
        finally
        {
            pollingGame = false;
            RefreshControls();
        }
    }

    void RefreshControls()
    {
        if (PlayLabel == null)
            return;
        var valid = PatchService.IsGame(GamePath.Text.Trim());
        var locked =
            busy
            || checking
            || launching
            || gameRunning
            || !sessionReady
            || exitRequested
            || sessionTick;
        PlayLabel.Text =
            launching ? "Đang khởi tạo..."
            : gameRunning ? "Game đang chạy..."
            : "Chơi ngay";
        PlayButton.IsEnabled =
            valid && !busy && !launching && !gameRunning && sessionReady && !exitRequested;
        GameStatus.Text =
            launching ? "Đang khởi tạo..."
            : gameRunning ? "Game đang chạy..."
            : valid ? "Sẵn sàng chơi"
            : "Chưa tìm thấy game";
        CheckUpdatesButton.IsEnabled = !locked;
        UpdateButton.IsEnabled = !locked;
        LauncherUpdateButton.IsEnabled = !locked;
        RestoreButton.IsEnabled = languageActive && !locked;
        InstallButton.IsEnabled =
            !locked
            && (
                available != null
                || (!languageActive && File.Exists(Path.Combine(package, "release.json")))
            );
        GamePath.IsEnabled = BrowseButton.IsEnabled =
            !busy
            && !checking
            && !launching
            && languageSession == null
            && sessionReady
            && !exitRequested;
        CheckLabel.Text = checking ? "Đang kiểm tra..." : "Kiểm tra cập nhật";
        UpdateLabel.Text = languageOperation ?? "Cập nhật";
        LanguageUpdateLabel.Text =
            languageOperation ?? (checking ? "Đang kiểm tra..." : "Cập nhật gói ngôn ngữ");
        RestoreLabel.Text =
            languageOperation == "Đang khôi phục..." ? languageOperation : "Dùng tiếng Anh";
        if (busy)
            InstallLabel.Text = languageOperation ?? "Đang cài đặt...";
        Progress.Visibility =
            busy || checking
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        Progress.IsIndeterminate =
            (busy || checking) && System.Windows.SystemParameters.ClientAreaAnimation;
    }

    void FitWorkArea()
    {
        var area = System.Windows.SystemParameters.WorkArea;
        MinWidth = Math.Min(MinWidth, Math.Max(640, area.Width - 24));
        MinHeight = Math.Min(MinHeight, Math.Max(480, area.Height - 24));
        Width = Math.Max(MinWidth, Math.Min(Width, area.Width - 24));
        Height = Math.Max(MinHeight, Math.Min(Height, area.Height - 24));
    }

    internal Task PreparePreview() => RefreshLanguageState();

    async Task RefreshLanguageState()
    {
        if (LanguageState == null || GamePath == null)
            return;
        var game = GamePath.Text.Trim();
        bool active = false;
        bool valid = false;
        try
        {
            (valid, active) = await Task.Run(() =>
            {
                if (!PatchService.IsGame(game))
                    return (false, false);
                var state = PatchService.State(game);
                return (
                    true,
                    state != null
                        && state.Components.All(c =>
                            PatchService.Hash(PatchService.Target(game, c.Name)) == c.Installed
                        )
                );
            });
        }
        catch { }
        if (game != GamePath.Text.Trim())
            return;
        languageActive = active;
        LanguageState.Text = active ? "Tiếng Việt đang bật" : "Đang dùng tiếng Anh";
        if (!valid)
            LanguageState.Text = "Chưa chọn game";
        RestoreButton.IsEnabled = active && !busy;
        InstallLabel.Text =
            available != null ? "Cập nhật"
            : active ? "Đã bật"
            : "Bật Việt hóa";
        InstallButton.IsEnabled =
            !busy
            && (
                available != null || (!active && File.Exists(Path.Combine(package, "release.json")))
            );
        RefreshControls();
    }
}
