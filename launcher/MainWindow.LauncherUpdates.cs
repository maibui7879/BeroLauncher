using System.Reflection;
using System.Windows;

namespace StellaViet;

public partial class MainWindow
{
    async Task CheckLauncherUpdate()
    {
        try
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
            var current = new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
            launcherUpdate = await LauncherUpdateService.Check(current);
            if (launcherUpdate == null)
            {
                LauncherUpdateButton.Visibility = Visibility.Collapsed;
                return;
            }
            LauncherUpdateLabel.Text = "Cập nhật launcher " + launcherUpdate.Version;
            LauncherUpdateButton.Visibility = Visibility.Visible;
            RefreshControls();
        }
        catch
        {
            launcherUpdate = null;
            LauncherUpdateButton.Visibility = Visibility.Collapsed;
        }
    }

    async void LauncherUpdateClick(object sender, RoutedEventArgs e) => await ApplyLauncherUpdate();

    async Task ApplyLauncherUpdate()
    {
        if (
            busy
            || checking
            || launching
            || gameRunning
            || launcherUpdate == null
            || !sessionReady
            || exitRequested
        )
            return;

        busy = true;
        languageOperation = "Đang tải launcher…";
        Progress.Visibility = Visibility.Visible;
        Progress.IsIndeterminate = SystemParameters.ClientAreaAnimation;
        RefreshControls();
        try
        {
            pendingLauncherUpdate = await LauncherUpdateService.DownloadAndStage(
                launcherUpdate,
                Message
            );
            Message("Đã tải launcher mới. Đang chờ khôi phục phiên để cập nhật an toàn…");
            LauncherUpdateButton.Visibility = Visibility.Collapsed;
        }
        catch
        {
            pendingLauncherUpdate = null;
            Message("Chưa thể tải launcher mới. Bản hiện tại vẫn dùng được.");
        }
        finally
        {
            busy = false;
            languageOperation = null;
            Progress.Visibility = Visibility.Collapsed;
            Progress.IsIndeterminate = false;
            RefreshControls();
        }

        if (pendingLauncherUpdate != null)
            RequestExit();
    }
}
