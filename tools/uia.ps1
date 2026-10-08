<#
  LwWinApp 自动化走查工具 —— 用 UI Automation 驱动正在运行的应用。

  为什么不用合成鼠标事件：
  Windows 的文字建议 / 自动更正弹窗会截走 mouse_event 的点击，坐标点击还依赖
  标签栏有没有溢出、下拉框往上还是往下弹这类布局细节，非常脆。UIA 直接按名字
  寻址控件，不走鼠标，也不受布局影响。

  为什么源文件必须带 UTF-8 BOM：
  PowerShell 5.1 读没有 BOM 的 .ps1 会当成 ANSI，本文件里的中文注释和字符串
  会被解析成乱码。若你的编辑器另存为无 BOM，请改回 UTF-8 with BOM。
  同理，通过 -ArgsFile 传进来的中文控件名，文件也要是 UTF-8。

  用法：
    # 打印控件树（实际是「控件视图」，只列有名字或 AutomationId 的元素）
    powershell -File tools/uia.ps1 -Dump | Out-File -Encoding UTF8 dump.txt

    # 按脚本跑一串动作，每行一个操作，# 开头是注释
    powershell -File tools/uia.ps1 -ArgsFile steps.txt

  操作列表：
    dump                      打印控件树
    select:<名字>             按名字选中（NavigationViewItem 这类 ListItem 走 SelectionItemPattern）
    invoke:<名字>             按名字点击（自动往上找到支持 InvokePattern 的那层）
    clickid:<AutomationId>    按 AutomationId 点击
    set:<AutomationId或名字>:<值>    给输入框赋值（ValuePattern）
    setindex:<n>:<值>         给「当前对话框里第 n 个输入框」赋值（0 起）
    key:<SendKeys 串>         发送按键，如 ^w = Ctrl+W、{ESC}
    click:<x>,<y>             按窗口内坐标点击（UIA 够不着的角落才用）
    wait:<毫秒>
    shot:<文件名>             截窗口（存在 -OutDir，默认当前目录）
    shotwin:<文件名>          截窗口的「自己画的那张」（不经过屏幕，被挡着也拍得到）

  只给 x:Name 的元素才有 AutomationId，所以脚本里能按 id 找到的只有：
  ShellTabs / AddButton / PrimaryButton / CloseButton / ErrorBar / UserNameBox 等。
