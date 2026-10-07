param([switch]$SkipInstaller)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$dotnet = Join-Path $root '.tools\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_HOME = Join-Path $root '.tools\cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:NUGET_PACKAGES = Join-Path $root '.tools\nuget'
& "$PSScriptRoot\make-icon.ps1"
& $dotnet publish 'src\ScreenshotsHanger\ScreenshotsHanger.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None "-p:RestoreConfigFile=$root\NuGet.Config" -o 'artifacts\publish'
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
$exe = Join-Path $root 'artifacts\publish\ScreenshotsHanger.exe'
$qa = Join-Path $root 'artifacts\qa'
$test = Start-Process -FilePath $exe -ArgumentList @('--smoke-test', ('"' + $qa + '"')) -WindowStyle Hidden -PassThru -Wait
if ($test.ExitCode -ne 0) { Get-Content "$qa\result.txt"; throw 'Smoke test failed' }
Get-Content "$qa\result.txt"
if (-not $SkipInstaller) {
    $iscc = Join-Path $root '.tools\inno\ISCC.exe'
    if (-not (Test-Path $iscc)) { $iscc = (Get-Command ISCC.exe -ErrorAction Stop).Source }
    & $iscc 'installer\ScreenshotsHanger.iss'
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
}
