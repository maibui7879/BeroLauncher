using System.Windows;

namespace StellaViet;

public partial class MainWindow
{
    LanguageSession? languageSession;
    System.Windows.Forms.NotifyIcon? tray;
    System.Windows.Forms.ToolStripMenuItem? exitMenu;
    bool sessionReady;
    bool exitRequested;
    bool allowClose;
    bool sessionTick;

    void SetupTray()
    {
        if (tray != null)
            return;
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Mở BeroLauncher", null, (_, _) => Dispatcher.Invoke(ShowLauncher));
        exitMenu = new System.Windows.Forms.ToolStripMenuItem("Thoát và dùng tiếng Anh");
        exitMenu.Click += (_, _) => Dispatcher.InvokeAsync(RequestExit);
        menu.Items.Add(exitMenu);
        using var iconStream = typeof(App).Assembly.GetManifestResourceStream(
            "BeroLauncher.LauncherIcon"
        );
        using var sourceIcon = iconStream != null ? new System.Drawing.Icon(iconStream) : null;
        tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "BeroLauncher",
            Icon =
                sourceIcon != null
                    ? (System.Drawing.Icon)sourceIcon.Clone()
                    : System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowLauncher);
        Application.Current.SessionEnding += (_, _) => allowClose = true;
    }

    void ShowLauncher()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    async Task InitializeSession()
    {
        Message("Đang kiểm tra phiên Việt hóa trước...");
        sessionReady = await Task.Run(LanguageSession.RecoverPending);
        if (!sessionReady)
        {
            Message("Đang chờ khôi phục phiên trước. Hãy đóng game và launcher Yostar.");
            RefreshControls();
            return;
        }
        try
        {
            if (PatchService.State(GamePath.Text.Trim()) != null)
                await EnsureLanguageSession();
        }
        catch (Exception ex)
        {
            sessionReady = false;
            Message("Chưa thể bảo vệ phiên Việt hóa: " + ex.Message);
        }
        RefreshControls();
    }

    async Task EnsureLanguageSession()
    {
        var game = GamePath.Text.Trim();
        if (
            languageSession != null
            && !Path.GetFullPath(game)
                .Equals(languageSession.Game, StringComparison.OrdinalIgnoreCase)
        )
            throw new IOException("Hãy dùng tiếng Anh trước khi đổi thư mục game.");
        languageSession ??= new LanguageSession(game);
        await Task.Run(languageSession.EnsureGuardian);
    }

    void FinishLanguageSession()
    {
        languageSession?.Complete();
        languageSession = null;
    }

    async Task SessionTick()
    {
        if (sessionTick || busy || checking || allowClose)
            return;
        sessionTick = true;
        RefreshControls();
        try
        {
            if (!sessionReady)
                await InitializeSession();
            else if (languageSession != null)
                await Task.Run(languageSession.EnsureGuardian);
            if (exitRequested && sessionReady && !gameRunning && !launching)
                await ExitAndRestore();
        }
        catch (Exception ex)
        {
            sessionReady = false;
            Message("Chưa thể khôi phục hoặc giám sát phiên: " + ex.Message);
            ShowLauncher();
        }
        finally
        {
            sessionTick = false;
            RefreshControls();
        }
    }

    void RequestExit()
    {
        exitRequested = true;
        if (exitMenu != null)
            exitMenu.Text = "Đang chờ game đóng để thoát...";
        Message("Launcher sẽ khôi phục tiếng Anh và thoát sau khi game đóng.");
        _ = SessionTick();
        RefreshControls();
    }

    void ExitLauncher(object sender, RoutedEventArgs e) => RequestExit();

    async Task ExitAndRestore()
    {
        busy = true;
        languageOperation = "Đang khôi phục...";
        RefreshControls();
        try
        {
            var game = languageSession?.Game ?? GamePath.Text.Trim();
            await Task.Run(() =>
            {
                if (PatchService.State(game) != null)
                    PatchService.Restore(game, _ => { });
            });
            FinishLanguageSession();
            allowClose = true;
            Close();
        }
        catch (Exception ex)
        {
            exitRequested = false;
            if (exitMenu != null)
                exitMenu.Text = "Thoát và dùng tiếng Anh";
            Message("Chưa thể khôi phục tiếng Anh. Launcher vẫn chạy: " + ex.Message);
            ShowLauncher();
        }
        finally
        {
            busy = false;
            languageOperation = null;
            if (!allowClose)
                await RefreshLanguageState();
        }
    }
}
