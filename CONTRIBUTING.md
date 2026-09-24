# 参与开发

先读 README 的第 1 节（环境与运行）和第 9 节（踩过的坑）。第 9 节是这个项目里最省时间的一节 ——
里面每一条都是真机跑出来的，照着做能少走几天弯路。

## 环境准备

| 项 | 要求 |
|---|---|
| 操作系统 | Windows 10 1809（17763）及以上，Win11 才能看到 Mica 和窗口圆角 |
| .NET SDK | 8.0 |
| Visual Studio | 2022 17.10+，勾选「使用 C++ 的桌面开发」里的 Windows SDK 与「.NET 桌面开发」 |
| MySQL | 8.0，跑得起来就行；没有也能开发，只是凡是要读库的页面会报连接失败 |
| PowerShell | 5.1 即可，`tools/` 里的脚本没用到 7.x 的语法 |

只要命令行编译的话 VS 不是必须的，但**第一次还原 NuGet 包**建议用 VS 或 `dotnet restore` 走一遍网络。

## 跑起来

```bash
# 1. 建本地配置（appsettings.json 不跟踪，仓库里只有模板）
cp LwWinApp/appsettings.example.json LwWinApp/appsettings.json
#    然后把里面的 Server / Uid / Pwd 改成你自己的

# 2. 建库 + 灌种子数据（可重复执行）
mysql -h 127.0.0.1 -P 3306 -u root -p lwwinapp < LwWinApp/Sql/init.sql

# 3. 编译
dotnet build LwWinApp/LwWinApp.csproj -c Debug -p:Platform=x64

# 4. 运行
LwWinApp/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64/LwWinApp.exe
```

演示账号是 `admin` / `123456`。

**编译命令里的 `-p:Platform=x64` 别省**：`WindowsAppSDKSelfContained` 不接受 `AnyCPU`，
裸跑 `dotnet build` 会报 `requires a supported Windows architecture`。

## 提交

- **提交信息用中文，一行说清「为什么改」**，不是「改了什么」——后者看 diff 就知道。
  细节多的话空一行再展开。
- **一个提交只做一件事。** 顺手改的格式、重命名、无关的清理，另开一个提交。
- 分支名用 `feat/xxx`、`fix/xxx` 这种前缀，PR 合到 `main`。

```bash
git commit -m "表格行高拖拽用窗口坐标算位移

用自身坐标每帧读到的都是 0，把手跟着边界走，相对位置根本没变。"
```

## 代码风格

跟着现有代码走，几条硬性的：

- **文件级命名空间**（`namespace LwWinApp.Utils;`），4 空格缩进，`Nullable` 与 `ImplicitUsings` 都是开的。
- 私有字段 `_camelCase`，常量 `PascalCase`，静态只读 `PascalCase`。
- **注释写「为什么」**，不写「是什么」。`// 自增 1` 这种注释不要。
  凡是为了绕开某个 Windows / WinUI 的坑而写的代码，把坑本身写进注释里 ——
  否则下一个人（很可能是三个月后的你）会把它当废话删掉，然后 bug 回来。
- 兜底的 `catch` 必须往日志里落一行。静默吞异常在本项目里已经制造过一个查了半天的问题
  （见 README 第 9 节「目录和文件的名字要分开」）。
- 新增对话框/表单里的可编辑字段，`x:Bind` 一律写 `UpdateSourceTrigger=PropertyChanged`。

## 加页面 / 加表 / 加对话框

三套步骤在 README 第 8 节的「扩展指南」里，照抄即可。加完之后**务必真跑一遍**：
本项目的 bug 大多是「编译通过、启动正常、就是不生效」，只看代码看不出来。

## 提 PR 之前

- [ ] `dotnet build LwWinApp/LwWinApp.csproj -c Debug -p:Platform=x64` 是 **0 警告 0 错误**
      （警告不是噪音，这个项目一直是零警告）
- [ ] 真机跑过受影响的页面，并说明验证了什么、怎么验证的（截图 / UIA 读到的值 / 数据库回查）
- [ ] 如果改了受影响的面板布局，用 `tools/uia.ps1` 抓一张截图放到 `docs/screenshots/`
- [ ] 没有提交本地文件：`appsettings.json`、`.claude/settings.local.json`、`*.user`、`bin/`、`obj/`
- [ ] 改了行为的话，README 相应章节和 `CHANGELOG.md` 一起改掉

## 报 bug

带上这三样，基本能直接定位：

1. `%LOCALAPPDATA%\LwWinApp\logs\` 里对应时间段的日志（桌面应用没有控制台，日志是唯一线索）
2. 复现步骤，以及**窗口状态**（是否最大化、DPI 缩放比例、当前主题）——
   好几个已知问题只在「最小化后还原」「切换 DPI」「深色主题」时才出现
3. 相关截图

## 许可

提交代码即表示同意以 [MIT 协议](LICENSE) 授权。
