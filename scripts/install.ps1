#Requires -Version 5.1
<#
.SYNOPSIS
  Installs Better Copilot Button for the current user. No admin required.
#>
[CmdletBinding()]
param(
    [switch]$StartAfterInstall
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$exeName = "BetterCopilotButton.exe"
$source = Join-Path $here $exeName

if (-not (Test-Path $source)) {
    throw "Could not find $exeName next to this script. Unzip the release first."
}

$destDir = Join-Path $env:LOCALAPPDATA "BetterCopilotButton"
$destExe = Join-Path $destDir $exeName
New-Item -ItemType Directory -Force -Path $destDir | Out-Null
Copy-Item $source $destExe -Force

$programs = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs"
New-Item -ItemType Directory -Force -Path $programs | Out-Null
$shortcutPath = Join-Path $programs "Better Copilot Button.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $destExe
$shortcut.WorkingDirectory = $destDir
$shortcut.WindowStyle = 1
$shortcut.Description = "Replace the Copilot key with any app you choose."
$shortcut.IconLocation = "$destExe,0"
$shortcut.Save()

$uninstall = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BetterCopilotButton"
New-Item -Path $uninstall -Force | Out-Null
New-ItemProperty -Path $uninstall -Name "DisplayName" -Value "Better Copilot Button" -Force | Out-Null
New-ItemProperty -Path $uninstall -Name "Publisher" -Value "Better Copilot Button" -Force | Out-Null
New-ItemProperty -Path $uninstall -Name "InstallLocation" -Value $destDir -Force | Out-Null
New-ItemProperty -Path $uninstall -Name "DisplayIcon" -Value $destExe -Force | Out-Null
New-ItemProperty -Path $uninstall -Name "UninstallString" -Value "`"$destExe`" --uninstall" -Force | Out-Null
New-ItemProperty -Path $uninstall -Name "NoModify" -PropertyType DWord -Value 1 -Force | Out-Null
New-ItemProperty -Path $uninstall -Name "NoRepair" -PropertyType DWord -Value 1 -Force | Out-Null

Write-Host "Installed for this user:"
Write-Host "  $destExe"
Write-Host "Start menu: Better Copilot Button"
Write-Host "Open the app, pick a target (Calculator is the default), then enable remap."

if ($StartAfterInstall) {
    Start-Process $destExe
}
