# Helpers for driving ShelfDrop the way a person does: with a real (injected) mouse, and by looking at the screen.
# Dot-source this file:  . "$PSScriptRoot\UiAutomation.ps1"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class Win
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public MOUSEINPUT mi; }

    delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    const uint InputMouse = 0, Move = 0x0001, LeftDown = 0x0002, LeftUp = 0x0004, Absolute = 0x8000;

    public static void MakeDpiAware() { SetProcessDPIAware(); }

    static void Send(uint flags, int x, int y)
    {
        int width = GetSystemMetrics(0), height = GetSystemMetrics(1);
        var input = new INPUT { type = InputMouse };
        input.mi.dx = (int)((long)x * 65535 / Math.Max(1, width - 1));
        input.mi.dy = (int)((long)y * 65535 / Math.Max(1, height - 1));
        input.mi.dwFlags = flags;
        if (SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT))) != 1) throw new Exception("SendInput failed");
    }

    public static void MoveTo(int x, int y) { Send(Move | Absolute, x, y); }
    public static void Press(int x, int y) { Send(Move | Absolute, x, y); Send(LeftDown | Absolute, x, y); }
    public static void Release(int x, int y) { Send(Move | Absolute, x, y); Send(LeftUp | Absolute, x, y); }

    /// <summary>Visible, titled top-level windows of one process: "rect;title".</summary>
    public static List<string> WindowsOf(uint pid)
    {
        var found = new List<string>();
        EnumWindows((hwnd, l) =>
        {
            uint owner; GetWindowThreadProcessId(hwnd, out owner);
            if (owner != pid || !IsWindowVisible(hwnd)) return true;
            RECT r; GetWindowRect(hwnd, out r);
            var text = new StringBuilder(256); GetWindowText(hwnd, text, 256);
            found.Add(r.Left + "," + r.Top + "," + (r.Right - r.Left) + "," + (r.Bottom - r.Top) + ";" + text);
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@

[Win]::MakeDpiAware()

function Get-VisibleWindow([int]$ProcessId, [string]$Title) {
    # Returns @{Left;Top;Width;Height} of the visible window of the process with this title, or $null.
    foreach ($entry in [Win]::WindowsOf([uint32]$ProcessId)) {
        $rect, $text = $entry -split ';', 2
        if ($text -eq $Title) {
            $n = $rect -split ',' | ForEach-Object { [int]$_ }
            return [pscustomobject]@{ Left = $n[0]; Top = $n[1]; Width = $n[2]; Height = $n[3] }
        }
    }
    return $null
}

function Wait-Until([scriptblock]$Condition, [double]$Seconds = 5, [int]$PollMs = 100) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $value = & $Condition
        if ($value) { return $value }
        Start-Sleep -Milliseconds $PollMs
    }
    return $null
}

function Move-Smoothly([int]$FromX, [int]$FromY, [int]$ToX, [int]$ToY, [int]$Steps = 12, [int]$PauseMs = 25) {
    for ($i = 1; $i -le $Steps; $i++) {
        [Win]::MoveTo([int]($FromX + ($ToX - $FromX) * $i / $Steps), [int]($FromY + ($ToY - $FromY) * $i / $Steps))
        Start-Sleep -Milliseconds $PauseMs
    }
}

function Save-Screen([string]$Path) {
    $bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($bounds.Left, $bounds.Top, 0, 0, $bitmap.Size)
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

function Stop-ShelfDrop {
    Get-Process -Name ShelfDrop -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}
