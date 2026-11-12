$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Src = Join-Path $Root "src\Program.cs"
$Icon = Join-Path $Root "src\AdbQuickTool.ico"
$Bin = Join-Path $Root "bin"
$Out = Join-Path $Bin "AdbQuickTool.exe"
$Csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (!(Test-Path -LiteralPath $Csc)) {
    throw "Cannot find csc.exe: $Csc"
}

if (!(Test-Path -LiteralPath $Icon)) {
    throw "Cannot find icon file: $Icon"
}

New-Item -ItemType Directory -Force -Path $Bin | Out-Null

& $Csc `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    "/win32icon:$Icon" `
    /out:$Out `
    /r:System.dll `
    /r:System.Core.dll `
    /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll `
    /r:System.Web.Extensions.dll `
    $Src

if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed with exit code $LASTEXITCODE"
}

Write-Host "Built: $Out"
