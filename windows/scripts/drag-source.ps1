# A tiny stand-in for "some other program" in the automated checks: a window you can drag a file out of,
# and drop things onto. What it receives is written to -ReceivedFile so that the checks can read it.
param(
    [Parameter(Mandatory)][string]$File,
    [Parameter(Mandatory)][string]$ReceivedFile
)

Add-Type -AssemblyName System.Windows.Forms

$form = New-Object System.Windows.Forms.Form
$form.Text = 'ShelfDropSource'
$form.StartPosition = 'Manual'
$form.Location = New-Object System.Drawing.Point(20, 20)
$form.Size = New-Object System.Drawing.Size(300, 200)
$form.TopMost = $true

$label = New-Object System.Windows.Forms.Label
$label.Text = 'drag me, or drop on me'
$label.Dock = 'Fill'
$label.TextAlign = 'MiddleCenter'
$label.AllowDrop = $true

$label.Add_MouseDown({
    $paths = New-Object System.Collections.Specialized.StringCollection
    [void]$paths.Add($File)
    $data = New-Object System.Windows.Forms.DataObject
    $data.SetFileDropList($paths)
    [void]$label.DoDragDrop($data, [System.Windows.Forms.DragDropEffects]::Copy)
})
$label.Add_DragEnter({ $_.Effect = [System.Windows.Forms.DragDropEffects]::Copy })
$label.Add_DragDrop({
    $lines = @()
    if ($_.Data.GetDataPresent([System.Windows.Forms.DataFormats]::FileDrop)) {
        $lines += $_.Data.GetData([System.Windows.Forms.DataFormats]::FileDrop)
    }
    if ($_.Data.GetDataPresent([System.Windows.Forms.DataFormats]::UnicodeText)) {
        $lines += 'TEXT:' + $_.Data.GetData([System.Windows.Forms.DataFormats]::UnicodeText)
    }
    Set-Content -Path $ReceivedFile -Value $lines
})

$form.Controls.Add($label)
[System.Windows.Forms.Application]::Run($form)
