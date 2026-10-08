# Starts the real ShelfDrop.exe and uses it the way a person would, with an injected mouse, then checks what happened.
# Every check runs even if an earlier one failed, so one run shows everything that is wrong.
#
#   pwsh scripts/smoke-test.ps1 -Exe publish\ShelfDrop.exe -Artifacts artifacts
#
# Needs an interactive desktop (a signed-in session), which the GitHub Windows runners have.
param(
    [Parameter(Mandatory)][string]$Exe,
    [Parameter(Mandatory)][string]$Artifacts
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\UiAutomation.ps1"

$Exe = (Resolve-Path $Exe).Path
New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null
$Artifacts = (Resolve-Path $Artifacts).Path

$settingsFile = Join-Path $env:APPDATA 'ShelfDrop\settings.json'
$logFile = Join-Path $env:LOCALAPPDATA 'ShelfDrop\shelfdrop.log'
$scratch = Join-Path $env:TEMP ("ShelfDropSmoke-" + [guid]::NewGuid())
New-Item -ItemType Directory -Force -Path $scratch | Out-Null

$results = New-Object System.Collections.Generic.List[object]

function Check([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $true; Detail = '' })
        Write-Host "  PASS  $Name"
    }
    catch {
        $results.Add([pscustomobject]@{ Name = $Name; Passed = $false; Detail = $_.Exception.Message })
        Write-Host "  FAIL  $Name -- $($_.Exception.Message)"
    }
}

function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }

function Read-Settings { if (Test-Path $settingsFile) { Get-Content $settingsFile -Raw | ConvertFrom-Json } else { $null } }

function Read-Log { if (Test-Path $logFile) { Get-Content $logFile -Raw } else { '' } }

function Start-Fresh([string[]]$Arguments = @(), [switch]$KeepSettings) {
    Stop-ShelfDrop
    Remove-Item $logFile -ErrorAction SilentlyContinue
    if (-not $KeepSettings) { Remove-Item (Split-Path $settingsFile) -Recurse -Force -ErrorAction SilentlyContinue }
    if ($Arguments.Count -gt 0) { Start-Process -FilePath $Exe -ArgumentList $Arguments -PassThru }
    else { Start-Process -FilePath $Exe -PassThru }
}

function Shelf([int]$ProcessId) { Get-VisibleWindow $ProcessId 'ShelfDrop' }

Write-Host "ShelfDrop smoke test: $Exe"

