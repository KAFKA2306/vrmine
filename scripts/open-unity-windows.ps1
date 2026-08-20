param(
    [Parameter(Mandatory = $true)]
    [string]$UnityPath,
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath
)

Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class VrMineUnityWindow {
    public delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    public static string GetText(IntPtr window) {
        var text = new StringBuilder(512);
        GetWindowText(window, text, text.Capacity);
        return text.ToString();
    }

    public static IntPtr FindRestartButton(uint processId) {
        IntPtr button = IntPtr.Zero;
        EnumWindows((window, unused) => {
            uint ownerProcessId;
            GetWindowThreadProcessId(window, out ownerProcessId);
            if (ownerProcessId != processId || GetText(window) != "Unity is running as administrator.") return true;

            EnumChildWindows(window, (child, unusedChild) => {
                if (GetText(child).Contains("Restart Unity as a standard user")) {
                    button = child;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return button == IntPtr.Zero;
        }, IntPtr.Zero);
        return button;
    }
}
'@

$process = Start-Process -FilePath $UnityPath -ArgumentList @('-projectPath', $ProjectPath) -WorkingDirectory $ProjectPath -PassThru
Write-Output "Windows Unity process: $($process.Id)"

$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) {
    if ($process.HasExited) { break }
    $button = [VrMineUnityWindow]::FindRestartButton([uint32]$process.Id)
    if ($button -ne [IntPtr]::Zero) {
        [VrMineUnityWindow]::SendMessage($button, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        Write-Output 'Unity administrator warning accepted; standard-user restart requested.'
        break
    }
    Start-Sleep -Milliseconds 250
}