#>
param(
    [string]$ProcessName = "LwWinApp",
    [switch]$Dump,
    [string]$ArgsFile = "",
    [int]$Depth = 14,
    [string]$OutDir = "."
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

# GetWindowRect 返回的是物理像素，进程不声明 DPI 感知的话拿到的是被缩放过的值，
# 截出来的图会缺一块。
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Lw32 {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flag, uint dx, uint dy, uint data, UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

[void][Lw32]::SetProcessDPIAware()

$proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) {
    Write-Output "NO_WINDOW 进程 $ProcessName 没有主窗口，应用可能没启动"
    exit 1
}

$h = $proc.MainWindowHandle
$script:Walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker

# 该进程的所有顶层窗口。ContentDialog 是独立顶层窗口，tab 提示等弹出层也是。
function Get-TopWindows {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
    return @([System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children, $cond))
}

function Walk($el, [int]$level) {
    if ($level -gt $Depth) { return }
    $child = $script:Walker.GetFirstChild($el)
    while ($child) {
        $ct = $child.Current.ControlType.ProgrammaticName -replace '^ControlType\.', ''
        $name = $child.Current.Name
        $aid = $child.Current.AutomationId
        if ($name -or $aid) {
            Write-Output ("{0}{1} | name='{2}' | id='{3}'" -f (' ' * $level), $ct, $name, $aid)
        }
        Walk $child ($level + 1)
        $child = $script:Walker.GetNextSibling($child)
    }
}

function FindByName($el, [string]$name, [int]$level) {
    if ($level -gt $Depth) { return $null }
    $child = $script:Walker.GetFirstChild($el)
    while ($child) {
        if ($child.Current.Name -eq $name) { return $child }
        $hit = FindByName $child $name ($level + 1)
        if ($hit) { return $hit }
        $child = $script:Walker.GetNextSibling($child)
    }
    return $null
}

function FindByAutomationId($el, [string]$id, [int]$level) {
    if ($level -gt $Depth) { return $null }
    $child = $script:Walker.GetFirstChild($el)
    while ($child) {
        if ($child.Current.AutomationId -eq $id) { return $child }
        $hit = FindByAutomationId $child $id ($level + 1)
        if ($hit) { return $hit }
        $child = $script:Walker.GetNextSibling($child)
    }
    return $null
}

# 按 id 找遍所有窗口（id 唯一），按名字只在需要时遍历。
function Find-ById([string]$id) {
    foreach ($w in Get-TopWindows) {
        $hit = FindByAutomationId $w $id 0
        if ($hit) { return $hit }
    }
    return $null
}

function Find-Anywhere([string]$name) {
    # 先按名字当 id 试一次：脚本里经常拿 AutomationId 当名字用。
    $hit = Find-ById $name
    if ($hit) { return $hit }

    foreach ($w in Get-TopWindows) {
        $hit = FindByName $w $name 0
        if ($hit) { return $hit }
    }
    return $null
}

function Get-DialogWindow {
    # 对话框在控件视图里是外壳窗口的一个子 Window。
    # setindex 只在这个范围内收集输入框，免得把页面背后的搜索框也算进来。
    $root = (Get-TopWindows)[0]
    $child = $script:Walker.GetFirstChild($root)
    while ($child) {
        if ($child.Current.ControlType.ProgrammaticName -eq 'ControlType.Window') { return $child }
        $child = $script:Walker.GetNextSibling($child)
    }
    return $root
}

$script:Edits = New-Object System.Collections.ArrayList

function Collect-Edits($el, [int]$level) {
    if ($level -gt $Depth) { return }
    $child = $script:Walker.GetFirstChild($el)
    while ($child) {
        try {
            $null = $child.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
            [void]$script:Edits.Add($child)
        } catch { }
        Collect-Edits $child ($level + 1)
        $child = $script:Walker.GetNextSibling($child)
    }
}

function Get-Pattern($el, $pattern) {
    try { return $el.GetCurrentPattern($pattern) } catch { return $null }
}

function Do-Dump {
    $i = 0
    foreach ($w in Get-TopWindows) {
        Write-Output "===== window #$i : '$($w.Current.Name)' ====="
        Walk $w 0
        $i++
    }
}

function Do-Select([string]$name) {
    $el = Find-Anywhere $name
    if (-not $el) { Write-Output "SELECT_FAIL 找不到: '$name'"; return }

    # NavigationViewItem 是 ListItem，只有 SelectionItemPattern。
    # 按名字找到的往往是内层的 TextBlock，所以要往上找几层。
    $cur = $el
    for ($i = 0; $i -lt 6 -and $cur; $i++) {
        $sp = Get-Pattern $cur ([System.Windows.Automation.SelectionItemPattern]::Pattern)
        if ($sp) {
            $sp.Select()
            Start-Sleep -Milliseconds 900
            Write-Output "SELECT ok '$name'"
            return
        }
        $cur = $script:Walker.GetParent($cur)
    }
    Write-Output "SELECT_FAIL '$name' 及其父级都不支持 SelectionItemPattern"
}

function Do-Invoke([string]$name) {
    $el = Find-Anywhere $name
    if (-not $el) { Write-Output "INVOKE_FAIL 找不到: '$name'"; return }

    # 命中的常常是按钮里的 TextBlock，本身不可点，往上找可点的父级。
    $cur = $el
    for ($i = 0; $i -lt 6 -and $cur; $i++) {
        $ip = Get-Pattern $cur ([System.Windows.Automation.InvokePattern]::Pattern)
        if ($ip) {
            $ip.Invoke()
            Start-Sleep -Milliseconds 900
            Write-Output "INVOKE ok '$name'"
            return
        }
        $cur = $script:Walker.GetParent($cur)
    }
    Write-Output "INVOKE_FAIL '$name' 及其父级都不支持 InvokePattern"
}

function Do-ClickId([string]$id) {
    $el = Find-ById $id
    if (-not $el) { Write-Output "CLICK_FAIL 找不到 id: '$id'"; return }
    $ip = Get-Pattern $el ([System.Windows.Automation.InvokePattern]::Pattern)
    if (-not $ip) { Write-Output "CLICK_FAIL '$id' 不支持 InvokePattern"; return }
    $ip.Invoke()
    Start-Sleep -Milliseconds 900
    Write-Output "CLICK ok '$id'"
}

function Do-SetByName([string]$name, [string]$value) {
    $el = Find-Anywhere $name
    if (-not $el) { Write-Output "SET_FAIL 找不到: '$name'"; return }

    $vp = Get-Pattern $el ([System.Windows.Automation.ValuePattern]::Pattern)
    if (-not $vp) { Write-Output "SET_FAIL '$name' 不支持 ValuePattern（它不是输入框？）"; return }
    $vp.SetValue($value)
    Write-Output "SET ok '$name' = '$value'"
}

function Do-SetByIndex([int]$index, [string]$value) {
    $script:Edits.Clear()
    Collect-Edits (Get-DialogWindow) 0
    if ($index -ge $script:Edits.Count) {
        Write-Output "SET_FAIL 第 $index 个越界（只找到 $($script:Edits.Count) 个输入框）"
        return
    }
    $vp = Get-Pattern $script:Edits[$index] ([System.Windows.Automation.ValuePattern]::Pattern)
    if (-not $vp) { Write-Output "SET_FAIL 第 $index 个不支持 ValuePattern"; return }
    $vp.SetValue($value)
    Write-Output "SET ok [$index] = '$value'"
}

function Get-Rect {
    $r = New-Object Lw32+RECT
    [void][Lw32]::GetWindowRect($h, [ref]$r)
    return $r
}

function Do-Focus {
    [void][Lw32]::ShowWindow($h, 9)   # SW_RESTORE
    [void][Lw32]::SetForegroundWindow($h)
    Start-Sleep -Milliseconds 350
}

function Do-Shot([string]$name) {
    $r = Get-Rect
    $w = $r.Right - $r.Left
    $ht = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $ht)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
    $path = Join-Path $OutDir $name
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output "SHOT $name ($w x $ht)"
}

