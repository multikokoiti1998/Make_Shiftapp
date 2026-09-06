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

; 64bitネイティブ環境としてインストール（Program Files (x86) ではなく Program Files に配置）
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64

; 権限（管理者権限必須）
PrivilegesRequired=admin

;---------------------------------
; フォルダ権限設定（DBやログを書き込む場合）
;---------------------------------
[Dirs]
Name: "{app}"; Permissions: users-modify

;---------------------------------
; ファイルコピー
;---------------------------------
[Files]
; アプリ本体
Source: "{#MyAppPublish}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

; VC++ 2015-2022 再頒布可能パッケージインストーラ（issファイルと同じ階層の redist フォルダに配置）
Source: "redist\VC_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

;---------------------------------
; ショートカット
;---------------------------------
[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成する"; GroupDescription: "追加オプション:";

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyExeName}"; Tasks: desktopicon

;---------------------------------
; インストール後・実行処理
;---------------------------------
[Run]
; 1. VC++ランタイムが入っていない場合のみサイレントインストール (/q /norestart)
Filename: "{tmp}\VC_redist.x64.exe"; Parameters: "/q /norestart"; Check: not IsVC2015To2022Installed; StatusMsg: "Microsoft Visual C++ 再頒布可能パッケージをインストールしています..."

; 2. アプリの起動（チェックボックス付き）
Filename: "{app}\{#MyExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

;---------------------------------
; アンインストール処理
;---------------------------------
[UninstallDelete]
; 実行時に生成されるログファイルなどを削除
Type: files; Name: "{app}\*.log"

;---------------------------------
; Pascal スクリプト（VC++ランタイム存在チェック）
;---------------------------------
[Code]
function IsVC2015To2022Installed: Boolean;
var
  installed: Cardinal;
begin
  Result := False;
  // x64版 Visual C++ 2015-2022 のレジストリキーを確認
  if RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', installed) then
  begin
    Result := (installed = 1);
  end;
end;