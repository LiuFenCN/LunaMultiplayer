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
4. 编译产出 `LunaMultiplayer.KSP2.dll`，放入 KSP2 的 `BepInEx/plugins/` 下。

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
