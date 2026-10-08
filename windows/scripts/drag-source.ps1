# Two ordinary windows that stand in for "some other program" in the automated checks:
#   ShelfDropSource  - you can drag a file out of it, and drop things onto it. What it receives goes to -ReceivedFile.
#   ShelfDropTarget  - only accepts drops. What it receives goes to -SelfTestFile. Dragging from the first to this one shows
#                      whether injected mouse input can drive a drag and drop at all, whatever ShelfDrop does.
# A line for everything that happens goes to <ReceivedFile>.log.
param(
    [Parameter(Mandatory)][string]$File,
    [Parameter(Mandatory)][string]$ReceivedFile,
    [Parameter(Mandatory)][string]$SelfTestFile
)

Add-Type -AssemblyName System.Windows.Forms
$diagnostics = "$ReceivedFile.log"
function Note([string]$Text) { Add-Content -Path $diagnostics -Value ("{0:HH:mm:ss.fff} {1}" -f (Get-Date), $Text) }

function New-Window([string]$Title, [int]$X, [int]$Y, [string]$Caption) {
    $form = New-Object System.Windows.Forms.Form
    $form.Text = $Title
    $form.StartPosition = 'Manual'
    $form.Location = New-Object System.Drawing.Point($X, $Y)
    $form.Size = New-Object System.Drawing.Size(300, 200)
    $form.TopMost = $true
    $label = New-Object System.Windows.Forms.Label
    $label.Text = $Caption
    $label.Dock = 'Fill'
    $label.TextAlign = 'MiddleCenter'
    $label.AllowDrop = $true
    $form.Controls.Add($label)
    return @{ Form = $form; Label = $label }
}

function Receive($EventArgs, [string]$Where, [string]$Name) {
    $lines = @()
    if ($EventArgs.Data.GetDataPresent([System.Windows.Forms.DataFormats]::FileDrop)) {
        $lines += $EventArgs.Data.GetData([System.Windows.Forms.DataFormats]::FileDrop)
    }
    if ($EventArgs.Data.GetDataPresent([System.Windows.Forms.DataFormats]::UnicodeText)) {
        $lines += 'TEXT:' + $EventArgs.Data.GetData([System.Windows.Forms.DataFormats]::UnicodeText)
    }
    Set-Content -Path $Where -Value $lines
    Note "$Name received: $($lines -join ' | ')"
}

$source = New-Window 'ShelfDropSource' 20 20 'drag me, or drop on me'
$target = New-Window 'ShelfDropTarget' 340 20 'drop here'

$source.Label.Add_MouseDown({
    try {
        $paths = New-Object System.Collections.Specialized.StringCollection
        [void]$paths.Add($File)
        $data = New-Object System.Windows.Forms.DataObject
        $data.SetFileDropList($paths)
        Note "mouse down: starting the drag of $File"
        $effect = $source.Label.DoDragDrop($data, [System.Windows.Forms.DragDropEffects]::Copy)
        Note "drag finished with effect: $effect"
    }
    catch { Note "drag failed: $($_.Exception.Message)" }
})
$source.Label.Add_DragEnter({ $_.Effect = [System.Windows.Forms.DragDropEffects]::Copy })
$source.Label.Add_DragDrop({ Receive $_ $ReceivedFile 'source' })

$target.Label.Add_DragEnter({ Note 'target: drag entered'; $_.Effect = [System.Windows.Forms.DragDropEffects]::Copy })
$target.Label.Add_DragDrop({ Receive $_ $SelfTestFile 'target' })

Note 'ready'
$target.Form.Show()
[System.Windows.Forms.Application]::Run($source.Form)