# 1. It starts, draws itself and takes its own picture.
Check 'starts, draws the demo shelf and exits after taking a picture' {
    $picture = Join-Path $Artifacts 'app-demo.png'
    Remove-Item $picture -ErrorAction SilentlyContinue
    $process = Start-Fresh -Arguments @('--demo', '--screenshot', "`"$picture`"")
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'it did not finish within a minute' }
    Copy-Item $logFile (Join-Path $Artifacts 'app-demo.log') -ErrorAction SilentlyContinue
    Assert ($process.ExitCode -eq 0) "exit code $($process.ExitCode)"
    Assert (Test-Path $picture) 'no picture was written'
    Assert ((Get-Item $picture).Length -gt 20000) "the picture is only $((Get-Item $picture).Length) bytes"
}

# 2. As a tray program: no window of its own, but a mouse hook.
$app = $null
Check 'runs quietly in the tray and installs its mouse hook' {
    $script:app = Start-Fresh
    Start-Sleep -Seconds 4
    Assert (-not $script:app.HasExited) "it exited with code $($script:app.ExitCode)"
    Assert ((Read-Log) -match 'mouse hook installed') 'the log does not say the mouse hook was installed'
    Assert ($null -eq (Shelf $script:app.Id)) 'the shelf is visible before anyone asked for it'
}

# 3. Shaking while dragging opens the shelf, and an empty shelf closes again.
Check 'a shake while dragging opens the shelf, and an empty one closes after the drag' {
    Assert ($null -ne $script:app -and -not $script:app.HasExited) 'the app is not running'
    $cx = 700; $cy = 450
    [Win]::MoveTo($cx, $cy); Start-Sleep -Milliseconds 200
    [Win]::Press($cx, $cy); Start-Sleep -Milliseconds 150
    foreach ($dx in 140, 0, 140, 0, 140, 0, 140, 0) {
        [Win]::MoveTo($cx + $dx, $cy)
        Start-Sleep -Milliseconds 45
    }
    $window = Wait-Until { Shelf $script:app.Id } 4
    Save-Screen (Join-Path $Artifacts 'desktop-after-shake.png')
    Assert ($null -ne $window) "the shelf did not appear. Log: $(Read-Log)"
    [Win]::Release($cx, $cy)
    $closed = Wait-Until { $null -eq (Shelf $script:app.Id) } 4
    Assert $closed 'the empty shelf stayed open after the drag ended'
}

# 4. Open it on purpose, then move it: the new place is remembered.
Check 'dragging the shelf by its background moves it and remembers where it went' {
    $process = Start-Fresh -Arguments @('--show-shelf')
    $window = Wait-Until { Shelf $process.Id } 8
    Assert ($null -ne $window) 'the shelf did not open'
    Start-Sleep -Milliseconds 500
    $x = $window.Left + 40; $y = $window.Top + 40
    [Win]::Press($x, $y); Start-Sleep -Milliseconds 200
    Move-Smoothly $x $y ($x + 150) ($y + 90) 15 30
    [Win]::Release($x + 150, $y + 90)
    Start-Sleep -Seconds 1
    $moved = Shelf $process.Id
    Assert ($null -ne $moved) 'the shelf disappeared'
    Assert ([math]::Abs($moved.Left - ($window.Left + 150)) -le 4 -and [math]::Abs($moved.Top - ($window.Top + 90)) -le 4) `
        "it moved from ($($window.Left),$($window.Top)) to ($($moved.Left),$($moved.Top)) instead of by (150,90)"
    $settings = Read-Settings
    Assert ($null -ne $settings -and $null -ne $settings.shelfTopLeftX) "no position was saved. Log: $(Read-Log)"
    Assert ([math]::Abs($settings.shelfTopLeftX - $moved.Left) -le 2 -and [math]::Abs($settings.shelfTopLeftY - $moved.Top) -le 2) `
        "saved ($($settings.shelfTopLeftX),$($settings.shelfTopLeftY)) but the shelf is at ($($moved.Left),$($moved.Top))"
    $script:movedRect = $moved
}

# 5. Resize with the grip: the new size is remembered.
Check 'dragging the grip resizes the shelf and remembers the size' {
    $process = Get-Process -Name ShelfDrop -ErrorAction Stop | Select-Object -First 1
    $window = Shelf $process.Id
    Assert ($null -ne $window) 'the shelf is not open'
    # The grip sits in the bottom-right corner of the panel: 12 of shadow, 4 of margin, half of its 18 size.
    $x = $window.Left + $window.Width - 25; $y = $window.Top + $window.Height - 25
    [Win]::Press($x, $y); Start-Sleep -Milliseconds 200
    Move-Smoothly $x $y ($x + 120) ($y + 70) 12 30
    [Win]::Release($x + 120, $y + 70)
    Start-Sleep -Seconds 1
    $resized = Shelf $process.Id
    Assert ($resized.Width -ge $window.Width + 100 -and $resized.Height -ge $window.Height + 55) `
        "it went from $($window.Width)x$($window.Height) to $($resized.Width)x$($resized.Height)"
    $settings = Read-Settings
    Assert ($null -ne $settings.shelfWidth) 'no size was saved'
    Assert ([math]::Abs($settings.shelfWidth - $resized.Width) -le 3 -and [math]::Abs($settings.shelfHeight - $resized.Height) -le 3) `
        "saved $($settings.shelfWidth)x$($settings.shelfHeight) but the shelf is $($resized.Width)x$($resized.Height)"
    $script:resizedRect = $resized
}

# 6. A new run opens where it was left, at the size it was left.
Check 'a new run opens at the remembered place and size' {
    $process = Start-Fresh -Arguments @('--show-shelf') -KeepSettings
    $window = Wait-Until { Shelf $process.Id } 8
    Assert ($null -ne $window) 'the shelf did not open'
    Assert ([math]::Abs($window.Left - $script:movedRect.Left) -le 4 -and [math]::Abs($window.Top - $script:movedRect.Top) -le 4) `
        "opened at ($($window.Left),$($window.Top)), expected ($($script:movedRect.Left),$($script:movedRect.Top))"
    Assert ([math]::Abs($window.Width - $script:resizedRect.Width) -le 4 -and [math]::Abs($window.Height - $script:resizedRect.Height) -le 4) `
        "opened at $($window.Width)x$($window.Height), expected $($script:resizedRect.Width)x$($script:resizedRect.Height)"
}

# 7. Dropping a file from another program onto the shelf adds it; dragging it out hands the file to the other program.
$sourceFile = Join-Path $scratch 'report to share.txt'
Set-Content -Path $sourceFile -Value 'hello from the smoke test'
$receivedFile = Join-Path $scratch 'received.txt'
$selfTestFile = Join-Path $scratch 'selftest-received.txt'

$helper = $null
Check 'control: injected mouse input can drag a file between two ordinary windows' {
    Stop-ShelfDrop
    $script:helper = Start-Process -FilePath (Get-Process -Id $PID).Path -PassThru -ArgumentList @(
        '-NoProfile', '-STA', '-File', (Join-Path $PSScriptRoot 'drag-source.ps1'),
        '-File', "`"$sourceFile`"", '-ReceivedFile', "`"$receivedFile`"", '-SelfTestFile', "`"$selfTestFile`"")
    $source = Wait-Until { Get-VisibleWindow $script:helper.Id 'ShelfDropSource' } 20
    $target = Wait-Until { Get-VisibleWindow $script:helper.Id 'ShelfDropTarget' } 20
    Assert ($null -ne $source -and $null -ne $target) 'the stand-in windows did not open'
    Start-Sleep -Milliseconds 500

    $fromX = $source.Left + 150; $fromY = $source.Top + 100
    $toX = $target.Left + 150; $toY = $target.Top + 100
    [Win]::Press($fromX, $fromY); Start-Sleep -Milliseconds 200
    Move-Smoothly $fromX $fromY ($fromX + 30) ($fromY + 30) 4 40
    Move-Smoothly ($fromX + 30) ($fromY + 30) $toX $toY 25 40
    Start-Sleep -Milliseconds 300
    [Win]::Release($toX, $toY)
    $got = Wait-Until { Test-Path $selfTestFile } 5
    Copy-Item "$receivedFile.log" (Join-Path $Artifacts 'stand-in-windows.log') -ErrorAction SilentlyContinue
    Assert ([bool]$got) "nothing arrived, so this harness cannot test drag and drop. $(Get-Content "$receivedFile.log" -Raw -ErrorAction SilentlyContinue)"
}

Check 'a file dragged in from another program lands on the shelf' {
    $source = Get-VisibleWindow $script:helper.Id 'ShelfDropSource'
    Assert ($null -ne $source) 'the stand-in program is gone'

    $process = Start-Fresh -Arguments @('--show-shelf')
    $shelf = Wait-Until { Shelf $process.Id } 8
    Assert ($null -ne $shelf) 'the shelf did not open'
    Start-Sleep -Milliseconds 800

    $fromX = $source.Left + 150; $fromY = $source.Top + 100
    $toX = $shelf.Left + [int]($shelf.Width / 2); $toY = $shelf.Top + [int]($shelf.Height / 2)
    [Win]::Press($fromX, $fromY); Start-Sleep -Milliseconds 200
    Move-Smoothly $fromX $fromY ($fromX + 30) ($fromY + 30) 4 40
    Move-Smoothly ($fromX + 30) ($fromY + 30) $toX $toY 25 40
    Start-Sleep -Milliseconds 400
    Save-Screen (Join-Path $Artifacts 'desktop-during-drop.png')
    [Win]::Release($toX, $toY)
    $dropped = Wait-Until { (Read-Log) -match 'dropped: 1 item' } 5
    Start-Sleep -Milliseconds 1500
    Save-Screen (Join-Path $Artifacts 'desktop-after-drop.png')
    Copy-Item "$receivedFile.log" (Join-Path $Artifacts 'stand-in-windows.log') -ErrorAction SilentlyContinue
    Copy-Item $logFile (Join-Path $Artifacts 'drop-test.log') -ErrorAction SilentlyContinue
    Assert ([bool]$dropped) "the shelf did not report a drop. App log: $(Read-Log)"
    Assert ((Read-Log) -notmatch 'ERROR') "errors in the log: $(Read-Log)"
}

Check 'an item dragged out of the shelf arrives at the other program' {
    $process = Get-Process -Name ShelfDrop -ErrorAction Stop | Select-Object -First 1
    $shelf = Shelf $process.Id
    Assert ($null -ne $shelf) 'the shelf is not open'
    $source = Get-VisibleWindow $script:helper.Id 'ShelfDropSource'
    Assert ($null -ne $source) 'the stand-in program is gone'
    Remove-Item $receivedFile -ErrorAction SilentlyContinue

    # The first tile: inside the panel (12 of shadow, 14 of padding), below the header.
    $fromX = $shelf.Left + 12 + 14 + 50; $fromY = $shelf.Top + 12 + 14 + 34 + 10 + 40
    $toX = $source.Left + 150; $toY = $source.Top + 100
    [Win]::Press($fromX, $fromY); Start-Sleep -Milliseconds 200
    Move-Smoothly $fromX $fromY ($fromX + 30) ($fromY - 10) 4 40
    Move-Smoothly ($fromX + 30) ($fromY - 10) $toX $toY 25 40
    Start-Sleep -Milliseconds 300
    [Win]::Release($toX, $toY)
    $got = Wait-Until { Test-Path $receivedFile } 5
    Save-Screen (Join-Path $Artifacts 'desktop-after-drag-out.png')
    Assert $got "nothing arrived. Log: $(Read-Log)"
    $received = (Get-Content $receivedFile -Raw).Trim()
    Assert ($received -eq $sourceFile) "received '$received' instead of '$sourceFile'"
    Assert (Test-Path $sourceFile) 'the original file is gone: the drag must copy, not move'
}

Stop-ShelfDrop
if ($helper) { Stop-Process -Id $helper.Id -Force -ErrorAction SilentlyContinue }
Copy-Item $logFile (Join-Path $Artifacts 'smoke-test.log') -ErrorAction SilentlyContinue
Remove-Item $scratch -Recurse -Force -ErrorAction SilentlyContinue

$failed = @($results | Where-Object { -not $_.Passed })
Write-Host ''
Write-Host ("{0} of {1} checks passed" -f ($results.Count - $failed.Count), $results.Count)
$results | ForEach-Object { Write-Host ("  {0}  {1}" -f $(if ($_.Passed) { 'PASS' } else { 'FAIL' }), $_.Name) }
if ($failed.Count -gt 0) { exit 1 }
