# tools/ —— 辅助脚本

三个脚本，互不相干：

- `uia.ps1`：用 UI Automation 驱动正在运行的 `LwWinApp.exe`，做回归走查和截图。
- `drag.ps1`：模拟一次鼠标拖拽（UIA 覆盖不了的那些交互）。
- `make-icon.ps1`：生成应用图标（见文末）。

## 为什么要用 UIA 而不是模拟鼠标

试过合成鼠标事件（`SetCursorPos` + `mouse_event`），结论是不可用：

- Windows 的**文字建议 / 自动更正**弹窗会截走点击，而且它还会把输入框里的内容改掉
  （实测把 `LwFrameTest` 改成了 `LW frame test`）；
- 坐标点击依赖布局细节：标签栏一溢出，所有标签的 x 坐标就整体位移，点「关闭标签」会点到别的标签上；
- 下拉框在空间不够时会**往上弹**，固定坐标会选到已经选中的那一项。

UIA 按名字寻址控件，不经过鼠标，以上问题都不存在。

**唯一的例外是拖拽** —— 拖拽没有对应的 UIA 模式（`Invoke` 是「点一下」，没有「按住拖」），
所以 `drag.ps1` 还是得走鼠标，上面这些问题它照样有。要用的时候请看它那一节。

## 用法

```bash
# 1. 打印控件树（只列有名字或 AutomationId 的元素，即 UIA 的「控件视图」）
powershell -NoProfile -ExecutionPolicy Bypass -File tools/uia.ps1 -Dump |
  Out-File -Encoding UTF8 dump.txt

# 2. 按脚本跑一串动作，每行一个操作
powershell -NoProfile -ExecutionPolicy Bypass -File tools/uia.ps1 -ArgsFile steps.txt

# 3. 截图存到指定目录
powershell -NoProfile -ExecutionPolicy Bypass -File tools/uia.ps1 -ArgsFile steps.txt -OutDir docs/screenshots
```

参数：`-ProcessName`（默认 `LwWinApp`）、`-Depth`（遍历深度，默认 14）、`-OutDir`。

**`-Dump` 一定要重定向到 UTF8 文件再用编辑器看。** 直接输出到终端时，中文控件名会按控制台的
CP936 编码打印出来，看起来是乱码 —— 那只是显示问题，不影响 UIA 的匹配。

## 操作

| 操作 | 说明 |
|---|---|
| `dump` | 打印控件树 |
| `select:<名字>` | 按名字选中。`NavigationViewItem` 是 ListItem，走 `SelectionItemPattern`，不是 Invoke |
| `invoke:<名字>` | 按名字点击。命中往往是按钮里的 `TextBlock`，会自动往上找到支持 `InvokePattern` 的那层 |
| `clickid:<AutomationId>` | 按 AutomationId 点击 |
| `set:<AutomationId或名字>:<值>` | 给输入框赋值（`ValuePattern`） |
| `setindex:<n>:<值>` | 给「当前对话框里第 n 个输入框」赋值（0 起）。只在对话框窗口内收集，不会误抓页面背后的搜索框 |
| `key:<SendKeys串>` | 发送按键：`^w` = Ctrl+W、`{ESC}`、`{ENTER}` |
| `click:<x>,<y>` | 按窗口内坐标点击。UIA 够不着的角落（比如标题栏上的主题切换）才用 |
| `wait:<毫秒>` | 等待 |
| `shot:<文件名>` | 截当前窗口（存到 `-OutDir`）。拍的是屏幕上那一块，**被别的应用挡着就拍到别人**，见坑 4 |
| `shotwin:<文件名>` | 同上，但让窗口自己重绘（`PrintWindow`）。挡着也拍得到，代价是**拍不到 tooltip 这类弹出窗口** |

`#` 开头是注释，空行忽略。

**点在「禁用」的元素上会报 `INVOKE_FAIL` / `CLICK_FAIL`，脚本继续往下走。** 这个判断是 2026-09-30 补的：
在那之前，`invoke:` 命中一个禁用元素时 `Invoke()` 会抛 `ElementNotEnabledException`
（消息是 `Could not open the process token`，看不出跟「禁用」有关），而脚本是
`$ErrorActionPreference = "Stop"` —— 于是**整个走查当场停在这一行**，后面的步骤一条都不跑，
看起来像「脚本自己崩了」。它多半只是在说一件很平常的事：**这一步的前提不成立**。
例：应用刚起来时只剩常驻的仪表盘，「关闭全部标签」这一项就是禁用的（`steps-sample.txt` 开头为此先开一个页面）。

## 只有 x:Name 的元素才有 AutomationId

