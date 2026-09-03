#Requires -Version 5.1
<#
.SYNOPSIS
  Removes Better Copilot Button for the current user and restores the Copilot key.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$destDir = Join-Path $env:LOCALAPPDATA "BetterCopilotButton"
$exe = Join-Path $destDir "BetterCopilotButton.exe"
$settings = Join-Path $env:APPDATA "BetterCopilotButton"
$shortcut = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs\Better Copilot Button.lnk"

Get-Process -Name "BetterCopilotButton" -ErrorAction SilentlyContinue | Stop-Process -Force

Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "BetterCopilotButton" -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BetterCopilotButton" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $shortcut -Force -ErrorAction SilentlyContinue
Remove-Item $settings -Recurse -Force -ErrorAction SilentlyContinue

if (Test-Path $destDir) {
    Start-Sleep -Seconds 1
    Remove-Item $destDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Better Copilot Button was removed. The Copilot key uses Windows default behavior again."
