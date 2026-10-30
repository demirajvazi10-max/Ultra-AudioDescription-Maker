; Inno Setup script for Ultra Audio Description Maker (Inno Setup 6.2 or newer).
;
; 1. Publish the program into the "publish" folder next to the project file:
;    in Visual Studio, Build > Publish, profile "Installer" (Properties\PublishProfiles\Installer.pubxml),
;    or: dotnet publish -c Release -r win-x64 --self-contained true -o publish
; 2. Open this file in Inno Setup and press Compile (Ctrl+F9).
;    The setup file appears in the "installer-output" folder.
; The GitHub workflow does both steps by itself for every "v" tag.
;
; The program is self-contained (no .NET install needed). FFmpeg is not inside: the program
; downloads it by itself on the first start.

#define AppName "Ultra Audio Description Maker"
#define AppExe "UltraAudioDescriptionMaker.exe"
#define SourceDir "..\publish"
#define AppVersion GetVersionNumbersString(SourceDir + "\" + AppExe)

[Setup]
AppId={{EE214937-D022-41EB-9285-199B24522EDA}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Ultra Creative Suite
AppPublisherURL=https://github.com/demirajvazi10-max
AppSupportURL=https://github.com/demirajvazi10-max
AppCopyright=Copyright (c) 2026 Demir Ajvazi
VersionInfoCompany=Demir Ajvazi
VersionInfoDescription={#AppName} Setup
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName=Ultra Creative Suite
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\installer-output
OutputBaseFilename=UltraAudioDescriptionMaker-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; installs without administrator rights (for this user); "for all users" can still be chosen
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ChangesAssociations=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
; Serbian (Latin) is not shipped with Inno Setup, so its translation file is kept next to this script
Name: "serbian"; MessagesFile: "SerbianLatin.isl"

[CustomMessages]
english.DesktopIcon=Create a desktop shortcut
serbian.DesktopIcon=Napravi prečicu na radnoj površini
english.Associate=Open audio description projects (.uad) with {#AppName}
serbian.Associate=Otvaraj projekte audio deskripcije (.uad) ovim programom
english.Launch=Start {#AppName}
serbian.Launch=Pokreni {#AppName}
english.ProjectType=Audio description project
serbian.ProjectType=Projekat audio deskripcije
english.Sponsors=Support the project (GitHub Sponsors)
serbian.Sponsors=Podrži projekat (GitHub Sponsors)
english.RemoveData=Also delete the settings, saved keys, the voice cache and the downloaded FFmpeg?%n%nChoose No to keep them for a later installation.
serbian.RemoveData=Da obrišem i podešavanja, sačuvane ključeve, keš glasova i preuzeti FFmpeg?%n%nIzaberi Ne da ostanu za neku sledeću instalaciju.
english.FfmpegNote=On the first start the program downloads FFmpeg (about 100 MB), which it needs for video and sound.
serbian.FfmpegNote=Pri prvom pokretanju program preuzima FFmpeg (oko 100 MB), koji mu treba za video i zvuk.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "associate"; Description: "{cm:Associate}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:Sponsors}"; Filename: "https://github.com/sponsors/demirajvazi10-max"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Classes\.uad"; ValueType: string; ValueName: ""; ValueData: "UltraAudioDescriptionMaker.Project"; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Classes\UltraAudioDescriptionMaker.Project"; ValueType: string; ValueName: ""; ValueData: "{cm:ProjectType}"; Flags: uninsdeletekey; Tasks: associate
Root: HKA; Subkey: "Software\Classes\UltraAudioDescriptionMaker.Project\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExe},0"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\UltraAudioDescriptionMaker.Project\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: associate

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent

[Code]
{ The program language follows the setup language, the first time only:
  a Serbian setup writes a settings file with Serbian chosen. An existing settings file is never changed. }
procedure CurStepChanged(CurStep: TSetupStep);
var
  Dir, Settings: String;
begin
  if CurStep = ssPostInstall then
  begin
    Dir := ExpandConstant('{localappdata}\UltraAudioDescriptionMaker');
    Settings := Dir + '\settings.json';
    if (ActiveLanguage = 'serbian') and not FileExists(Settings) then
    begin
      ForceDirectories(Dir);
      SaveStringToFile(Settings, '{ "UiLanguage": "sr", "DescLanguage": "sr" }', False);
    end;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
    WizardForm.FinishedLabel.Caption := WizardForm.FinishedLabel.Caption + #13#10#13#10 + CustomMessage('FfmpegNote');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
    if MsgBox(CustomMessage('RemoveData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(ExpandConstant('{localappdata}\UltraAudioDescriptionMaker'), True, True, True);
end;
