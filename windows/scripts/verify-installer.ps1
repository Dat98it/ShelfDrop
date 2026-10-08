# Installs ShelfDrop with its setup program, checks what it put where, runs the installed copy, uninstalls it and
# checks that nothing is left. Everything is silent and per-user, so it needs no administrator rights.
#
#   pwsh scripts/verify-installer.ps1 -Installer dist\ShelfDrop-Setup-0.1.0.exe -Artifacts artifacts
param(
    [Parameter(Mandatory)][string]$Installer,
    [Parameter(Mandatory)][string]$Artifacts
)

$ErrorActionPreference = 'Stop'
$Installer = (Resolve-Path $Installer).Path
New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null
$Artifacts = (Resolve-Path $Artifacts).Path

$target = Join-Path $env:TEMP ("ShelfDropInstallCheck-" + [guid]::NewGuid())
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$settingsFolder = Join-Path $env:APPDATA 'ShelfDrop'
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\ShelfDrop.lnk'
$results = New-Object System.Collections.Generic.List[object]

function Check([string]$Name, [scriptblock]$Body) {
    try { & $Body; $results.Add([pscustomobject]@{ Name = $Name; Passed = $true; Detail = '' }); Write-Host "  PASS  $Name" }
    catch { $results.Add([pscustomobject]@{ Name = $Name; Passed = $false; Detail = $_.Exception.Message }); Write-Host "  FAIL  $Name -- $($_.Exception.Message)" }
}
function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Run-Setup([string[]]$Arguments) {
    $process = Start-Process -FilePath $Installer -ArgumentList $Arguments -PassThru -Wait
    return $process.ExitCode
}

Write-Host "Installer check: $Installer"
Get-Process -Name ShelfDrop -ErrorAction SilentlyContinue | Stop-Process -Force

Check 'installs silently without administrator rights' {
    $code = Run-Setup @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', "/DIR=`"$target`"", "/LOG=`"$Artifacts\setup.log`"", '/TASKS=""')
    Assert ($code -eq 0) "setup exited with code $code (see setup.log)"
    Assert (Test-Path (Join-Path $target 'ShelfDrop.exe')) 'ShelfDrop.exe was not installed'
    Assert (Test-Path (Join-Path $target 'unins000.exe')) 'the uninstaller was not installed: the app''s Uninstall menu item needs it'
    Assert (Test-Path (Join-Path $target 'LICENSE')) 'the license was not installed'
}

Check 'adds a Start menu entry and a Settings -> Apps entry' {
    Assert (Test-Path $startMenu) 'no Start menu shortcut'
    $uninstallEntries = Get-ChildItem 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall' | Where-Object { $_.PSChildName -like '{6B2D6E52-1D0C-4F2A-9B53-3C1F0A7D8E64}*' }
    Assert ($uninstallEntries.Count -eq 1) 'no entry in Settings -> Apps'
    Assert ($uninstallEntries[0].GetValue('DisplayName') -eq 'ShelfDrop') 'the entry has the wrong name'
}

Check 'does not start at login unless asked' {
    Assert ($null -eq (Get-ItemProperty -Path $runKey -Name 'ShelfDrop' -ErrorAction SilentlyContinue)) 'a startup entry was created without being asked'
}

Check 'the installed copy runs and draws its demo shelf' {
    $picture = Join-Path $Artifacts 'installed-demo.png'
    Remove-Item $picture -ErrorAction SilentlyContinue
    $process = Start-Process -FilePath (Join-Path $target 'ShelfDrop.exe') -ArgumentList '--demo', '--screenshot', "`"$picture`"" -PassThru
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'it did not finish within a minute' }
    Assert ($process.ExitCode -eq 0) "exit code $($process.ExitCode)"
    Assert ((Test-Path $picture) -and (Get-Item $picture).Length -gt 20000) 'no usable picture was written'
}

Check 'the settings folder is created by the app and removed by the uninstaller' {
    Get-Process -Name ShelfDrop -ErrorAction SilentlyContinue | Stop-Process -Force
    New-Item -ItemType Directory -Force -Path $settingsFolder | Out-Null
    Set-Content -Path (Join-Path $settingsFolder 'settings.json') -Value '{"shelfWidth": 400}'

    $code = Start-Process -FilePath (Join-Path $target 'unins000.exe') -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru -Wait | Select-Object -ExpandProperty ExitCode
    Assert ($code -eq 0) "the uninstaller exited with code $code"

    $gone = $false
    foreach ($attempt in 1..30) {
        if (-not (Test-Path (Join-Path $target 'ShelfDrop.exe'))) { $gone = $true; break }
        Start-Sleep -Milliseconds 500
    }
    Assert $gone 'ShelfDrop.exe is still there after uninstalling'
    Assert (-not (Test-Path $settingsFolder)) 'the settings were left behind'
    Assert (-not (Test-Path $startMenu)) 'the Start menu shortcut was left behind'
    $uninstallEntries = Get-ChildItem 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall' | Where-Object { $_.PSChildName -like '{6B2D6E52-1D0C-4F2A-9B53-3C1F0A7D8E64}*' }
    Assert ($uninstallEntries.Count -eq 0) 'the Settings -> Apps entry was left behind'
}

Check 'with "start at login" ticked it sets up the startup entry, and uninstalling removes it' {
    $code = Run-Setup @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', "/DIR=`"$target`"", '/TASKS="startup"')
    Assert ($code -eq 0) "setup exited with code $code"
    $entry = (Get-ItemProperty -Path $runKey -Name 'ShelfDrop' -ErrorAction SilentlyContinue).ShelfDrop
    Assert ($entry -eq "`"$target\ShelfDrop.exe`"") "the startup entry is '$entry'"

    $null = Start-Process -FilePath (Join-Path $target 'unins000.exe') -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru -Wait
    foreach ($attempt in 1..30) { if (-not (Test-Path (Join-Path $target 'ShelfDrop.exe'))) { break }; Start-Sleep -Milliseconds 500 }
    Assert ($null -eq (Get-ItemProperty -Path $runKey -Name 'ShelfDrop' -ErrorAction SilentlyContinue)) 'the startup entry was left behind'
}

Check 'a startup entry made by the app itself is removed by the uninstaller too' {
    $code = Run-Setup @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', "/DIR=`"$target`"", '/TASKS=""')
    Assert ($code -eq 0) "setup exited with code $code"
    Set-ItemProperty -Path $runKey -Name 'ShelfDrop' -Value "`"$target\ShelfDrop.exe`""

    $null = Start-Process -FilePath (Join-Path $target 'unins000.exe') -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru -Wait
    foreach ($attempt in 1..30) { if (-not (Test-Path (Join-Path $target 'ShelfDrop.exe'))) { break }; Start-Sleep -Milliseconds 500 }
    Assert ($null -eq (Get-ItemProperty -Path $runKey -Name 'ShelfDrop' -ErrorAction SilentlyContinue)) 'the startup entry was left behind'
}

Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
$failed = @($results | Where-Object { -not $_.Passed })
Write-Host ''
Write-Host ("{0} of {1} checks passed" -f ($results.Count - $failed.Count), $results.Count)
if ($failed.Count -gt 0) { exit 1 }
