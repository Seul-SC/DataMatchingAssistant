; 数据匹配助手 Inno Setup 安装脚本（1.3.0 起的正式安装方式）
;
; 编译（在仓库根目录）：
;   ISCC.exe /DAppVersion=1.3.0 /DPublishDir=..\build\publish /DOutputDir=..\release Installer\DataMatchingAssistant.iss
; 输出：release\DataMatchingAssistant-<版本>-Setup.exe
;
; 要点：
; - AppId 固定不变（与 1.0.0 的 Inno 安装包相同），升级时靠它认出同一个软件；
; - 显示「选择安装位置」页（带「浏览」），再次安装/升级时默认沿用上次的目录；
; - 默认按当前用户安装（不需要管理员），也可以在开头的对话框里改为给所有用户安装；
; - 安装时会自动卸载 1.1.0 / 1.2.0 的旧版 Velopack 安装（Setup.exe 或 MSI 装的都可以），并保留设置。

#ifndef AppVersion
  #define AppVersion "1.3.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\build\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\release"
#endif

#define AppName "数据匹配助手"
#define AppPublisher "Seul_SC_照晨"
#define AppExeName "DataMatchingAssistant.exe"
#define AppUrl "https://github.com/Seul-SC/DataMatchingAssistant"
#define AppUpdatesUrl "https://gitee.com/seul-sc/DataMatchingAssistant/releases"

