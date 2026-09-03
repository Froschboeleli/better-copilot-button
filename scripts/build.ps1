#Requires -Version 5.1
<#
.SYNOPSIS
  Publishes a self-contained Better Copilot Button zip for Windows x64.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputRoot) {
    $OutputRoot = Join-Path $repoRoot "artifacts"
}

$project = Join-Path $repoRoot "src\BetterCopilotButton\BetterCopilotButton.csproj"
$publishDir = Join-Path $OutputRoot "portable"
$zipPath = Join-Path $OutputRoot "BetterCopilotButton-win-x64.zip"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK is not installed. Install .NET 8 from https://dotnet.microsoft.com/download"
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}

dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=embedded `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

Copy-Item (Join-Path $repoRoot "scripts\install.ps1") $publishDir -Force
Copy-Item (Join-Path $repoRoot "scripts\uninstall.ps1") $publishDir -Force

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
Write-Host "Published: $zipPath"
