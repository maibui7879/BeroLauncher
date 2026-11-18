#define MyAppName "BeroLauncher"
#ifndef MyAppVersion
  #define MyAppVersion "0.4.5"
#endif

[Setup]
AppId={{B64389F1-5533-4DE1-B94D-31CC776590BD}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=BeroLauncher
DefaultDirName={localappdata}\Programs\BeroLauncher
DefaultGroupName=BeroLauncher
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=BeroLauncher-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\BeroLauncher.exe
WizardStyle=modern
CloseApplications=no
RestartApplications=no

[Files]
Source: "..\artifacts\BeroLauncher\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\BeroLauncher"; Filename: "{app}\BeroLauncher.exe"
Name: "{autodesktop}\BeroLauncher"; Filename: "{app}\BeroLauncher.exe"

[Run]
Filename: "{app}\BeroLauncher.exe"; Description: "Mở BeroLauncher"; Flags: nowait postinstall skipifsilent

[Code]
function IsLauncherRunning: Boolean;
var
  ResultCode: Integer;
  OutputPath: string;
  Output: TArrayOfString;
  I: Integer;
begin
  Result := False;
  OutputPath := ExpandConstant('{tmp}\BeroLauncher-processes.txt');
  if Exec(ExpandConstant('{cmd}'), '/C tasklist /FI "IMAGENAME eq BeroLauncher.exe" /NH > "' + OutputPath + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
  begin
    if LoadStringsFromFile(OutputPath, Output) then
      for I := 0 to GetArrayLength(Output) - 1 do
        if Pos('BeroLauncher.exe', Output[I]) > 0 then
        begin
          Result := True;
          Exit;
        end;
  end;
  DeleteFile(OutputPath);
end;

function InitializeSetup: Boolean;
begin
  Result := not IsLauncherRunning;
  if not Result then
    MsgBox('Hãy thoát BeroLauncher và kết thúc phiên Việt hóa để khôi phục file game trước khi cài đặt hoặc nâng cấp.', mbError, MB_OK);
end;

function InitializeUninstall: Boolean;
begin
  Result := not IsLauncherRunning;
  if not Result then
    MsgBox('Hãy thoát BeroLauncher và kết thúc phiên Việt hóa để khôi phục file game trước khi gỡ cài đặt.', mbError, MB_OK);
end;