# Win11 Tools / Windows11 轻松设置 — 开局速览

> 每会话自动注入，只放"必须先知道"的。详细设计说明见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

## 1. 项目定位

做"一键设置 / 优化 Windows 10&11"的中文图形工具。技术栈 .NET 10 + WinUI 3，
非打包 + 自包含分发。

- **仓库**：`C:\Users\Voheart\source\repos\Win11 Tools New`（唯一仓库，无外部依赖项目）
- **远程**：`https://github.com/Vohrt/Win11-Tools.git`（**公开**）
- **当前版本**：`1.2.0`（`Directory.Build.props` / `app.manifest`），最新 tag `v1.2.0`

## 2. 仓库布局

`Win11EasyConfig.slnx`（仅 x64）+ `Directory.Build.props` + `global.json` + `.github/workflows/release.yml`
+ `src/` + `tests/`：

- **Core**（纯逻辑，零 UI 依赖）
  - `Abstractions/` — `IRegistryService` / `ICommandRunner` / `IWindowsServiceManager`
  - `Services/` — 注册表、命令执行、服务管理、系统信息、Shell 操作的实现
  - `Models/` — `TweakState` / `TweakCategory` / `EffectScope`
  - `Tweaks/` — 声明式调整项引擎（`ITweak` + `RegistryTweak` / `ServiceTweak` / `CommandTweak` / `TweakFactory`）
  - `Settings/` — 下拉 / 数值 / 动作三类设置的抽象
  - `Catalog/` — **所有设置"即数据"**（`SystemTweaks` / `AdvancedTweaks` / `ExplorerTweaks` /
    `ExplorerExtras` / `PrivacyTweaks` / `PrivacyExtras` / `PerformanceTweaks` / `TweakCatalog`）
  - `Features/` — `EdgeService` / `AppsService` / `HardwareInfoService`
- **App**（WinUI 3）
  - `AppHost.cs`（DI 组合根）`MainWindow`（NavigationView + 路由）`RuntimeBootstrap.cs` `app.manifest`
  - `ViewModels/`（`CategoryPage` / `Edge` / `Apps` / `SystemInfo` + `Settings/*` 四类卡片 VM）
  - `Views/`（`CategoryPage` / `EdgePage` / `AppsPage` / `SystemInfoPage`）
- **tests**（xunit v3 + Microsoft.Testing.Platform）`Win11EasyConfig.Core.Tests` — **34 用例**

**一句话架构**：新增一项设置 = 在 `Catalog/` 加一行，不用碰任何 XAML。

## 3. 当前状态（2026-10-18 实测）

| 项 | 值 |
|---|---|
| 分支 / 提交数 | `master` / 25 |
| 最后提交 | `c859a3a`（= tag `v1.2.0`），已推送，与 origin 同步 |
| 工作树 | 干净 |
| 构建 | **0 警告 0 错误**（SDK 10.0.401） |
| 测试 | **34 / 34 通过** |
| 设置项 | **90 项** = 57 开关 + 6 下拉 + 2 数值 + 25 动作 |

**设置项分布**：System 8 / Advanced 7 / Explorer 14 / Taskbar 6 / Privacy 11 / Performance 10 / Network 1。
**Windows 服务**：`SysMain`、`PcaSvc`、`DPS`。**唯一只读项**：`performance.appPrefetch`。

**导航页（10 项）**：系统设置 / 高级设置 / 资源管理器 / 任务栏 / 隐私 / 性能与远程 / 网络 →
Edge 管理 / 应用商店 → 系统信息。

## 4. 构建与发布（实测通过）

```powershell
# 构建（含 tests 工程）
dotnet build Win11EasyConfig.slnx -c Release        # 0 警告 0 错误
# → src\Win11EasyConfig.App\bin\Release\net10.0-windows10.0.19041.0\win-x64\Win11EasyConfig.exe

# Core 单元测试（xunit v3 / MTP）
dotnet test --project tests/Win11EasyConfig.Core.Tests -c Release   # 34 通过

# 独立版（自包含，解压即用）：文件夹 186 MB → zip ~71 MB
dotnet publish src/Win11EasyConfig.App/Win11EasyConfig.App.csproj -c Release -r win-x64 `
  --self-contained true  -p:WindowsAppSDKSelfContained=true  -p:DebugType=none "-p:Version=1.2.0" -o publish/standalone

