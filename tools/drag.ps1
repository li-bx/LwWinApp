<#
  LwWinApp 辅助脚本 —— 在窗口里模拟一次鼠标拖拽（按住 → 移动 → 松开）。

  为什么只有这一个脚本走鼠标：uia.ps1 那一套（按名字寻址、InvokePattern）覆盖不了拖拽 ——
  拖拽没有对应的 UIA 模式，只能真的按下再移动。所以坐标依赖、前台窗口这些老问题在这个脚本里
  依然存在，用之前先看清楚参数表。

  用法：
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/drag.ps1 `
      -FromX 456 -FromY 388 -ToX 536 -ToY 388

  坐标是**窗口内坐标**（左上角为 0,0，物理像素），跟 uia.ps1 的 click:<x>,<y> 一个口径。
  怎么拿到目标坐标：给控件加 x:Name，再用 tools/uia.ps1 的控件树读出它的 BoundingRectangle，
  换算成窗口内坐标（也可以直接用 uia.ps1 的 click 试一次，看命中没有）。

  参数：
    -FromX/-FromY   按下点
    -ToX/-ToY       松开点（和按下点相同 = 一次原地点击）
    -Steps          中间分几步移动，默认 12。步子太粗有些控件收不到中间的 PointerMoved
    -StepDelayMs    每步间隔，默认 30ms。30ms 是实测下来 WinUI 能稳定逐帧收到的节奏

  两个必须知道的点，否则会得到「拖了但没反应」：

  1. 位移必须用 mouse_event(MOUSEEVENTF_MOVE|ABSOLUTE) 注入。
     SetCursorPos 只是把光标挪过去，WinUI 不会为它派发 PointerMoved ——
     实测按下一路拖，整个拖拽只收到 1 次 PointerMoved 且 delta=0；
     换成 mouse_event 注入位移后，8 步就是 8 次，delta 逐次正确。
     松手时坐标是对的，所以这不是坐标系问题，是消息没派发。

  2. 窗口必须是前台窗口。脚本自己会试一次（AttachThreadInput + SetForegroundWindow），
     但 Windows 的前台锁有可能让它失败，脚本会打印 fg: 一行告诉你成没成 ——
     isApp=True 才说明按键真的会落到本应用上。
#>
param(
    [Parameter(Mandatory = $true)][int]$FromX,
    [Parameter(Mandatory = $true)][int]$FromY,
    [Parameter(Mandatory = $true)][int]$ToX,
    [Parameter(Mandatory = $true)][int]$ToY,
    [int]$Steps = 12,
    [int]$StepDelayMs = 30,
    [string]$ProcessName = 'LwWinApp'
)
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class LwDrag {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
    public const uint MOVE = 0x0001, ABSOLUTE = 0x8000, DOWN = 0x0002, UP = 0x0004;
}
"@

$scrW = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width
$scrH = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Height

# 主屏坐标归一化到 0~65535，这是 MOUSEEVENTF_ABSOLUTE 的要求。
function MoveTo([int]$x, [int]$y) {
    $nx = [uint32][math]::Round($x * 65535.0 / ($scrW - 1))
    $ny = [uint32][math]::Round($y * 65535.0 / ($scrH - 1))
    [LwDrag]::mouse_event([LwDrag]::MOVE -bor [LwDrag]::ABSOLUTE, $nx, $ny, 0, [UIntPtr]::Zero)
}

$p = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Output "NOT RUNNING ($ProcessName)"; exit 1 }
$h = $p.MainWindowHandle

# 只把窗口提到前台，不碰窗口状态：uia.ps1 的 Do-Focus 会用 SW_RESTORE，
# 那会顺手把最大化退掉，窗口尺寸一变所有坐标就作废了。
$fg = [LwDrag]::GetForegroundWindow()
$fgT = [LwDrag]::GetWindowThreadProcessId($fg, [IntPtr]::Zero)
$myT = [LwDrag]::GetCurrentThreadId()
[void][LwDrag]::AttachThreadInput($myT, $fgT, $true)
[void][LwDrag]::BringWindowToTop($h)
[void][LwDrag]::SetForegroundWindow($h)
[void][LwDrag]::AttachThreadInput($myT, $fgT, $false)
Start-Sleep -Milliseconds 600

$fgAfter = [LwDrag]::GetForegroundWindow()
Write-Output ("fg: before={0} after={1} isApp={2}" -f $fg, $fgAfter, ($fgAfter -eq $h))

$r = New-Object LwDrag+RECT
[void][LwDrag]::GetWindowRect($h, [ref]$r)
$x1 = $r.L + $FromX; $y1 = $r.T + $FromY
$x2 = $r.L + $ToX;   $y2 = $r.T + $ToY

MoveTo $x1 $y1
Start-Sleep -Milliseconds 250
[LwDrag]::mouse_event([LwDrag]::DOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 200
for ($i = 1; $i -le $Steps; $i++) {
    $x = [int]($x1 + ($x2 - $x1) * $i / $Steps)
    $y = [int]($y1 + ($y2 - $y1) * $i / $Steps)
    MoveTo $x $y
    Start-Sleep -Milliseconds $StepDelayMs
}
Start-Sleep -Milliseconds 200
[LwDrag]::mouse_event([LwDrag]::UP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 400
Write-Output ("DRAG ({0},{1}) -> ({2},{3})  win={4},{5}" -f $FromX, $FromY, $ToX, $ToY, $r.L, $r.T)
