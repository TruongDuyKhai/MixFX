; Soundboard installer (PROJECT_PROMPT.md §10.2).
; Build with ../build-installer.ps1, which publishes the app and passes the defines below.
; Saved as UTF-8 with BOM so Inno Setup reads the Vietnamese texts correctly.

#ifndef AppVersion
  #error Build with build-installer.ps1: AppVersion comes from Directory.Build.props.
#endif
#ifndef NumericVersion
  #define NumericVersion "0.0.0.0"
#endif
#ifndef SourceDir
  #error SourceDir (the dotnet publish output folder) is required.
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#define AppName "Soundboard"
#define AppExe "Soundboard.exe"
#define AppPublisher "TruongDuyKhai"
#define AppUrl "https://github.com/TruongDuyKhai/MixFX"
; Must match SingleInstance.MutexName in the app, so Setup can detect a running copy before replacing its files.
#define AppMutexName "Soundboard.SingleInstance.6E3F0C51-6F43-4C6B-9C3E-2B7C3F1E9A10"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"
; Official VB-Audio page. The cable is never bundled: its licence does not allow redistribution.
#define VbCableUrl "https://vb-audio.com/Cable/"

[Setup]
AppId={{7C252956-8025-4C9F-BC0E-58E6792F5890}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
VersionInfoVersion={#NumericVersion}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
; Per-user install by default (no admin prompt); the user may still choose "all users".
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; The startup entry is deliberately per-user (HKCU), matching the app's own checkbox.
UsedUserAreasWarning=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
AppMutex={#AppMutexName}
OutputDir={#OutputDir}
OutputBaseFilename=Soundboard-Setup-{#AppVersion}
SetupIconFile=..\src\Soundboard.App\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
TaskDesktop=Tạo shortcut trên Desktop
TaskStartup=Khởi động cùng Windows (chạy ẩn dưới khay hệ thống)
LaunchApp=Mở Soundboard
CableMissing=Chưa tìm thấy VB-Audio Virtual Cable.%n%nSoundboard cần micro ảo này để đồng đội nghe được giọng và sound. VB-Cable miễn phí nhưng phải tải và cài riêng từ trang chính thức của VB-Audio, sau đó khởi động lại máy.%n%nMở trang tải VB-Cable ngay bây giờ?
UninstallDataCaption=Gỡ cài đặt Soundboard
UninstallDataInfo=Thư viện sound, phím tắt và cài đặt của bạn nằm trong %AppData%\Soundboard. Mặc định chúng được giữ lại, để cài lại sau này vẫn còn nguyên.
UninstallDataCheck=Xóa cả dữ liệu (thư viện sound, phím tắt, cài đặt, log)
UninstallDataOk=Tiếp tục

[Tasks]
Name: "desktopicon"; Description: "{cm:TaskDesktop}"; Flags: unchecked
Name: "startup"; Description: "{cm:TaskStartup}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\licenses\Apache-2.0.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Same value the app's "Khởi động cùng Windows" checkbox writes, so the two stay in sync.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"" --minimized"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
function IsVbCableInstalled: Boolean;
var
  FindRec: TFindRec;
begin
  { VB-Cable installs a kernel driver named vbaudio_cable*.sys; the exact name depends on the Windows version. }
  Result := FindFirst(ExpandConstant('{sys}\drivers\vbaudio_cable*.sys'), FindRec);
  if Result then
    FindClose(FindRec);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ErrorCode: Integer;
begin
  if (CurStep = ssPostInstall) and not WizardSilent and not IsVbCableInstalled then
  begin
    if MsgBox(CustomMessage('CableMissing'), mbInformation, MB_YESNO) = IDYES then
      ShellExec('open', '{#VbCableUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;

var
  DeleteUserData: Boolean;

{ §10.2: user data is deleted only when the user ticks "Xóa cả dữ liệu"; the box starts unticked. }
function AskDeleteUserData: Boolean;
var
  Form: TSetupForm;
  Info: TNewStaticText;
  Check: TNewCheckBox;
  OkButton: TNewButton;
begin
  { Fixed height: nothing on the form can grow vertically. Without a parent form it centers on the screen. }
  Form := CreateCustomForm(ScaleX(440), ScaleY(150), False, True);
  try
    Form.Caption := CustomMessage('UninstallDataCaption');

    Info := TNewStaticText.Create(Form);
    Info.Parent := Form;
    Info.AutoSize := False;
    Info.WordWrap := True;
    Info.Left := ScaleX(16);
    Info.Top := ScaleY(14);
    Info.Width := Form.ClientWidth - ScaleX(32);
    Info.Height := ScaleY(48);
    Info.Caption := CustomMessage('UninstallDataInfo');

    Check := TNewCheckBox.Create(Form);
    Check.Parent := Form;
    Check.Left := ScaleX(16);
    Check.Top := ScaleY(70);
    Check.Width := Form.ClientWidth - ScaleX(32);
    Check.Height := ScaleY(20);
    Check.Caption := CustomMessage('UninstallDataCheck');
    Check.Checked := False;

    OkButton := TNewButton.Create(Form);
    OkButton.Parent := Form;
    OkButton.Caption := CustomMessage('UninstallDataOk');
    OkButton.Width := Form.CalculateButtonWidth([OkButton.Caption]);
    OkButton.Height := ScaleY(26);
    OkButton.Left := Form.ClientWidth - OkButton.Width - ScaleX(16);
    OkButton.Top := Form.ClientHeight - OkButton.Height - ScaleY(14);
    OkButton.ModalResult := mrOk;
    OkButton.Default := True;

    Form.ActiveControl := OkButton;
    Form.ShowModal();
    Result := Check.Checked;
  finally
    Form.Free();
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    { A silent uninstall never deletes user data. }
    DeleteUserData := False;
    if not UninstallSilent then
      DeleteUserData := AskDeleteUserData();

    { The app's own checkbox may have created the startup entry even if the installer task was not chosen. }
    RegDeleteValue(HKCU, '{#RunKey}', '{#AppName}');
  end;

  if (CurUninstallStep = usPostUninstall) and DeleteUserData then
    DelTree(ExpandConstant('{userappdata}\{#AppName}'), True, True, True);
end;
