# Registers SDM Native Messaging host for Chrome / Edge / Brave / Firefox.
# Run after building, or from the app: 브라우저 연결 → 지금 등록.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exe = Get-ChildItem -Path $root -Recurse -Filter SDM.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\bin\\' } |
    Select-Object -First 1
if (-not $exe) {
    Write-Host "먼저 솔루션을 빌드하세요:  dotnet build `"$root\SDM.slnx`""
    exit 1
}
Start-Process $exe.FullName
Write-Host "SDM을 실행한 뒤 앱에서 '브라우저 연결'을 누르세요."
Write-Host "실행 파일: $($exe.FullName)"
