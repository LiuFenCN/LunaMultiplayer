# LunaMultiplayer KSP2（KSP2 联机 mod 适配）

基于 [LunaMultiplayer (KSP1 联机 mod)](https://github.com/LunaMultiplayer/LunaMultiplayer) 的网络层与架构，
为 **Kerbal Space Program 2** 做的联机适配。本仓库是 `LiuFenCN/LunaMultiplayer` 的 fork，`ksp2` 分支。

## 思路

- **网络层直接复用 LMP**：Lidgren 可靠 UDP、消息信封（`MessageBase`/`IMessageData`）、收发线程、
  `MessageSystem<T, TS, TH>` 泛型编排——这些与游戏引擎无关，原样沿用。
- **同步层重写**：LMP 读/写飞船状态靠 KSP1 的 `Vessel`/`ProtoVessel` API，KSP2 完全不同。
  本适配改为对接 **`KSP.Sim`**（`VesselComponent` / `PartComponent` / `PatchedConicsOrbit` /
  `SpaceSimulation`），API 已通过反射完整测绘（见 `KSP2_MP_DESIGN.md`）。
- **线格式兼容**：飞船位置消息数组布局（Orbit[8]、LatLonAlt[3]、SrfRelRotation[4]…）刻意与 LMP 一致，
  仅把 `BodyIndex` 换成 KSP2 的 `BodyGuid`（天体用 `IGGuid` 字符串标识）。

## 目录结构

```
LunaMultiplayer.KSP2/
  Plugin.cs                     BepInEx 入口：启动网络、启用系统、挂载主循环
  Core/
    Ksp2Logger.cs               日志（BepInEx ManualLogSource）
    Ksp2Time.cs                 统一游戏时间（SpaceSimulation.UniverseModel.UniverseTime）
    Ksp2Runner.cs               Unity 主循环驱动器（Update/Late/Fixed）
  Base/
    IMessageData / MessageTypeIdAttribute / MessageRegistry / ClientMessage
    SystemBase / MessageSystem / SubSystem / MessageHandlerBase / MessageSenderBase
  Network/
    NetworkMain / NetworkSender / NetworkReceiver / NetworkConnection / MessageRouter
    RelayServer.cs              轻量中继服务端（host 模式，Lidgren NetServer 字节级转发）
  Systems/
    VesselPositionSys/          飞船位置同步（Sender 读 KSP.Sim，Handler 入缓冲，FixedUpdate 时间插值写回）
    TimeSyncSys/                时间同步（NTP 风格，subspace 基础）
    VesselResourceSys/          零件级资源同步（燃料/电量等，按 partGuid+resourceName 写回容器）
    VesselActionGroupSys/       动作组同步（声明顺序位掩码编码 KSPActionGroup，Get/SetActionGroupState）
    VesselStructureSys/         对接/分离/级间分离同步（轮询 PartOwnerComponent.PartCount 检测结构变化）
  VesselUtilities/
    VesselCommon.cs             KSP.Sim 飞船工具 + 远端状态写入（TeleportSimObjectToOrbit / SetResourceStoredUnits）+ 天体引力参数/零件数缓存
  Patches/
    SpaceSimulationPatch.cs     可选：Harmony 挂 KSP2 仿真循环（默认不启用）
```

## 构建

1. 用 Visual Studio / `dotnet build` 打开 `LunaMultiplayer.KSP2.csproj`。
2. 把 csproj 里的 `KSP2GameDir` 指向你的 KSP2 安装目录（默认 `F:\Program Files\Epic Games\Kerbal.Space.Program.2`）。
3. 还原 NuGet 包 `Lidgren.Network`。
4. 编译产出 `LunaMultiplayer.KSP2.dll` + `swinfo.json` + `lib/Lidgren.Network.dll`（见下方安装布局）。

## 安装（KSP2 Redux / SpaceWarp2 环境）

Redux 版 KSP2 没有启用 BepInEx 链式加载器（缺 `winhttp.dll`），第三方 mod 由 Redux 内置的
mod 目录 **`mods/<ModName>/swinfo.json`** 发现并加载。dll **不能直接丢在 `BepInEx/plugins\` 根**，
必须放进游戏根目录的 `mods\<ModName>\` 子目录：

> ⚠️ **入口类必须是 SpaceWarp2 模块，且 swinfo 必须声明 `main_assembly`。**
> 1. `Plugin` 继承自 `SpaceWarp2.API.Mods.GeneralMod`（实现 `ISpaceWarpMod`），
>    初始化逻辑写在重写的 `OnInitialized()` 里（不是 `Awake()`）。
> 2. `swinfo.json` **必须**含 `"main_assembly": "LunaMultiplayer.KSP2.dll"`。
>    缺失该字段时 SpaceWarp 会把 mod 当成 `AssetOnlyMod`（仅资源、不实例化、不调用 `OnInitialized`），
>    表现为日志里「Registered plugin」出现、但没有任何 `[LMP2]` 输出、`Initialization completed in 0.0000s`。
> 3. 依赖程序集（如 `Lidgren.Network.dll`）必须放在 mod 子目录的 **`lib/`** 下——
>    SpaceWarp 的 `RegisterMods` 只预加载 `<ModDir>/lib/*.dll`。入口 dll 由 `main_assembly` 指定。
>    （此外 `Plugin` 静态构造里挂了 `AssemblyResolve` 回退， doubly 保证 Lidgren 可在任意加载上下文解析到。）

```
mods/LunaMultiplayer.KSP2/
├── LunaMultiplayer.KSP2.dll      ← main_assembly 指向它
├── swinfo.json
└── lib/
    └── Lidgren.Network.dll       ← 依赖放 lib/，SpaceWarp 会预加载
```

一键安装（管理员 PowerShell）：

```powershell
$src = 'F:\缓存\软件缓存\workboddy\2026-08-02-17-40-23\ksp2_mp\LunaMultiplayer.KSP2\bin\Debug\netstandard2.1'
$mods = 'F:\Program Files\Epic Games\Kerbal.Space.Program.2\mods'
$dst = "$mods\LunaMultiplayer.KSP2"
# 清理旧的错误放置（BepInEx/plugins 根目录、以及旧版散落的 Lidgren）
$oldPlugins = 'F:\Program Files\Epic Games\Kerbal.Space.Program.2\BepInEx\plugins'
Remove-Item "$oldPlugins\LunaMultiplayer.KSP2.dll" -ErrorAction SilentlyContinue
Remove-Item "$oldPlugins\Lidgren.Network.dll"      -ErrorAction SilentlyContinue
Remove-Item "$dst\Lidgren.Network.dll"             -ErrorAction SilentlyContinue   # 旧版在根目录的
New-Item -ItemType Directory -Force -Path "$dst\lib" | Out-Null
Copy-Item "$src\LunaMultiplayer.KSP2.dll" $dst -Force
Copy-Item "$src\swinfo.json"              $dst -Force
Copy-Item "$src\lib\Lidgren.Network.dll"  "$dst\lib" -Force
Write-Host "已安装到 $dst :"; Get-ChildItem $dst -Recurse
```

> 注意：游戏程序集（`Assembly-CSharp`/`UnityEngine.CoreModule`）本身编译目标就是 netstandard 2.1，
> 所以本 mod **必须**用 `netstandard2.1`（改 2.0 会与游戏程序集版本冲突，CS1705）。
> 启动后看 `Ksp2.log` 里 `[Space Warp] Registered plugin: LunaMultiplayer KSP2` 即表示注册成功；
> 再看是否有 `[LMP2] >>> OnInitialized ENTRY` 以确认 `OnInitialized()` 实际执行。

## ⚠️ 集成点核对（VERIFY）

代码先通过反射测绘 KSP2 API 后编写，csproj 直接引用本机 KSP2 的 `Assembly-CSharp.dll` 等游戏程序集，
因此以下位置已由 `dotnet build` **编译期坐实**（不再需要运行时核对）：

1. ✅ `SpaceSimulation.GetVesselGuids()` 返回 `ICollection<string>`（GUID 字符串，非 `IGGuid`）——代码中用 `IGGuid.TryParse` 转换。
2. ✅ `IGGuid` 转换：`IGGuid.TryParse(string, out IGGuid)` / 隐式 `Guid↔IGGuid`。
3. ✅ `ITransformModel.Rotation`（`Rotation` 结构，含 `x,y,z,w`）。
4. ✅ `KeplerOrbitState` 构造参数顺序/类型。
5. ✅ `SpaceSimulation.UniverseModel` 实现 `IUniverseTime`，取 `UniverseTime`。
6. ✅ `VesselComponent.OrbitalVelocity`（`Vector` 结构，含 `x,y,z`）。
7. ✅ 资源同步：`GameManager.Instance.Game.ResourceDefinitionDatabase` 访问器；
   `CelestialBodyComponent.gravParameter`（2 体传播用 μ）；
   零件资源容器 `PartResourceContainer` / `SetResourceStoredUnits`。

> 注意：全局游戏访问器是 `KSP.Game.GameManager.Instance.Game`（`GameManager` 有静态 `Instance`，
> 返回 `GameInstance`），**不是** `GameInstance.Instance`——`GameInstance` 是 `MonoBehaviour`，无静态实例。

## 联机模式（host / 客户端）

- **房主（host）**：在 BepInEx 配置文件里设 `HostMode=true`、`HostPort=8800`，启动即开房并起轻量中继服务端；
  房主自身也通过 loopback 接入，复用同一套收发管线。
- **玩家（客户端）**：设 `ServerAddress=房主IP`、`ServerPort=8800`，启动自动连；或运行时调用
  `NetworkConnection.Connect(ip, 8800)`。
- **中继模型**：星型拓扑，服务端只做 `ClientMessage` 字节级转发（回弹抑制：不把消息发回发送者），
  每个客户端各自跑自己的 KSP2 仿真，靠状态广播实现 co-op。当前为轻量中继，不做服务端权威仲裁。

## 当前进度

- ✅ 网络层（Lidgren 客户端 + 收发线程 + 消息路由）
- ✅ 飞船位置同步（读 KSP.Sim 发送 / 接收入缓冲 / 时间插值写回）
- ✅ 时间同步（NTP 风格偏移估算）
- ✅ 飞船位置时间插值（缓冲样本 + 2 体平近点角传播 n=√(μ/a³) + 朝向 Slerp，延迟 200ms）
- ✅ 零件级资源同步（燃料/电量等，按 partGuid+resourceName 写回容器，2Hz 节流）
- ✅ 对接/分离、动作组同步（VesselStructureSys + VesselActionGroupSys）
- ✅ host 模式 / 轻量中继服务端（RelayServer + 配置驱动切换）