所以能按 id 找到的就这些：`ShellTabs`、`AddButton`、`PrimaryButton`、`CloseButton`、
`ErrorBar`、`UserNameBox`、`TogglePaneButton`、`TitleText`… 其余一律按名字找。

## 七个会让脚本「看起来失准」的坑

### 1. 弹出层里的名字会盖住导航里的同名项

`+` 按钮弹出的标签菜单，在控件视图里是外壳窗口的**最前面**的一段：

```
Pane | name='主机弹出窗口'
  Window | name='弹出窗口'
    MenuItem | name='系统管理'      ← 菜单的
    MenuItem | name='框架示例'
    MenuItem | name='设置'
    MenuItem | name='关闭全部标签'
Pane | name='Non Client Input Sink Window'
...
Custom | name='ShellNav'            ← 导航的
  ListItem | name='系统管理'
```

`Find-Anywhere` 按树的顺序取第一个匹配，所以**菜单开着的时候** `invoke:系统管理` 打到的是
菜单项而不是左侧导航。写脚本时要么先 `key:{ESC}` 关掉菜单，要么把这类步骤拆成两条命令。

### 2. `CloseButton` 这个 AutomationId 有两个意思

- 对话框的关闭按钮：`Button | name='取消' | id='CloseButton'`
- 每个标签页上的关闭按钮：`Button | name='关闭标签页' | id='CloseButton'`

`clickid:CloseButton` 取的是树里第一个匹配 —— 有对话框开着时它命中对话框，没有的时候
它会去关标签。所以**关对话框请按文字点**（`invoke:取消`），而且要按那个对话框**实际**的文字：
文字是调用方传进去的，同一个方法在不同页面可能是「取消」「算了」「关闭」。
拿不准就先 `dump` 看一眼。

### 3. 标签栏溢出会移动所有坐标

标签一多，标签栏出现 ◀/▶ 滚动按钮，于是每个标签的 x 坐标都变了。
自动化脚本里点标签一律用 `select:`，不要用 `click:`。

### 4. 截图里可能根本没有本应用

`shot:` 走的是 `Do-Shot`（`GetWindowRect` + `CopyFromScreen`），拍的是**屏幕上那一块**，不是窗口自己画的。
别的应用占满整个屏幕时，拍回来的是别人（实测：WPS 最大化 + 一个聊天窗口在前，
截出来的标题栏是 WPS 的，而应用当时根本没被挡住的自觉）。

**首选 `shotwin:`**，别跟 z 序较劲。它走 `PrintWindow(hwnd, dc, 2)`（`2` = `PW_RENDERFULLCONTENT`），
让窗口**自己往上画**，前面挡着谁都不影响，也不用去动前台窗口 —— 对 WinUI 3 实测可用
（1424×836 与 1440×900 各拍过一张，内容正常、不是黑图；当时 WPS 正占着整个屏幕）。
**它可能返回全黑**（有些内容走硬件合成，拿不到），脚本会数一遍非黑像素、拍成全黑时直接报
`SHOTWIN_FAIL` 让你改回 `shot:`，不会把一张黑图当成功交给你。

**`shotwin:` 拍不到弹出窗口**，这是它唯一的代价：tooltip / 下拉 / 菜单都是独立的顶层窗口，
不在主窗口的绘制里。要拍那些东西只能用 `shot:`，回到下面这套 z 序玩法。

退回方案：`SetForegroundWindow` 救不了这个。实测 `GetForegroundWindow()` **已经返回本窗口了**，
画面依旧被盖着 —— 对方窗口多半是 topmost 的，非 topmost 的窗口抬不到它上面去。可行的做法是
截图期间临时把本窗口也置顶，拍完立刻还原：

```powershell
SetWindowPos($hwnd, [IntPtr](-1), 0,0,0,0, 0x0047)   # HWND_TOPMOST, NOSIZE|NOMOVE|SHOWWINDOW
# 截图
SetWindowPos($hwnd, [IntPtr](-2), 0,0,0,0, 0x0047)   # HWND_NOTOPMOST
```

**一定要还原**。中途抛异常没还原到位的话，用户的窗口会一直浮在所有人上面。
另外这套也**不是万能的**：置顶之后仍旧被盖住过一次（对方也是 topmost，而且更靠前），
这种时候只有 `shotwin:` 一条路。

反过来，**要排查 tooltip 这类弹出窗口时，置顶必须撤掉**：tooltip 是独立的弹出窗口，
不是主窗口里画的一块内容，主窗口一旦置顶，弹出窗口就被压在自己主窗口下面，
截出来什么都没有 —— 会误判成「这个 tooltip 不存在」。同一个位置、同一套鼠标动作，
置顶时截不到、不置顶时截得到（实测过一次，白排查了两轮）。

