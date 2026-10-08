$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = [System.IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
$installDir = [System.IO.Path]::GetFullPath((Join-Path $artifacts 'installer-check'))
if (-not $installDir.StartsWith($artifacts + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Test destination is outside artifacts' }
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{7ED5E904-61A8-4AE1-9574-DF9787F6318B}_is1'
if (Test-Path $uninstallKey) { throw 'An existing app installation is registered. Skipping to protect it.' }
if (Test-Path $installDir) { throw 'Installer test destination already exists. Inspect it before another test.' }
$setup = Join-Path $artifacts 'ScreenshotsHanger-Setup-1.0.2-x64.exe'
$uninstaller = Join-Path $installDir 'unins000.exe'
try {
    $process = Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/NOICONS','/TASKS=""',('/DIR="' + $installDir + '"'),('/LOG="' + $artifacts + '\install-check.log"')) -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Setup exited with $($process.ExitCode)" }
    $installed = Join-Path $installDir 'ScreenshotsHanger.exe'
    $published = Join-Path $artifacts 'publish\ScreenshotsHanger.exe'
    if ((Get-FileHash $installed).Hash -ne (Get-FileHash $published).Hash) { throw 'Installed binary differs from tested binary' }
    if (-not (Test-Path $uninstaller)) { throw 'Uninstaller missing' }
    if (-not (Test-Path $uninstallKey)) { throw 'Windows uninstall registration missing' }
} finally {
    if (Test-Path $uninstaller) {
        $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + $artifacts + '\uninstall-check.log"')) -WindowStyle Hidden -PassThru -Wait
        if ($uninstall.ExitCode -ne 0) { throw "Uninstall exited with $($uninstall.ExitCode)" }
    }
}
if (Test-Path (Join-Path $installDir 'ScreenshotsHanger.exe')) { throw 'Executable remained after uninstall' }
if (Test-Path $uninstallKey) { throw 'Uninstall registration remained after uninstall' }
'PASS: silent install, installed binary SHA256 match, uninstall registration, clean uninstall.' | Set-Content (Join-Path $artifacts 'installer-test-result.txt')
Get-Content (Join-Path $artifacts 'installer-test-result.txt')
