;---------------------------------
; 基本情報
;---------------------------------
#define MyAppName "ShiftApp"
#define MyAppVersion "1.0.0"
#define MyPublisher ""
#define MyExeName "Shiftapp_demo.exe"
#define MyAppPublish ".\bin\Release\net8.0-windows"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyPublisher}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputBaseFilename=ShiftAppInstaller
Compression=lzma
SolidCompression=yes
DisableProgramGroupPage=yes

; 権限（管理者権限必須：Program Files 配下にインストールするため）
PrivilegesRequired=admin

;---------------------------------
; ファイルコピー
;---------------------------------
[Files]
Source: "{#MyAppPublish}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

;---------------------------------
; ショートカット
;---------------------------------
[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成する"; GroupDescription: "追加オプション:";

;---------------------------------
; インストール後に起動（省略可）
;---------------------------------
[Run]
Filename: "{app}\{#MyExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

;---------------------------------
; アンインストール時に残すもの（例：DB）
;---------------------------------
[UninstallDelete]
;Log だけ消す例（DBは残す）
Type: files; Name: "{app}\*.log"