### 5. 走查脚本本身会留下痕迹

- `key:` 走的是 `SendKeys`，发完会在鼠标位置留一个 tooltip 残影，截图里能看到（不是应用的 bug）；
- 主题被标题栏的那个按钮点过之后，会从「跟随系统」变成明确的「浅色/深色」并写进本地设置。
  走查完记得用「系统设置」页的下拉框改回「跟随系统」；
- 合成点击偶尔会误触（见下）。

### 6. 被滚动容器裁剪的元素，`BoundingRectangle` 报的是「内容长度」

`BoundingRectangle` 是布局坐标，但它**不反映裁剪**。分组表格页那个 `ListView`（视口只有几百像素）
在控件树里报出来的高度是 `2282` / `2762` —— 那是它内容的总长度，不是它被排版后的高度。
拿这个数判断「表格多高」「有没有溢出窗口」会得到完全相反的结论（实测踩过：差点把
「高度自适应没生效」当成 bug 去改）。

这类被滚动 / 被裁剪的元素，想知道它实际占多少可见区域只能**截图按像素量**；
`ActualHeight` 也是准的，但那要在后置代码里读，UIA 拿不到。

这一条和下面的拖拽坐标直接相关 —— 拖拽坐标正是这么量出来的，量之前先确认量的是
「看得见的那块」而不是内容长度。

### 7. 颜色别靠眼睛看：文字色用 UIA 读，底色用像素数

判断「这行字是不是白的」时，**看截图是失准的**。深色字压在饱和底色上、字号又小，
看上去就是浅色 —— 实测把一张 `dark=848 / light=0`（一个浅色像素都没有）的图看成了
「文字已经变白」，还差点据此得出「`Foreground` 那个属性生效了」的相反结论。
同一次还踩到第二个坑：按 `luma < 90` 分「深色像素」时，`#0F6CBD` 那个背景自己的 luma
是 **89.4**，被整片算成了文字像素，数字直接没意义。

**文字颜色首选 UIA 直接读**，不截图，别的应用挡着也照样读得到：

```powershell
$tp = $el.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern)
$fg = $tp.DocumentRange.GetAttributeValue(
        [System.Windows.Automation.TextPattern]::ForegroundColorAttribute)
# 返回值是 BGR 排列的 int，不是 RGB，拆的时候别搞反：
$r = $fg -band 0xFF; $g = ($fg -shr 8) -band 0xFF; $b = ($fg -shr 16) -band 0xFF
```

要按像素数的话，**别用 luma 阈值分类**，改成量「离背景色有多远」：
把背景色取出来，像素离它超过某个距离才算文字像素，这样饱和底色不会被算进去。
另外先拿一个**结论明确**的探针把方法本身验一遍（例：把底色改成品红、文字改成白，
浅色像素数从 0 变成 2239），再拿去量真正想问的那个问题。

## 已知不可靠的地方

**刚 `SW_RESTORE` 完的窗口，UIA 短时间内枚举不到。** 2026-09-30 走查「弹窗窗口」时，把主窗口最小化
再还原，紧接着去列这个进程的顶层窗口，**两个非模态弹窗一个都不在列表里**，看起来就是「还原把子窗口弄丢了」。
等 1~2 秒再列就都回来了，窗口从头到尾都在（`IsWindowVisible` 也是 `True`）。

判断「窗口还在不在」别只看一次枚举结果：**等一两秒重来一次**，或者直接读 UIA 元素本身的状态
（`IsEnabled` / `IsOffscreen`）—— 那才是不依赖枚举时序的读法。这次是先把结论往「窗口没了」的方向写了，
回头才发现是枚举的时序。

2026-09-22 那次走查里，有 **两个标签（部门架构、操作日志）在没有对应命令的情况下被打开了**，
没能定位到原因。单条操作本身都是准的，都单独验证过：

- `select:` 精确地只开一个标签（4 → 5，只多出被选的那个）；
- `key:^w` 精确地只关当前标签（5 → 4）；
- `关闭全部标签` 之后确实只剩常驻的「仪表盘」。

怀疑是「菜单开着时按名字寻址」那条（坑 1）在某一步命中了菜单项。**用这个脚本做断言时，
对每次操作后的控件树 dump 做校验，不要只看动作有没有报 ok。**

## 示例

`steps-sample.txt` 是一条可以直接跑的走查脚本。

---

# drag.ps1 —— 模拟一次拖拽

