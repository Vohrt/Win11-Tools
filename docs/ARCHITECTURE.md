# 架构说明

本文档描述 **Windows11 轻松设置**（`Win11EasyConfig`）的设计约定，供后续开发参考。
开局速览见 [AGENTS.md](../AGENTS.md)。

## 1. 总体结构

三个工程，依赖方向单向向下：

```
Win11EasyConfig.App  (WinUI 3, 可执行)
        │
        ▼
Win11EasyConfig.Core (纯逻辑, 零 UI)
        ▲
        │
Win11EasyConfig.Core.Tests (xunit v3)
```

**Core 不引用任何 UI 程序集**。它只依赖 `System.Management` 与
`System.ServiceProcess.ServiceController` 两个包，因此可以独立引用与单元测试。

| 工程 | 目标框架 | 说明 |
|---|---|---|
| `Win11EasyConfig.Core` | `net10.0-windows10.0.19041.0` | 全部业务逻辑 |
| `Win11EasyConfig.App` | 同上 | WinUI 3 外壳，`AssemblyName=Win11EasyConfig` |
| `Win11EasyConfig.Core.Tests` | 同上 | xunit v3，`PlatformTarget=x64` |

## 2. 核心抽象

### 2.1 服务接口（`Core/Abstractions/`）

| 接口 | 职责 | 实现 |
|---|---|---|
| `IRegistryService` | 注册表读写（int / string / binary / 子键枚举 / 删树） | `RegistryService` |
| `ICommandRunner` | 执行 cmd / PowerShell、启动进程 | `CommandRunner` |
| `IWindowsServiceManager` | 查询与控制 Windows 服务 | `WindowsServiceManager` |

全部通过 DI 注入。测试用假实现替换（见 `tests/…/Fakes.cs`），因此 Core 的测试**不触碰真实系统**。

### 2.2 `ITweak` —— 单个开关

```csharp
public interface ITweak
{
    string Id { get; }                       // 全局唯一，形如 "explorer.showFileExtensions"
    string Title { get; }                    // 界面标题
    string? Description { get; }
    TweakCategory Category { get; }          // 决定出现在哪个导航页
    EffectScope Effect { get; }              // Immediate / RestartExplorer / SignOut / Reboot
    bool IsReadOnly { get; }                 // 只读项：界面禁用、只展示状态
    IReadOnlyList<string> RequiresOn { get; }// 依赖的其它 tweak id，未满足时禁用
    bool IsSupported { get; }                // 版本门控
    string? UnsupportedReason { get; }
    Task<TweakState> GetStateAsync(...);     // 读真实状态
    Task<TweakApplyResult> ApplyAsync(bool enable, ...);
}
```

`TweakState` 三态：`Unknown` / `On` / `Off`。

### 2.3 引擎实现（`Core/Tweaks/`）

| 类型 | 适用场景 |
|---|---|
| `RegistryTweak` | 由「探测函数 + 开/关写入集」描述的任何注册表项 |
| `ServiceTweak` | Windows 服务启停（`probeRunningStatus` 可按"是否运行"判定） |
| `CommandTweak` | 由命令行输出决定状态的项 |
| `TweakBase` | 上述类型的基类，提供 `Description` / `Effect` / `RequiresOn` 等属性的默认实现 |

`TweakFactory` 提供三种简写，覆盖绝大多数场景：

```csharp
// 值型：开=写 onValue，关=写 offValue
factory.Value("system.ceip", "…", TweakCategory.System, Key, "CEIPEnable", onValue: 0, offValue: 1, …);

// 策略型：开=删除策略值（回到系统默认），关=写 offValue
factory.Policy("system.restore", "…", TweakCategory.System, Key, "DisableSR", offValue: 1, …);

// 完全自定义：探测函数 + 任意写入集
factory.Custom("performance.remoteDesktop", "…", TweakCategory.Performance,
    reg => … ? TweakState.On : TweakState.Off,
    [RegistryWrite.Int(Key, "fDenyTSConnections", 0)],
    [RegistryWrite.Int(Key, "fDenyTSConnections", 1)], …);
```

需要非注册表语义时（例如读写二进制 blob、调用 PowerShell），直接继承 `TweakBase` 写一个专用类——
参考 `AutoHideTaskbarTweak`（`Catalog/ExplorerTweaks.cs`）。

### 2.4 三类非开关设置（`Core/Settings/`）

| 接口 | 说明 | 例 |
|---|---|---|
| `IChoiceSetting` | 下拉单选，每个选项有自己的读写逻辑 | `explorer.launchTo`、`taskbar.alignment` |
| `INumericSetting` | 数值输入，带 `Minimum` / `Maximum` | `performance.maxPrefetchFiles`、`performance.rdpPort` |
| `IActionCommand` | 一次性动作按钮，按 `GroupName` 分组 | `action.restartExplorer`、`action.flushDns` |

## 3. 目录即数据（`Core/Catalog/`）

**所有设置集中声明在这里，界面不含任何硬编码清单。**

| 文件 | 内容 |
|---|---|
| `SystemTweaks.cs` | System 分类 8 项 |
| `AdvancedTweaks.cs` | Advanced 7 项 + Network 1 项（`advanced.bbr2`） |
| `ExplorerTweaks.cs` | Explorer 14 项 + Taskbar 6 项（含 `AutoHideTaskbarTweak`） |
| `ExplorerExtras.cs` | 下拉 2 项 + 动作 15 项（资源管理器操作 / 快速打开） |
| `PrivacyTweaks.cs` | Privacy 11 项 |
| `PrivacyExtras.cs` | 动作 4 项（Windows 搜索防火墙） |
| `PerformanceTweaks.cs` | Performance 10 项 + 数值 2 项 + 动作 6 项（网络与诊断） |
| `TweakCatalog.cs` | 汇总入口，`Lazy` 构建四个集合 |

