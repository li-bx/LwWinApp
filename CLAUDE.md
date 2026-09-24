# CLAUDE.md

给 Claude Code 的项目导航。**详细的原理、踩坑过程、实测数据都在 [README.md](README.md)**，
这里只放「动手前必须知道的东西」和那些不能违反的约定。

## 这是什么

WinUI 3 桌面应用框架（.NET 8 + Windows App SDK，**无打包**运行，双击 exe 即可启动），
外壳是「左侧一级导航 + 顶部多标签」，数据层接 MySQL。定位是「拿来当新项目起点」，
所以示例页（`框架示例 → *`）和框架代码同等重要 —— 它们是这个框架的用法说明。

## 命令

```bash
# 编译（-p:Platform=x64 别省：WindowsAppSDKSelfContained 不接受 AnyCPU）
dotnet build LwWinApp/LwWinApp.csproj -c Debug -p:Platform=x64

# 运行
LwWinApp/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64/LwWinApp.exe
```

仓库里**没有 `appsettings.json`**（不跟踪，里面有真实连接串）。新环境先
`cp LwWinApp/appsettings.example.json LwWinApp/appsettings.json` 再改里面的连接串。
数据库脚本：`LwWinApp/Sql/init.sql`（可重复执行），演示账号 `admin` / `123456`。

## 目录地图

| 目录 | 放什么 |
|---|---|
| `Core/` | 常量、配置模型、枚举、`BusinessException` |
| `Models/Entities` `Models/Dtos` | 一表一实体；`PagedResult<T>` / `QueryFilter` / `SelectOption` |
| `Data/` | 连接工厂、`RepositoryBase<T>`、`IUnitOfWork`、按表的具体仓储 |
| `Services/` | 界面级服务（对话框、标签、主题、窗口、通知、子窗口）+ 业务服务 |
| `Utils/` | 静态工具类；`WindowHelper` 是唯一的 Win32 互操作入口 |
| `Controls/` | `Pager` / `BusyOverlay` / `PageHeader` / `TableGrid` 等自建控件 |
| `Navigation/PageCatalog.cs` | **页面登记表，二级导航的唯一事实来源** |
| `ViewModels/` | 每页一份；`Demo/` 下是示例页的 |
| `Views/` | `Demo/` 示例页、`System/` 业务页、`Settings/`、`Dialogs/`、`Windows/` 弹窗窗口 |
| `Themes/` | `Colors.xaml` / `Styles.xaml` / `Icons.xaml` |

## 不能违反的约定

1. **页面登记只改 `PageCatalog.Groups`**。菜单（`MainWindow.xaml`）靠 Key 关联，
   Key 重复会在启动时抛异常 —— 别在两处各写一份页面清单。
2. **页面必须继承 `PageBase`**，ViewModel 在构造函数里用 `Require<T>()` 取
   （XAML 只认无参构造函数，页面不能走构造函数注入）。
3. **所有 `ContentDialog` 都走 `IDialogService`**（自动设 `XamlRoot` + 排队）。
   自己 `new` 一个再 `ShowAsync` 会撞上「同一时刻只能开一个」，或抛「没有 XamlRoot」。
4. **对话框与表单里可编辑字段的 `x:Bind` 一律写 `UpdateSourceTrigger=PropertyChanged`**。
   默认是 `LostFocus`，而对话框按钮不移动焦点 —— 最后编辑的那个字段会被静默丢掉。
5. **兜底的 `catch` 必须往日志里落一行。** 静默吞异常在本项目里制造过「设置改了不记住」这种
   查半天的问题。桌面应用没有控制台，日志是唯一线索。
6. **注释写「为什么」**（中文），尤其是为绕开某个 WinUI/Windows 的坑而写的代码 ——
   把坑本身写进注释，否则会被下一个人当废话删掉。
7. **`appsettings.json` 不跟踪**，不要 `git add` 它。
8. 编译必须保持 **0 警告 0 错误**。

## 改完怎么验证

本项目最典型的 bug 是**「编译通过、启动正常、就是不生效」**（例如视觉状态盖掉了样式、
绑定丢值、`RightInset` 给假值）。所以**不要靠读代码下结论**：

- `tools/uia.ps1` —— 按名字/自动化 ID 定位控件、点击、读值、截图。
  用法与 7 个容易「看起来失准」的坑见 `tools/README.md`。
- `tools/drag.ps1` —— 模拟拖拽（`SetCursorPos` 挪得动光标但派发不出 `PointerMoved`，
  必须用 `mouse_event` 注入，脚本里已经处理好了）。
- 截图存 `docs/screenshots/`。改了界面就抓一张，和改前的那张对比着看。
- 日志在 `%LOCALAPPDATA%\LwWinApp\logs\`。

跑不了应用（比如没有数据库）时，**如实说明「只过了编译、没有真机验证」**，
不要写成「已验证」。README 第 10 节就是按这个标准写的，请保持同样的诚实度。

## 提交

中文提交信息，一行说清**为什么**改（不是改了什么），一个提交只做一件事。