# 精简版（需目标机装 .NET 10 桌面运行时 + Windows App SDK 运行时）：文件夹 ~39 MB → zip ~10.4 MB
dotnet publish src/Win11EasyConfig.App/Win11EasyConfig.App.csproj -c Release -r win-x64 `
  --self-contained false -p:WindowsAppSDKSelfContained=false -p:DebugType=none "-p:Version=1.2.0" -o publish/runtime-dependent
```

**发版**：Actions → `发布新版本` → Run workflow → 填版本号（可勾预发布）；推 `v*` 标签也触发。
工作流在 `windows-latest` 跑上面两条 publish → `Compress-Archive` 成 zip → 内置 `gh` CLI 建 Release。

## 5. 必须知道的坑

1. **`requireAdministrator` 是硬需求**（`app.manifest`）。做 UI 冒烟测试若临时改成 `asInvoker`，
   **测完必须换回并确认，绝不能提交 asInvoker**。
2. **`EnableMsixTooling=true` 必须常开**：它生成有效 `resources.pri`；缺了它，`dotnet publish`
   的产物会以 `0xC000027B` 启动即崩。
3. **WinUI 3 单文件发布不可靠**：本机能跑、CI 产物可能损坏，且本地无法复现。分发一律
   **"发布文件夹 → zip"**，不要为小体积回退到单文件。
4. **slnx 平台映射三件套缺一不可**：props 的 `<Platforms>x64</Platforms>` + slnx 的
   `<Platform Name="x64"/>` + **每个项目**的 `<Platform Project="x64"/>`；少一个 VS 就报
   "project configuration does not exist"。`AppendPlatformToOutputPath=false` 统一输出路径。
5. **ML 运行时剔除必须两个开关同进同退**：`RemoveUnusedWindowsMLRuntime` 目标（按文件名 +
   NuGet 包 id 剔除 onnxruntime / DirectML / Microsoft.Windows.AI.MachineLearning，约 38 MB）
   + `DisableWindowsAppSDKMLAutoInitialize=true`；`StripUnusedMLRuntime`（默认 true）可关。
6. **本机环境**：`ProductName` 报 "Windows 10 IoT Enterprise LTSC 2024"，实际 `CurrentBuild=26300`（26H2）。
   `explorer.win10Style` 的门控条件是"**仅 build 22000 不支持**"，故本机可用——**不是 bug**。
7. **本机没装 Windows App SDK 运行时**：所以"精简版"在本机**必然**起不来，属预期。
8. **中文 + PowerShell 5.1**：带中文路径的 `.ps1` 必须存 UTF-8 **with BOM**，否则中文路径被当 ANSI 读成乱码。
9. **git CRLF 虚报**：提交统计里删除行数虚高是 `autocrlf` 归一化，用 `--numstat` 核对内容即可。
10. **Edge 安装必须用 c2rsetup 直链**（`c2rsetup.officeapps.live.com` / `c2rsetup.edog.officeapps.live.com`）。
    早期用的 `go.microsoft.com/fwlink/?linkid=2069148` 实测返回 267 MB 企业版 `.pkg`（XAR 归档，
    **不能**当安装器执行）。
11. **`dotnet test` 若静默挂起 / Access Denied**：查仓库目录的完整性标签（见第 14 条）。
12. **⚠️ 完整性标签**：若仓库目录被施加 `Mandatory Label\Low Mandatory Level`，全部子进程会以
    Low 完整性运行——症状：应用商店页全部显示"未安装"（`Get-AppxPackage` Access Denied）、
    `dotnet test` 挂起。诊断 `icacls <目录>`；修复
    `icacls "<目录>" /setintegritylevel '(OI)(CI)M' /T /C`（恢复 Medium）。
13. **`bin/obj` 里可能有已删功能的陈旧生成物**：源码 grep 为准，别把构建缓存误判成"没删干净"。
    `dotnet clean` 或删 `bin`/`obj` 即可。

## 6. 待办

无


## 7. 新会话开局第一步

```powershell
cd "C:\Users\Voheart\source\repos\Win11 Tools New"
git status --short --branch          # 期望干净、与 origin 同步
git log --oneline -5
dotnet build Win11EasyConfig.slnx -c Release                              # 期望 0 警告 0 错误
dotnet test --project tests/Win11EasyConfig.Core.Tests -c Release          # 期望 34 通过
```

1. 先构建 + 跑测试，确认基线。
2. 读 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) 了解架构约定（新增设置、卡片类型、状态回读等）。
3. 改代码前先确认需求；**删除功能类改动必须先问用户**。