`TweakCatalog` 对外暴露：

```csharp
AllTweaks / AllChoices / AllNumerics / AllActions      // 全量
TweaksIn(category) / ChoicesIn / NumericsIn / ActionsIn // 按分类过滤
FindTweak(id)                                           // 按 id 查找
```

### 3.1 新增一项设置的步骤

1. 在对应 `Catalog/*.cs` 的 `Create(...)` 里 `yield return` 一条数据（或用 `factory.Value/Policy/Custom`）。
2. 若需要新的分类行为，改 `TweakCategory` 枚举 + `MainWindow.xaml` 导航项 + `MainWindow.xaml.cs` 路由。
3. 在 `tests/…/CatalogIntegrityTests.cs` 更新计数断言。
4. 跑 `dotnet test` 确认。

**不需要改任何 XAML。**

## 4. 界面层（`App/`）

### 4.1 组合根

`AppHost.Initialize()` 用 `Microsoft.Extensions.DependencyInjection` 注册所有服务与 ViewModel，
`AppHost.GetRequiredService<T>()` 取用。新增服务记得在这里注册。

### 4.2 导航

`MainWindow.xaml` 的 `NavigationViewItem.Tag` 决定去向：

- `System` / `Advanced` / `Explorer` / `Taskbar` / `Privacy` / `Performance` / `Network`
  → 解析为 `TweakCategory`，导航到 `CategoryPage`
- `Edge` → `EdgePage`；`Apps` → `AppsPage`；`SystemInfo` → `SystemInfoPage`

### 4.3 四类卡片

`CategoryPage` 是唯一的通用设置页，按分类从目录取数据，渲染四种卡片
（ViewModel 在 `App/ViewModels/Settings/`）：

| 卡片 | ViewModel | 交互 |
|---|---|---|
| 开关 | `ToggleSettingViewModel` | 点击 → `ApplyAsync` → **回读真实状态** |
| 下拉 | `ChoiceSettingViewModel` | 选择 → 写入 → 回读 |
| 数值 | `NumericSettingViewModel` | 输入 → 校验范围 → 写入 → 回读 |
| 动作分组 | `ActionCommandViewModel` | 按钮点击，按 `GroupName` 分组显示 |

### 4.4 状态回读原则

**每次修改后必须重新调用 `GetStateAsync()` 回填界面**，而不是假定写入成功。
若写入被系统策略拦截（如 UCPD 驱动），开关会弹回原状并显示错误，避免
"界面显示已改、实际没改"。

### 4.5 联动与门控

- `RequiresOn`：依赖项未开启时，卡片置灰。由 `CategoryPageViewModel` 实时重算。
- `IsReadOnly`：只展示、不可改（当前唯一：`performance.appPrefetch`）。
- `IsSupported` + `UnsupportedReason`：版本门控（如 `explorer.win10Style` 在 build 22000 上禁用）。

## 5. 功能服务（`Core/Features/`）

| 服务 | 职责 |
|---|---|
| `EdgeService` | Edge 各通道版本检测 / 安装 / 卸载；WebView2 管理；禁止自动更新 |
| `AppsService` | 应用商店 61 项清单的检测 / 安装 / 卸载，支持「当前用户」与「系统预装」双作用域 |
| `HardwareInfoService` | WMI 读取 CPU / 内存条 / 显卡 / 硬盘 |

`AppsService` 的检测策略：一次 `Get-AppxPackage` 拿到全部用户级包，再在本地按
`PackagePrefix` 匹配——比逐个探测快得多。系统预装包走 `AppxAllUserStore` 注册表枚举。

## 6. 测试

`tests/Win11EasyConfig.Core.Tests`，xunit v3 + Microsoft.Testing.Platform（运行器在 `global.json` 指定）。

| 文件 | 覆盖 |
|---|---|
| `CatalogIntegrityTests` | 90 项总量、分类分布、id 唯一性、`RequiresOn` 引用有效、只读契约、数值范围 |
| `AutoHideTaskbarTweakTests` | 二进制 blob 的奇偶判定、位翻转保真、幂等、失败路径 |
| `RegistryTweakTests` | `Value` / `Policy` / `Custom` 的探测与写入语义 |
| `StoreCatalogTests` | 61 项清单、名称/StoreId 唯一、前缀匹配 |
| `Fakes.cs` | `FakeRegistryService`（记录写入）/ `FakeCommandRunner` / `FakeWindowsServiceManager` |

运行：

```powershell
dotnet test --project tests/Win11EasyConfig.Core.Tests -c Release
```

## 7. 构建与分发

见 [AGENTS.md §4](../AGENTS.md)。要点：

- 仅 **x64**；平台映射三件套（props / slnx 解决方案级 / slnx 每项目）缺一不可。
- 非打包（`WindowsPackageType=None`）+ 自包含 + `EnableMsixTooling=true`。
- 分发形态是**发布文件夹压缩成 zip**，不用单文件（WinUI 3 单文件在 CI 上不可靠）。
- `RemoveUnusedWindowsMLRuntime` 剔除未使用的 ML 运行时（约 38 MB）。

## 8. 约定与注意事项

1. **新增设置优先用 `Catalog/` 数据声明**，不要为单个开关写页面。
2. **id 命名**：`<分类小写>.<驼峰名>`，如 `explorer.showFileExtensions`。全局唯一。
3. **`Description` 要写清楚代价**，这是用户做决定的唯一依据。
4. **`Effect` 要如实标注**：需要重启/注销/重启资源管理器的，别标成 `Immediate`。
5. **删除功能是产品决策**，必须先与用户确认。
6. **不要引入外部镜像或第三方下载源**。