# 同上，但让窗口**自己重绘**一遍，不经过屏幕。
# 好处：前面挡着别的应用也拍得到，也不用去抢前台。
# 代价：拍不到弹出窗口 —— tooltip / 下拉 / 菜单都是独立的顶层窗口，不在主窗口的绘制里。
# 所以要看 tooltip 就用 shot:（并且别置顶），要看被挡住的主窗口就用 shotwin:。
function Do-ShotWin([string]$name) {
    $r = Get-Rect
    $w = $r.Right - $r.Left
    $ht = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $ht)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $dc = $g.GetHdc()
    # 2 = PW_RENDERFULLCONTENT。构造 DirectComposition 的内容（WinUI 3 就是）需要这个标志，
    # 不带它拍回来是一张黑图。
    $ok = [Lw32]::PrintWindow($h, $dc, 2)
    $g.ReleaseHdc($dc)
    $g.Dispose()
    $path = Join-Path $OutDir $name
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)

    # 抽几个点看看是不是全黑。黑图是这条路走不通的样子，别当成拍成功了。
    $lit = 0
    for ($y = 0; $y -lt $ht; $y += 7) {
        for ($x = 0; $x -lt $w; $x += 7) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.R -gt 12 -or $c.G -gt 12 -or $c.B -gt 12) { $lit++ }
        }
    }
    $bmp.Dispose()
    if (-not $ok -or $lit -eq 0) {
        Write-Output "SHOTWIN_FAIL $name 拍回来是全黑（ok=$ok），这条路在这台机器上不通，改用 shot:"
        return
    }
    Write-Output "SHOTWIN $name ($w x $ht)"
}

function Do-Click([string]$coords) {
    Do-Focus
    $xy = $coords.Split(',')
    $r = Get-Rect
    $x = $r.Left + [int]$xy[0]
    $y = $r.Top + [int]$xy[1]
    [void][Lw32]::SetCursorPos($x, $y)
    Start-Sleep -Milliseconds 120
    [Lw32]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)   # LEFTDOWN
    Start-Sleep -Milliseconds 60
    [Lw32]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)   # LEFTUP
    Write-Output "CLICK $coords -> 屏幕 $x,$y"
}

function Do-Key([string]$keys) {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.SendKeys]::SendWait($keys)
    Write-Output "KEY '$keys'"
}

if ($Dump) { Do-Dump; exit 0 }

if ($ArgsFile -eq '') {
    Write-Output "需要 -Dump 或 -ArgsFile，见文件头部的用法说明。"
    exit 0
}

if (-not (Test-Path $ArgsFile)) {
    Write-Output "找不到参数文件: $ArgsFile"
    exit 1
}

# -Encoding UTF8 是必须的：文件里是中文控件名。
foreach ($line in (Get-Content -Path $ArgsFile -Encoding UTF8)) {
    $line = $line.Trim()
    if ($line -eq '' -or $line.StartsWith('#')) { continue }

    $parts = $line.Split(':', 3)
    $op = $parts[0]

    switch ($op) {
        'dump'     { Do-Dump }
        'select'   { Do-Select $parts[1] }
        'invoke'   { Do-Invoke $parts[1] }
        'clickid'  { Do-ClickId $parts[1] }
        'set'      { Do-SetByName $parts[1] $parts[2] }
        'setindex' { Do-SetByIndex ([int]$parts[1]) $parts[2] }
        'key'      { Do-Key $parts[1] }
        'click'    { Do-Click $parts[1] }
        'wait'     { Start-Sleep -Milliseconds ([int]$parts[1]); Write-Output "WAIT $($parts[1])" }
        'shot'     { Do-Shot $parts[1] }
        'shotwin'  { Do-ShotWin $parts[1] }
        default    { Write-Output "未知操作: $line" }
    }
}