[Setup]
AppId={{8B0D6D6B-3D88-4B6E-9E6C-7D3A7E46B12A}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUpdatesUrl}
AppComments=在本地匹配表格数据并将成绩写入现有 Excel 名单表。
; 默认目录：按当前用户安装时为 %LocalAppData%\Programs\数据匹配助手，给所有用户安装时为 C:\Program Files\数据匹配助手。
; 故意不用 DataMatchingAssistant 作文件夹名，避免与旧版 Velopack 的安装目录重名（旧版会被自动删除）。
DefaultDirName={autopf}\{#AppName}
DisableDirPage=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=DataMatchingAssistant-{#AppVersion}-Setup
SetupIconFile=..\StudentScoreTool\Assets\app.ico
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName}安装程序
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=*.exe,*.dll
RestartApplications=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动{#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  LegacyAppId = 'DataMatchingAssistant';
  UninstallKeyBase = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\';

var
  LegacyRoots: TArrayOfString;
  LegacyLeftovers: String;

function SameOrInside(const Child, Parent: String): Boolean;
var
  C, P: String;
begin
  Result := False;
  if (Trim(Child) = '') or (Trim(Parent) = '') then
    Exit;
  C := Lowercase(AddBackslash(Trim(Child)));
  P := Lowercase(AddBackslash(Trim(Parent)));
  Result := Pos(P, C) = 1;
end;

procedure AddLegacyRoot(Root: String);
var
  I, N: Integer;
begin
  Root := Trim(RemoveQuotes(Trim(Root)));
  if Root = '' then
    Exit;
  Root := RemoveBackslashUnlessRoot(Root);
  N := GetArrayLength(LegacyRoots);
  for I := 0 to N - 1 do
    if CompareText(LegacyRoots[I], Root) = 0 then
      Exit;
  SetArrayLength(LegacyRoots, N + 1);
  LegacyRoots[N] := Root;
end;

procedure AddRootFromRegistry(RootKey: Integer; const SubKey: String);
var
  Location: String;
begin
  if RegQueryStringValue(RootKey, SubKey, 'InstallLocation', Location) then
    AddLegacyRoot(Location);
end;

{ 找出旧版 Velopack 的安装位置：过渡包传来的参数、卸载信息里的路径，以及默认路径。 }
procedure CollectLegacyRoots;
var
  DefaultRoot: String;
begin
  SetArrayLength(LegacyRoots, 0);
  AddLegacyRoot(ExpandConstant('{param:VELOPACKROOT|}'));
  AddRootFromRegistry(HKCU, UninstallKeyBase + LegacyAppId);
  AddRootFromRegistry(HKCU, UninstallKeyBase + 'MSI:' + LegacyAppId);
  AddRootFromRegistry(HKLM64, UninstallKeyBase + 'MSI:' + LegacyAppId);
  AddRootFromRegistry(HKLM32, UninstallKeyBase + 'MSI:' + LegacyAppId);
  DefaultRoot := ExpandConstant('{localappdata}\') + LegacyAppId;
  if FileExists(DefaultRoot + '\Update.exe') then
    AddLegacyRoot(DefaultRoot);
end;

function InitializeSetup(): Boolean;
begin
  CollectLegacyRoots;
  LegacyLeftovers := '';
  Result := True;
end;

{ 不允许装进旧版的文件夹：旧版卸载时会删除整个文件夹。 }
function NextButtonClick(CurPageID: Integer): Boolean;
var
  I: Integer;
begin
  Result := True;
  if CurPageID <> wpSelectDir then
    Exit;
  for I := 0 to GetArrayLength(LegacyRoots) - 1 do
  begin
    if SameOrInside(WizardDirValue, LegacyRoots[I]) then
    begin
      SuppressibleMsgBox('这个文件夹是旧版数据匹配助手的安装位置，安装时旧版会被自动删除，所以不能装在这里。' + #13#10#13#10 +
        '旧版位置：' + LegacyRoots[I] + #13#10#13#10 +
        '请点「浏览」换一个文件夹，例如：' + ExpandConstant('{autopf}\{#AppName}'),
        mbError, MB_OK, IDOK);
      Result := False;
      Exit;
    end;
  end;
end;

{ 旧版把设置放在 %LocalAppData%\DataMatchingAssistant，新版放在 %AppData%\DataMatchingAssistant。 }
procedure MigrateSettings;
var
  OldFile, NewFile: String;
begin
  OldFile := ExpandConstant('{localappdata}\') + LegacyAppId + '\settings.json';
  NewFile := ExpandConstant('{userappdata}\') + LegacyAppId + '\settings.json';
  if FileExists(OldFile) and not FileExists(NewFile) then
  begin
    ForceDirectories(ExtractFileDir(NewFile));
    if CopyFile(OldFile, NewFile, True) then
      Log('已把旧版设置复制到 ' + NewFile)
    else
      Log('复制旧版设置失败：' + OldFile);
  end;
end;

function ExtractProductCode(const S: String): String;
var
  A, B: Integer;
begin
  Result := '';
  A := Pos('{', S);
  B := Pos('}', S);
  if (A > 0) and (B > A) then
    Result := Copy(S, A, B - A + 1);
end;

{ 1.2.0 起的 MSI 安装：必须用 msiexec 卸载。 }
procedure UninstallLegacyMsi(RootKey: Integer);
var
  Command, Code: String;
  ResultCode: Integer;
begin
  if not RegQueryStringValue(RootKey, UninstallKeyBase + 'MSI:' + LegacyAppId, 'UninstallString', Command) then
    Exit;
  Code := ExtractProductCode(Command);
  if Code = '' then
    Exit;
  Log('正在卸载旧版 MSI 安装 ' + Code);
  if Exec(ExpandConstant('{sys}\msiexec.exe'), '/x ' + Code + ' /passive /norestart', '', SW_SHOWNORMAL,
    ewWaitUntilTerminated, ResultCode) then
    Log('msiexec 退出码：' + IntToStr(ResultCode))
  else
    Log('无法启动 msiexec：' + SysErrorMessage(ResultCode));
end;

{ 1.1.0 起的 Setup.exe 安装：用旧版自带的 Update.exe 卸载。 }
procedure UninstallLegacyVelopack(const Root: String);
var
  UpdateExe: String;
  ResultCode: Integer;
begin
  UpdateExe := AddBackslash(Root) + 'Update.exe';
  if not FileExists(UpdateExe) then
    Exit;
  if FileExists(AddBackslash(Root) + '.msi-installed') then
    Exit;
  Log('正在卸载旧版 Velopack 安装 ' + Root);
  if Exec(UpdateExe, '--uninstall --silent', ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Log('Update.exe 退出码：' + IntToStr(ResultCode))
  else
    Log('无法启动 Update.exe：' + SysErrorMessage(ResultCode));
end;

procedure RemoveLegacyInstalls;
var
  I: Integer;
begin
  if GetArrayLength(LegacyRoots) = 0 then
    Exit;

  MigrateSettings;

  UninstallLegacyMsi(HKCU);
  UninstallLegacyMsi(HKLM64);
  UninstallLegacyMsi(HKLM32);

  for I := 0 to GetArrayLength(LegacyRoots) - 1 do
  begin
    if SameOrInside(ExpandConstant('{app}'), LegacyRoots[I]) then
      Continue;
    UninstallLegacyVelopack(LegacyRoots[I]);
  end;

  { 检查是否还有旧版残留，安装结束时提示用户手动卸载。 }
  for I := 0 to GetArrayLength(LegacyRoots) - 1 do
  begin
    if FileExists(AddBackslash(LegacyRoots[I]) + 'Update.exe') then
      LegacyLeftovers := LegacyLeftovers + #13#10 + LegacyRoots[I];
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { 在复制新文件、创建快捷方式之前卸载旧版，避免旧版卸载时连同新快捷方式一起删掉。 }
  if CurStep = ssInstall then
    RemoveLegacyInstalls;

  if (CurStep = ssPostInstall) and (LegacyLeftovers <> '') then
    SuppressibleMsgBox('新版本已经装好，可以正常使用。' + #13#10#13#10 +
      '不过旧版本没能自动删除，请在「设置 → 应用 → 已安装的应用」里找到旧的「数据匹配助手」手动卸载。' + #13#10 +
      '旧版位置：' + LegacyLeftovers,
      mbInformation, MB_OK, IDOK);
end;
