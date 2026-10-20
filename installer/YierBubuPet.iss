; Inno Setup 6 script for Yier & Bubu.
;
; Easiest: run installer\build.ps1, which publishes the app and then compiles this script.
; By hand (from the repository root):
;   dotnet publish src\YierBubuPet -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist\win-x64
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\YierBubuPet.iss
; For ARM PCs publish with -r win-arm64 -o dist\win-arm64 and compile with /DArch=arm64.
; The installer is written to dist\YierBubuPet-<version>-setup-<arch>.exe.

#ifndef Arch
  #define Arch "x64"
#endif
#if Arch != "x64" && Arch != "arm64"
  #error Arch must be x64 or arm64
#endif

#define AppName "Yier & Bubu"
#define AppExe "YierBubuPet.exe"
#define AppPublisher "lesyhau"
#define AppUrl "https://github.com/lesyhau/yier-bubu-pet"
#define SourceDir AddBackslash(SourcePath) + "..\dist\win-" + Arch
#define RepoDir AddBackslash(SourcePath) + ".."

#if !FileExists(SourceDir + "\" + AppExe)
  #error Publish the app first (see the top of this file), or run installer\build.ps1.
#endif

; Version comes from the exe (set by <Version> in YierBubuPet.csproj) unless given with /DAppVersion=1.2.3
#ifndef AppVersion
  #define AppVersion GetVersionNumbersString(SourceDir + "\" + AppExe)
  #if Copy(AppVersion, Len(AppVersion) - 1, 2) == ".0"
    #define AppVersion Copy(AppVersion, 1, Len(AppVersion) - 2)
  #endif
#endif

[Setup]
; Keep AppId the same in every release so new versions upgrade the old one in place.
AppId={{6F0B6C5E-3D7A-4C59-9F0E-5B1E2C8A9D41}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\YierBubuPet
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile={#RepoDir}\LICENSE
SetupIconFile={#RepoDir}\src\YierBubuPet\Resources\icon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir={#RepoDir}\dist
OutputBaseFilename=YierBubuPet-{#AppVersion}-setup-{#Arch}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Installs for the current user by default (no admin prompt, into %LocalAppData%\Programs).
; The user can still choose "Install for all users" on the first page.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
; The app holds this mutex while running, so setup and uninstall ask to close it first.
AppMutex=YierBubuPet.SingleInstance
CloseApplications=yes
; The "start with Windows" entry is per user on purpose, also for all-users installs.
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Start {#AppName} when I sign in to Windows"; GroupDescription: "Startup:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; The same value the app's "Start with Windows" menu item writes, so either can turn it off again.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "YierBubuPet"; ValueData: """{app}\{#AppExe}"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Folder: String;
begin
  if CurUninstallStep <> usPostUninstall then Exit;

  { "Start with Windows" may have been switched on from the app's menu rather than by setup. }
  RegDeleteValue(HKEY_CURRENT_USER, RunKey, 'YierBubuPet');

  { Downloaded stickers and your own animations and sounds aren't removed unless you say so. }
  Folder := ExpandConstant('{app}\animations');
  if not DirExists(Folder) then
    Folder := ExpandConstant('{localappdata}\YierBubuPet\animations');
  if DirExists(Folder) and not UninstallSilent then
    if MsgBox('Also delete the downloaded stickers and your own animations and sounds?' + #13#10#13#10 + Folder,
              mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(Folder, True, True, True);
  RemoveDir(ExpandConstant('{app}'));
end;
