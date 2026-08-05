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
  Systems/
    VesselPositionSys/          飞船位置同步（Sender 读 KSP.Sim，Handler 入队，Update 插值写回）
    TimeSyncSys/                时间同步（NTP 风格，subspace 基础）
  VesselUtilities/
    VesselCommon.cs             KSP.Sim 飞船工具 + 远端飞船状态写入（TeleportSimObjectToOrbit）
  Patches/
    SpaceSimulationPatch.cs     可选：Harmony 挂 KSP2 仿真循环（默认不启用）
```

## 构建

1. 用 Visual Studio / `dotnet build` 打开 `LunaMultiplayer.KSP2.csproj`。
2. 把 csproj 里的 `KSP2GameDir` 指向你的 KSP2 安装目录（默认 `F:\Program Files\Epic Games\Kerbal.Space.Program.2`）。
3. 还原 NuGet 包 `Lidgren.Network`。
4. 编译产出 `LunaMultiplayer.KSP2.dll`，放入 KSP2 的 `BepInEx/plugins/` 下。

## ⚠️ 需对照 KSP2 源码确认的集成点（VERIFY）

代码已在本地通过反射测绘 API 后编写，但以下位置在真正编译/运行前需在 KSP2 源码里核对
（均已标注 `// VERIFY`）：

1. `SpaceSimulation.GetVesselGuids()` 返回集合的元素类型（应为 `IGGuid`）。
2. `IGGuid` 从 `System.Guid` 的构造/转换方式（`new IGGuid(g)` / `IGGuid.Parse` / 隐式转换）。
3. `ITransformModel.Rotation` 的类型与字段（`Rotation` 结构，含 `x,y,z,w`），以及其 setter 形态
   （属性赋值 or `UpdateRotation(Rotation)` 方法）。
4. `KeplerOrbitState` 构造参数顺序/类型（标准 KSP 为
   `inclination, eccentricity, semiMajorAxis, LAN, argumentOfPeriapsis, meanAnomalyAtEpoch, epoch, referenceBody`）。
5. `SpaceSimulation.UniverseModel` 是否实现 `IUniverseTime`（取 `UniverseTime`）。
6. `VesselComponent.OrbitalVelocity`（`Vector` 结构，含 `x,y,z`）。

服务端（host）侧目前复用 LMP 的 `Server` 项目（同为 Lidgren、引擎无关），后续可做 KSP2 专用轻量中继。

## 当前进度

- ✅ 网络层（Lidgren 客户端 + 收发线程 + 消息路由）
- ✅ 飞船位置同步（读 KSP.Sim 发送 / 接收入队 / FixedUpdate 写回）
- ✅ 时间同步（NTP 风格偏移估算）
- 🚧 插值（目前直接应用最新消息，插值队列已预留）
- 🚧 零件级资源同步（燃料/电量）、对接/分离、动作组
- 🚧 host 模式 / 专用服务端
