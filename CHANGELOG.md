# 更新日志

本文件记录本项目的所有重要改动，格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

**说明**：本项目的“验证”指在真机上把功能跑一遍并核对结果（截图、UIA 读值、数据库回查），
不是“单元测试通过”。凡是标了「未走查」的条目，都只保证编译通过，运行行为尚未核对。

## [未发布]

### 新增

- 「弹窗窗口」示例页（`框架示例 → 弹窗窗口`）：三种模态窗口（消息 / 确认 / 表单）、嵌套模态窗口、
  可多开的非模态窗口（带计数与「全部关闭」），以及一张「ContentDialog / 模态窗口 / 非模态窗口 怎么选」的对照表。
- `IChildWindowService`：弹窗窗口的显示与生命周期（认属主、锁属主、非模态计数、全部关闭）。
- `DialogWindow`：通用弹窗窗口外壳（标题 / 说明 / 正文 / 一排按钮 / 关闭原因）。
  关闭原因用新加的 `DialogResult`，不是 `ContentDialogResult` —— 后者分不出「点了取消」和「直接关了 X」。
- `WindowHelper` 新增 `SetOwner` / `SetEnabled` / `CenterOnOwner`；`IThemeService` 新增 `ApplyToWindow`。

### 验证

- 上面这一整页（含 `DialogWindow` 与 `IChildWindowService`）已在真机上走查通过，逐条结论见
  README 第 10 节。五条最容易出问题的都过了：模态是否真的锁住属主、关内层时外层是否只解开一层、
  非模态是否不锁属主且跟着属主收起、深色下弹窗本体与标题栏是否都变深、任务栏上是否只占一个按钮。
- 走查收尾时另外查出一个缺口：**已经打开的弹窗不跟着切主题**（`ApplyToWindow` 只在窗口显示的那一刻
  调一次，没有谁订阅 `ThemeChanged`）。实测数据与修法见 README 第 5 节「弹窗窗口不继承主题」。
  **尚未修复**，本轮只记录。
- 截图补在 `docs/screenshots/11-弹窗窗口.png` 与 `12-嵌套模态窗口.png`。

## [0.1.0] - 2026-09-24

首个版本：一套可作为新项目起点的 WinUI 3 桌面应用框架。

### 新增

- **外壳**：`NavigationView` 一级导航 + `TabView` 二级标签，每个标签持有独立 `Frame`；
  标签支持 `Ctrl+数字` 切换、`Ctrl+W` 关闭、`+` 菜单按分组开新页、全部关闭。
- **数据访问层**：`IDatabaseFactory` / `IRepository<T>` / `IUnitOfWork` / `RepositoryBase<T>`，
  表名与主键按约定 + 特性解析并缓存，下划线列名全局映射打开；Dapper + MySqlConnector。
- **业务页面**：仪表盘（真实聚合查询）、用户管理（增删改查 + 分页 + 导出 CSV）、
  角色管理、部门架构（TreeView 三层）、操作日志（大表格 + 分页 + 动态筛选）。
- **示例页面**：常用控件、弹出对话框、进度与加载、消息通知、分组编辑表格（两级分组 + 行列拖拽）、表单校验。
- **自建控件**：`Pager` / `BusyOverlay` / `PageHeader` / `TableGrid` / `TableLayout` / `ResizeGrip`。
- **服务层**：`IDialogService`（内部排队，同一时刻只开一个）、`ITabNavigationService`、
  `IThemeService`、`IWindowService`、`INotificationService`、`IFilePickerService`、`IWindowContext`。
- **工具库**：加密 / PBKDF2 口令哈希（兼容旧格式并就地升级）/ JSON / 时间 / 字符串 / 文件 /
  CSV 导出 / HTTP / 正则校验 / 线程调度 / 窗口 Win32 互操作 / 本地设置 / 文件日志。
- **无打包运行**：`WindowsPackageType=None` + 自带 Windows App SDK 运行时，双击 exe 即可启动，目标机器无需预装。
- **审计**：所有写操作落 `sys_log`；密码只存 PBKDF2-SHA256 哈希（21 万次迭代、每用户独立盐）。
- 深色 / 亮色 / 跟随系统主题，窗口状态与主题记忆，应用图标与 `tools/` 走查脚本。

### 修复

走查阶段修掉的五个真 bug（详见 README 第 9、10 节）：

- `ShowCustomAsync` 漏设 `XamlRoot`，导致所有页面专属对话框一打开就抛异常。
- `x:Bind` 默认 `LostFocus`，新建用户时最后编辑的字段被静默丢弃。
- 危险确认框的主按钮被 `PrimaryAsDefaultButton` 视觉状态盖成 accent 蓝，警示色不生效。
- `settings.json` 被建成了目录，此后每次写盘静默失败 —— 表现为设置改了不记住。
- 还原窗口后标题栏右侧错位：`RightInset` 在最小化/还原过渡期会返回负数，而还原只触发一次事件。
