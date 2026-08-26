param(
    [string]$Configuration = "Release",
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if (-not $OutDir) { $OutDir = Join-Path $root "artifacts\win-x64" }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Get-ChildItem $OutDir -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

$common = @(
    "-c", $Configuration,
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishReadyToRun=true",
    "-p:DebugType=none",
    "-p:DebugSymbols=false",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-o", $OutDir
)

dotnet publish (Join-Path $root "src\SDM.App\SDM.App.csproj") @common
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish (Join-Path $root "src\SDM.NativeHost\SDM.NativeHost.csproj") @common
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-ChildItem $OutDir -Include *.pdb,*.xml -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "Published to $OutDir"
