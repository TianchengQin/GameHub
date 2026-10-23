# GameHub

一个原生 Windows 桌面游戏库管理软件：统一管理本地任意 exe 游戏，支持手动/扫描文件夹/拖拽添加、自定义分组、启动与游玩时长统计、自定义封面图，并提供一键安装包。

界面基于 [WPF-UI](https://github.com/lepoco/wpfui) 实现 Windows 11 风格的 Fluent/Mica 深色主题。

## 功能特性

### 添加游戏
- **手动添加**：选择单个 exe 文件
- **拖拽添加**：直接把 exe 拖进主窗口
- **扫描文件夹**：选择一个根目录（如 `D:\Games`），自动遍历每个子文件夹，用黑名单规则过滤安装包/卸载程序/运行库等明显不是游戏主程序的 exe，再按"文件名与文件夹名相似度 + 是否在根目录 + 文件大小"打分排序，弹出确认对话框让你勾选要导入的游戏、并可手动改选探测到的其他 exe

### 分组与浏览
- 侧边栏固定分组：全部游戏 / 收藏 / 最近玩过
- 自定义分组（可增删改，一个游戏可同时属于多个分组）
- 搜索、收藏、右键菜单（查看详情 / 收藏 / 在文件夹中显示 / 从库中移除）

### 启动与统计
- 点击启动即记录"上次启动时间"（不依赖进程能否被追踪，保证这一项始终准确）
- 自动统计启动次数、总游玩时长
- 运行中状态标识，防止重复启动
- exe 路径失效检测，详情面板可重新定位

### 游戏详情 & 封面
- 详情面板：统计数据、备注、自定义启动参数/工作目录、所属分组管理
- 图标：新增游戏时提取 exe 的 256px 高清图标（旧版本留下的低清缓存会在启动时自动升级），无自定义封面时居中显示，避免放大模糊
- 封面图：详情面板可手动更换为自定义图片或在线搜索，自定义封面会铺满卡片

### 收藏与悬浮窗
- 卡片右上角 ★/☆ 一键收藏，收藏的游戏在主界面自动排在最前面
- 「收藏悬浮窗」：置顶、无边框、可拖动的小窗口，只显示收藏游戏的图标，单击即启动；位置与开关状态会被记住
- 启动游戏后可自动最小化到系统托盘（设置中可关闭），单击托盘图标恢复

### 更多添加/整理方式
- 从开始菜单导入：解析开始菜单里的快捷方式（.lnk），自动过滤卸载/帮助/运行库，勾选确认后导入
- 把游戏卡片拖到侧边栏分组上即可加入该分组；在「管理分组」里拖动 ⠿ 手柄调整分组顺序
- 启动失败/路径失效会弹出 toast 提示，而不是静默失败
- 在线封面：设置 SteamGridDB API Key 后，可在游戏详情里搜索并选用高清封面

### 设置
- 主题切换（浅色 / 深色 / 跟随系统）
- 文件夹扫描黑名单规则增删
- 数据/缓存文件夹入口
- 游戏库导出/导入（JSON 备份，用于换电脑或重装系统后恢复）

## 技术栈

| 层 | 技术 |
|---|---|
| UI | .NET 8 + WPF + [WPF-UI](https://github.com/lepoco/wpfui)（Fluent Design / Mica） |
| MVVM | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) |
| 数据持久化 | SQLite + EF Core（`Microsoft.EntityFrameworkCore.Sqlite`） |
| 依赖注入 | `Microsoft.Extensions.Hosting` |
| 图标提取 | `System.Drawing.Common` |
| 安装包 | [Inno Setup 6](https://jrsoftware.org/isinfo.php) |

## 项目结构

```
GameHub.sln
├─ GameHub.Core/        领域模型、文件夹扫描启发式算法、进程启动与计时、设置与备份、不依赖 WPF
├─ GameHub.Data/        EF Core DbContext、Repository 实现、Migrations
├─ GameHub.App/         WPF 主程序（Views / ViewModels / Converters）
├─ GameHub.Core.Tests/  xUnit 单元测试（扫描黑名单过滤、打分排序、启动器守卫逻辑）
└─ installer/setup.iss  Inno Setup 安装包脚本
```

数据与缓存存放在 `%LocalAppData%\GameHub\`：

```
library.db          SQLite 数据库
icons\              自动提取的 exe 图标缓存
covers\             用户设置的自定义封面图
settings.json       主题 / 扫描黑名单等应用设置
logs\               预留日志目录
```

## 本地开发

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```bash
# 构建整个解决方案
dotnet build GameHub.sln

# 运行单元测试
dotnet test GameHub.Core.Tests/GameHub.Core.Tests.csproj

# 启动应用（调试模式）
dotnet run --project GameHub.App/GameHub.App.csproj
```

首次启动会自动在 `%LocalAppData%\GameHub\` 创建数据库并执行 EF Core 迁移。

## 打包与发布更新

需要 [Inno Setup 6](https://jrsoftware.org/isinfo.php)（`winget install JRSoftware.InnoSetup`）。版本号在 `GameHub.App/GameHub.App.csproj` 的 `<Version>` 里。

```powershell
# 测试 -> self-contained 单文件发布 -> 编译安装包 -> 生成 SHA-256
powershell -File scriptsuild-release.ps1
```

产物是 `dist\GameHubSetup-<版本>.exe`（同时也是"更新程序"：直接运行即可原地升级，设置和游戏库数据保留）。

### 应用内更新
设置页 →「更新」可以检查并一键安装新版本；启动时也会静默检查（可关闭）。它读取本仓库 GitHub Releases 的最新发布，要求：
1. Release 的 tag 形如 `v1.3.0`
2. 附件里有 `GameHubSetup-*.exe`
3. 完整性校验值：GitHub 会自动给附件生成 digest；若没有，请在 Release 说明里写一行 `SHA-256: <64位十六进制>`（`build-release.ps1` 末尾会打印）

下载后会校验 SHA-256，不匹配则丢弃，然后以静默模式运行安装包并自动重启 GameHub。

安装包已验证过完整的"静默安装 → 启动 → 卸载"流程；卸载时会询问是否同时删除 `%LocalAppData%\GameHub` 下的数据（静默卸载默认保留数据）。

## 已知局限

- **游玩时长统计**：部分游戏的主 exe 只是启动器，启动后很快退出并由子进程真正运行游戏，这种情况下时长统计会不准（但"上次启动时间"始终准确，因为它在点击启动的瞬间就被记录）。精确统计需要监控子进程树，见下方路线图。
- 暂不支持自动导入 Steam / Epic / GOG 等平台已安装的游戏库。

## 未来迭代计划

### 已完成（v1.1）
- [x] Windows 开始菜单快捷方式批量扫描导入
- [x] 启动后自动最小化到系统托盘
- [x] 分组拖拽排序、游戏卡片拖拽分组
- [x] 启动失败/路径失效的更友好提示（toast 而不是静默失败）
- [x] SteamGridDB 等在线接口抓取高清封面图

### 中期
- [ ] 自动导入 Steam / Epic / GOG /育碧 / 战网等平台已安装游戏库
- [ ] 基于子进程树（WMI/ETW）的精确游玩时长统计
- [ ] 游戏详情页支持评分、截图集
- [ ] 多设备间的库数据云同步

### 远期
- [ ] 手柄导航的"大屏模式"（Big Picture）
- [ ] 游玩数据统计看板（周/月活跃度、时长趋势图）
- [ ] 插件化的平台导入机制（类似 Playnite 的 library plugin）

欢迎提 Issue 或 PR。
