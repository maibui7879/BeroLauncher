# BeroLauncher

A community launcher for Stella Sora on Windows, with a Vietnamese interface and support for Vietnamese language packs.

[Download the launcher](https://github.com/maibui7879/BeroLauncher/releases/tag/launcher-v0.4.5) · [Changelog](CHANGELOG.md) · [Report an issue](https://github.com/maibui7879/BeroLauncher/issues)

## Getting started

1. Download **BeroLauncher-Setup-<version>.exe** to install BeroLauncher, or **BeroLauncher-win-x64.zip** for the portable version.
2. If you chose the ZIP, extract the archive and run **BeroLauncher.exe**.
3. Open **Cài đặt** (Settings) and select your Stella Sora installation folder if it is not detected.
4. Close the game and the Yostar launcher before installing or changing a language pack.
5. Select **Cập nhật gói ngôn ngữ** (Update language pack) to download and install the latest compatible Vietnamese pack.
6. Select **Chơi ngay** (Play now) to launch the game.

Select **Dùng tiếng Anh** (Use English) on the home screen to disable Vietnamese and restore the original game files backed up by BeroLauncher.

## Language sessions

Keep BeroLauncher running while using Vietnamese. Closing its window keeps it running in the system tray. Double-click the tray icon to reopen it, or choose **Thoát và dùng tiếng Anh** (Exit and use English) to restore the original files and quit. If the game is running, the launcher waits for it to close first.

A separate background guardian monitors active language sessions. If the launcher crashes or is terminated, the guardian attempts to restore English after the game and Yostar launcher close. If the computer shuts down, both processes are terminated, or restoration fails, recovery remains pending and is retried the next time BeroLauncher opens. Game updates or missing backups may require manual intervention; immediate restoration cannot be guaranteed.

## Features

- Launch Stella Sora and display its running status, with the play button disabled while the game is running.
- Check for language pack updates automatically when the launcher opens.
- Update language packs or switch back to English from the home screen.
- Show progress while checking, downloading, installing, or restoring a language pack.
- Verify package signatures, file sizes, and SHA-256 hashes before installation.
- Check game data compatibility and back up original files before applying changes.
- Roll back file replacements if installation fails.

Language packs are distributed separately from launcher releases. An internet connection is required to check for and download new packs. The Windows x64 release is self-contained and does not require a separate .NET installation.

If another translation tool has modified your game, restore the original files with that tool before using BeroLauncher.

## Development

Requires Windows, .NET SDK 9 with Windows Desktop support, and Node.js 22 or later. Node.js is only needed for development tools.

```powershell
npm ci --ignore-scripts
dotnet tool restore
dotnet restore launcher/StellaViet.Launcher.csproj
dotnet restore launcher-tests/Launcher.Tests.csproj
npm run lint
dotnet build launcher/StellaViet.Launcher.csproj -c Release --no-restore
npm test
```

Run the launcher from source:

```powershell
dotnet run --project launcher/StellaViet.Launcher.csproj
```

Use `npm run format` to format the source. CI checks formatting, builds with warnings treated as errors, and runs installer tests against fixture data before packaging the application.

## License

Source code is licensed under [GPL-3.0](LICENSE). The Lexend font is licensed under the [SIL Open Font License](ui/fonts/OFL-Lexend.txt).

BeroLauncher is an unofficial community project and is not affiliated with or endorsed by Yostar. Stella Sora and its assets belong to their respective owners.