```bash
# 在窗口内 (456,388) 按住，向右拖到 (536,388)，松开
powershell -NoProfile -ExecutionPolicy Bypass -File tools/drag.ps1 `
  -FromX 456 -FromY 388 -ToX 536 -ToY 388

# 起点终点相同 = 一次原地点击（比如点进某个输入框拿到焦点）
powershell -NoProfile -ExecutionPolicy Bypass -File tools/drag.ps1 `
  -FromX 1000 -FromY 555 -ToX 1000 -ToY 555
```

坐标口径和 `uia.ps1` 的 `click:<x>,<y>` 一致：**窗口内物理像素**，左上角是 `0,0`。
参数：`-Steps`（分几步移动，默认 12）、`-StepDelayMs`（默认 30ms）、`-ProcessName`。

## 位移必须用 mouse_event 注入，不能只 SetCursorPos

这是写这个脚本时踩的坑，症状是「拖了但一点反应都没有」：

```
mouse_event 按下 + SetCursorPos 走 8 步        → 只收到 1 次 PointerMoved，delta=0
mouse_event 按下 + mouse_event(ABSOLUTE) 8 步  → 8 次 PointerMoved，delta 逐次正确
```

`SetCursorPos` 确实把光标挪动了（松开后 `GetCursorPos` 停在终点），但 WinUI 不会为它产生的那几次
移动派发 `PointerMoved`。**松手时的坐标又是对的** —— 所以这不是坐标系或换算的问题，
是那几次移动的消息没被派发。绝对坐标要按主屏宽高归一化到 `0~65535`。

顺带一个诊断手段：双击前先看脚本打印的第一行 `fg: ... isApp=True`。
前台锁偶尔会让窗口提不到前台，那时按键不会落到应用上，拖拽全程静默失败。

## 它有你自己的坑

- **输入法弹窗**。往输入框里打字时如果输入法切到中文，候选框会浮在窗口中间，
  截图全废；`SendKeys` 发过去的字母也会进到候选里（实测 `L2` 变成了带拼音候选的 `l-复核x`）。
  截图前先 `key:{ESC}` 关掉候选框；
- **坐标依赖布局**，和上面「为什么不用模拟鼠标」里说的一样 —— 表格行高一变、标签栏一溢出，
  之前量好的坐标就全作废了。每次拖之前重新量一遍；
- **别用 `uia.ps1` 的 `Do-Focus`**（`SW_RESTORE`）来提前台，它会把最大化的窗口还原，
  尺寸一变坐标就废了。`drag.ps1` 自己只做 `BringWindowToTop` + `SetForegroundWindow`。

---

# make-icon.ps1 —— 生成应用图标

品牌色圆角方块 + 白色字母标记，一次产出 16/24/32/48/64/128/256 七个尺寸的 `.ico`。

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/make-icon.ps1

# 换颜色 / 换字母
powershell -NoProfile -ExecutionPolicy Bypass -File tools/make-icon.ps1 -Color '#0F6CBD' -Text 'LW'
```

产物写到 `LwWinApp/Assets/AppIcon.ico`（外加一张 256 的 `AppIcon.png` 备用）。
`LwWinApp.csproj` 已经把它接好了：

- `<ApplicationIcon>` → 嵌进 exe 的资源段，资源管理器和快捷方式用它；
- 又作为 `Content` 复制到输出目录 → `WindowService` 启动时读它设**窗口**图标，
  任务栏按钮和 Alt+Tab 用的是这一个。

**两处都要，缺一不可**，原因见根目录 `README.md` 第 9 节。

改图标内容（颜色、字母）改完重跑这个脚本即可，不用手改 csproj。

## 几个刻意的做法

- **先放大 4 倍画、再降采样**，而不是直接在小画布上画字。16px 上直接渲染文字会糊成一团。
- **文字走 `GraphicsPath` 而不是 `DrawString`**：矢量路径缩放时不会带上 GDI+ 在小字号下做的字形微调，
  而且能顺带描边加粗。
- **16/24/32 额外描一圈同色的边**。`W` 在 16px 下只有约 7 像素宽却要放下四条斜线，
  不补这一下就是个白疙瘩。
- **`.ico` 里每张图都存 PNG 原始字节**（Vista 以后支持）。手写 32bpp 的 BMP 调色板和掩码是
  这个格式最容易出错的地方，绕开它。

## 改完怎么验证

别只看文件大小。`icon-preview` 那类做法是把 `.ico` 读回来、按目录项逐个解码，
确认每个尺寸解出来的宽高跟目录里声明的一致 —— 偏移量算错的话，后面几张图会解成乱码或者干脆解不出来。
另外把 16px 放大几倍看一眼：那个尺寸是任务栏实际用的，最容易糊。
