# Runs an AutoPilot mode on a virtual desktop the user is not on, without taking focus.
# Godot is started through WMI so it is not a child of the foreground app (Windows lets those take focus), starts
# minimized with no_focus set, is moved to another virtual desktop, and is then shown with SW_SHOWNOACTIVATE.
# Usage: powershell -File tools/autopilot.ps1 -Mode hovercheck [-Capture <dir>] [-TimeoutSeconds 60]
param(
    [Parameter(Mandatory)][string]$Mode,
    [string]$Capture = "$env:TEMP\anomaly-autopilot",
    [int]$TimeoutSeconds = 60
)

$godot = 'C:\Users\vn\Apps\Godot\Godot 4.7.2.exe'
$project = Split-Path $PSScriptRoot -Parent

Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class AutoPilotWindow {
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int w, int h, uint flags);
    delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr hWnd, out int pid);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
    // A no_focus window is never the process's MainWindowHandle, so look for its unowned top-level window directly.
    public static IntPtr Find(int pid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) => { int p; GetWindowThreadProcessId(h, out p);
            if (p == pid && GetWindow(h, 4) == IntPtr.Zero) { found = h; return false; } return true; }, IntPtr.Zero);
        return found;
    }
}
'@

if (Test-Path $Capture) { Remove-Item -Recurse -Force $Capture }
New-Item -ItemType Directory $Capture | Out-Null
$log = Join-Path $Capture 'log.txt'

# Godot activates its window on startup unless no_focus is set when the window is created. Project settings are
# read at launch, so a temporary override.cfg scopes the flag to this run.
$override = Join-Path $project 'override.cfg'
if (Test-Path $override) { throw "override.cfg already exists; not touching it" }
Set-Content -Path $override -Encoding ascii -Value "[display]`r`n`r`nwindow/size/no_focus=true"

try {
    $SW_SHOWMINNOACTIVE = 7
    $startup = New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ ShowWindow = [uint16]$SW_SHOWMINNOACTIVE }
    $commandLine = "`"$godot`" --path `"$project`" --audio-driver Dummy --log-file `"$log`" -- --autopilot --$Mode --capture=`"$Capture`""
    $created = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
        CommandLine = $commandLine; CurrentDirectory = $project; ProcessStartupInformation = $startup }
    if ($created.ReturnValue -ne 0) { throw "Godot launch failed ($($created.ReturnValue))" }
    $process = Get-Process -Id $created.ProcessId

    # "Monitor 2" is \\.\DISPLAY2; fall back to any secondary screen, then the primary.
    $screens = [System.Windows.Forms.Screen]::AllScreens
    $target = $screens | Where-Object { $_.DeviceName -eq '\\.\DISPLAY2' } | Select-Object -First 1
    if (-not $target) { $target = $screens | Where-Object { -not $_.Primary } | Select-Object -First 1 }
    if (-not $target) { $target = $screens[0] }

    $window = [IntPtr]::Zero
    for ($i = 0; $i -lt 100 -and $window -eq [IntPtr]::Zero -and -not $process.HasExited; $i++) {
        Start-Sleep -Milliseconds 100
        $window = [AutoPilotWindow]::Find($process.Id)
    }
    if ($window -ne [IntPtr]::Zero) {
        $SW_SHOWNOACTIVATE = 4
        $SWP_NOSIZE_NOZORDER_NOACTIVATE = 0x0001 -bor 0x0004 -bor 0x0010
        [AutoPilotWindow]::ShowWindow($window, $SW_SHOWNOACTIVATE) | Out-Null
        [AutoPilotWindow]::SetWindowPos($window, [IntPtr]::Zero, $target.WorkingArea.X + 40, $target.WorkingArea.Y + 40, 0, 0, $SWP_NOSIZE_NOZORDER_NOACTIVATE) | Out-Null

        # Park the window on a virtual desktop the user is not looking at. The shell only knows about a window once it
        # is shown, so this has to come after ShowWindow. Screenshots come from the viewport texture, and a window on
        # another desktop is cloaked, not minimized, so the game keeps rendering.
        $virtualDesktop = 'C:\Users\vn\dependencies\PSModules\VirtualDesktop\VirtualDesktop.psd1'
        if (Test-Path $virtualDesktop) {
            Import-Module $virtualDesktop -DisableNameChecking
            $current = Get-DesktopIndex (Get-CurrentDesktop)
            $other = 0..((Get-DesktopCount) - 1) | Where-Object { $_ -ne $current } | Select-Object -First 1
            $desktop = if ($null -ne $other) { Get-Desktop $other } else { New-Desktop }
            $moved = $false
            for ($i = 0; $i -lt 40 -and -not $moved; $i++) {
                try { Move-Window -Desktop $desktop -Hwnd $window -ErrorAction Stop | Out-Null; $moved = $true }
                catch { Start-Sleep -Milliseconds 50 }
            }
            if (-not $moved) { Write-Warning "Could not move the game window to another virtual desktop" }
        }
        else { Write-Warning "VirtualDesktop module not found at $virtualDesktop; the window stays on this desktop" }
    }
}
finally {
    # Once the window exists the setting has been read, so the file goes before anyone else launches the game.
    Remove-Item $override -ErrorAction SilentlyContinue
}

if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { $process.Kill() }
Write-Output "log: $log"
