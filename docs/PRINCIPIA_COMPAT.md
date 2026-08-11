# Principia / N 体物理 Mod 联机兼容方案（LMP fork）

> 分支：`feature/principia-compat`
> 目标：让安装 Principia（N 体物理）以及 Kopernicus/OPM（自定义星系）的客户端能在 LMP 下联机。

## 0. 实现状态（2026-08-11）

- ✅ **阶段 A 代码已落地并推到本分支**（5 个文件）：
  - `LmpCommon/Message/Data/Vessel/VesselPositionMsgData.cs` — 协议扩展：`NBodyMode` 字节 + `WorldPosition[3]`/`WorldVelocity[3]`（末尾追加、向后兼容，老消息可正常反序列化）。
  - `LmpClient/Systems/VesselPositionSys/VesselPositionMessageSender.cs` — 发送端：off-rails 且在轨的飞船改发世界坐标（`vessel.GetWorldPos3d()` / `vessel.obt_velocity`）。
  - `LmpClient/Systems/VesselPositionSys/VesselPositionUpdate.cs` — 接收端：拷贝 NBodyMode/World* 字段。
  - `LmpClient/Systems/VesselPositionSys/ExtensionMethods/VesselPositioner.cs` — 应用端：`NBodyMode==1` 时走 `ApplyNBodyVesselPosition`，直接驱动世界变换、**不强制 on-rails**，让本地积分器接管。
  - `LmpClient/Systems/VesselUpdateSys/VesselUpdate.cs` — 全量同步：off-rails 且在轨的船跳过 `orbitDriver` 强制 IDLE/UPDATE。
- ⚠️ **未做（阶段 A 范围内故意留白）**：握手阶段的"全队 Principia 版本一致性检测"（目前靠"所有客户端都装同版本 Principia"的约定保证，未做代码强制）。阶段 B（warp）、阶段 C（健壮性/版本校验/重连校正）未做。
- ⚠️ **编译与联机验证必须在用户真机完成**：见第 6 节。沙箱（本 AI 环境）只有 .NET 10 SDK，而 LmpClient 目标框架是 .NET Framework 4.7.2、且硬引用 KSP 的 `Assembly-CSharp.dll`/`UnityEngine.*.dll`（不进 git），**无法在沙箱编译或运行**。

## 0.1 触发条件（实现细节）
发送端用与接收端一致的启发式判定 N-body 船：`!vessel.packed && situation ∈ [ORBITING, ESCAPING] && !Landed && !Splashed`。即"off-rails 且在轨"的船走世界坐标同步。这是积分器无关的判定，Principia 的船天然命中；地面/水面船仍走原二体路径，不影响。

## 1. 当前为什么会崩（根因）

LMP 的飞船同步是 **服务端权威的二体轨道模型**：

- 客户端 `VesselPositionSystem` 每帧广播飞船的 **KSP 开普勒轨道根数（Orbit[8]）+ 位置/速度/姿态**（`LmpClient/Systems/VesselPositionSys/VesselPositionUpdate.cs`）。
- 服务端存权威状态，转发给其他客户端。
- 接收端 `VesselUpdate.ProcessVesselUpdate` 把远程飞船强制：
  ```csharp
  vessel.orbitDriver.SetOrbitMode(OrbitDriver.UpdateMode.IDLE); // = on-rails，用二体轨道根数
  ```
  即把飞船"钉"在二体开普勒轨道上。

Principia 的做法相反：它把飞船设为 **off-rails（N 体积分）**，飞行轨迹由 `principia.dll` 用牛顿力学 + 多体引力实时积分得到，**完全不用 KSP 开普勒根数**。于是：

- LMP 用开普勒根数同步 → 位置与 Principia 的 N 体轨迹不一致 → 进入 Principia 引力显著区域（近行星、转移轨道）就跳变/卡死 → 服务端 `Connection timed out`。
- `SetOrbitMode(IDLE)` 进一步覆盖 Principia 积分，等于强行把 N 体飞船拉回二体轨道，冲突必然发生。

结论：**不是白名单问题，是同步模型与 N 体积分根本不兼容。** 光加 dll 白名单解决不了。

## 2. 可行方案：N 体分布式同步模式（NBodyMode）

Principia 的积分是**确定性**的：相同初始条件 + 相同物理步 + 相同天体配置 → 所有客户端得到完全相同的轨迹。因此不需要服务端做轨道权威，改为：

> **所有联机客户端都装同版本 Principia 时，进入 NBodyMode：**
> - 服务端不再对飞船位置做二体权威，只转发"绝对世界坐标状态 + 控制输入"。
> - 每个客户端收到他人飞船的绝对位置/速度/姿态后，在自己本地的 Principia 世界里积分/插值。
> - 本地的自己飞船由本地 Principia 正常积分，只广播控制输入（油门、RCS、姿态指令）和绝对状态。

因为初始条件一致、Principia 积分确定，大家对同一飞船看到的轨迹一致 → 联机可用。

### 2.1 为什么服务端不需要跑 Principia
LMP 服务端 `Server.exe` 是 headless，不加载 KSP/GameData，无法实例化 Principia。但 NBodyMode 下服务端只需做**消息中继 + 时间/子空间协调**，不做天体物理，所以不依赖 Principia。天体（行星）的 N 体积分由每个客户端本地 Principia 各自完成，只要所有客户端 Kopernicus/OPM 配置一致，行星位置一致，飞船相对位置叠加后也一致。

### 2.2 Kopernicus / OPM 的联机条件
Kopernicus/OPM 改的是**天体**而非飞船。联机前提：**所有客户端安装完全相同的 Kopernicus + OPM/OPX 配置**（用户已满足：朋友 mod 都是他发的）。服务端 Universe 只存 scenario 数据，不定义天体，不影响。闪退真因是之前客户端 Principia 半加载损坏 + Universe 被清档成原版，现已排障。

## 3. 实施计划（分阶段）

### 阶段 A — MVP：1x 实时 N 体联机（先做这个）
范围：**所有客户端 warp=1（不时间加速）**，能看到对方飞船在 Principia 轨迹上的正确位置。
1. **能力检测**：握手/ModControl 里检测所有客户端是否都含 `Principia.dll`；是则全队进入 NBodyMode（否则回退原二体模式，保证向后兼容）。
2. **协议扩展**：`VesselPositionMsgData` 增加 `NBodyMode` 标志 + `WorldPosition[3]`/`WorldVelocity[3]`（绝对世界坐标，替代 Orbit[8] 的权威用途）。
3. **发送端**（`VesselPositionMessageSender`）：NBodyMode 下发世界坐标状态（来自 Principia 积分后的 `vessel.GetWorldPos3D()` / `vessel.obt_velocity`）。
4. **接收端**（`VesselPositionUpdate`）：NBodyMode 下**不调用 `SetOrbitMode(IDLE)`**，直接用收到的世界坐标驱动本地 Principia 积分 / 插值，飞船保持 off-rails。
5. **VesselUpdate**：NBodyMode 下跳过 `orbitDriver` 强制 on-rails。
- 交付：编译通过的原型 + 文档，需用户在真机（有 KSP GUI）联机验证 1x 模式。

### 阶段 B — 时间加速（warp）兼容
N 体下时间加速会放大积分漂移。方案：NBodyMode 下限制 warp ≤ 某阈值，或对绝对状态做周期性硬校正（每 N 秒用收到的世界坐标 reset 一次本地积分），抑制累积误差。待阶段 A 验证后做。

### 阶段 C — 健壮性
- 客户端 Principia 版本不一致时拒绝联机（握手阶段提示）。
- 掉线重连后状态校正。
- 与 FreeIva/KIS 等物理 mod 的协同（这些仍可能不同步，文档标注）。

## 4. 风险与边界
- **完整 Principia 联机（含高 warp）是社区多年未解的难题**，阶段 A 的 1x 实时 MVP 是现实可达的目标，warp 支持需迭代。
- 源码改造需完整 LMP 树编译验证；沙箱无 KSP GUI 不能跑联机，运行时联调必须由用户在真机完成。
- Kopernicus/OPM 只需"全队一致"，无需改代码（已通过 ModControl 一致性保证）。

## 5. 涉及的源码位置（已定位）
- 同步发送：`LmpClient/Systems/VesselPositionSys/VesselPositionMessageSender.cs`
- 同步接收：`LmpClient/Systems/VesselPositionSys/VesselPositionUpdate.cs`
- 远程飞船 on/off-rails 强制：`LmpClient/Systems/VesselUpdateSys/VesselUpdate.cs`（SetOrbitMode）
- 轨道模式事件：`LmpClient/Harmony/Vessel_GoOnRails.cs` / `Vessel_GoOffRails.cs`
- 协议定义：`LmpCommon/Message/Data/Vessel/VesselPositionMsgData.cs`
- 能力检测：`LmpClient/Systems/ModSys`（ModControl dll 列表）

## 6. 编译与联机验证（必须在真机做）

> LMP 是 KSP 游戏 mod，强依赖 KSP 程序集。**沙箱/CI 无法编译**，以下步骤在用户装有 KSP 的 Windows 上做。

### 6.1 准备 KSP 程序集（一次性）
LMP 仓库的 `LmpClient/LmpClient.csproj` 引用 `..\External\KSPLibraries\*.dll`（KSP 本体 DLL，不进 git）。需要把 KSP 的以下文件复制到仓库 `External\KSPLibraries\`：
- `KSP_x64_Data\Managed\Assembly-CSharp.dll`
- `KSP_x64_Data\Managed\UnityEngine*.dll`（CoreModule / PhysicsModule / UI / IMGUIModule / InputLegacyModule / AnimationModule / ImageConversionModule / TextRenderingModule / UnityWebRequestModule 等）
- `KSP_x64_Data\Managed\System.dll`、`System.Xml.dll`
外加 `External\Dependencies\Harmony\000_Harmony\0Harmony.dll`（LMP 自带或手动放置）。

### 6.2 用 Visual Studio 编译
1. 打开 `LunaMultiplayer.sln`（或分别编译 `LmpCommon` → `Lidgren.Net` → `LmpClient`）。
2. 目标框架 **.NET Framework 4.7.2**，平台 **AnyCPU / x64**。
3. 编译 `LmpClient`，产出 `LmpClient.dll` + 依赖。
4. 把产物连同 `LmpCommon.dll`、`Lidgren.Net.dll` 复制到 KSP 的 `GameData\LunaMultiplayer\Plugins\` 覆盖（保留原 `LunaMultiplayer.dll` 入口）。

### 6.3 联机自测（1x 实时）
1. **全员**装同版本 Principia + 同版本 Kopernicus/OPM（朋友 mod 都是你发的，已满足）。
2. 启动服务端（`Server.exe` 或 `LmpServerManager.exe`），客户端联机。
3. 在 Principia 引力显著场景（近行星、转移轨道、多体共振区）驾驶飞船，让队友观察：
   - ✅ 队友看到你的飞船沿 **N 体轨迹** 平滑移动，不跳变、不卡死 → Phase A 成功。
   - ❌ 仍跳变/timeout：开 KSP.log 看 `[LMP]` / Principia 报错，多半是 `WorldPosition`/`obt_velocity` 取法或 `SetVesselWorldPositionAndRotation` 的 transform 偏移问题，回到 `VesselPositioner.cs` 调。
4. warp>1 时预期会漂移（阶段 B 未做），先只用 1x 验证。

### 6.4 已知待调点（真机联调时最可能踩）
- 世界速度取法：`vessel.obt_velocity` 是相对 SOI 体的世界速度，对 N 体绝对坐标基本可用；若发现队友飞船有系统性速度偏差，改取 `vessel.srf_velocity + mainBody.velocity`（同源到世界系）。
- `UpdateFromStateVectors(relPos, relVel, body, UT)` 仅用于让 KSP 内部系统（CommNet/情况）不崩，Principia 下一 tick 会覆盖；若冲突，可整段跳过（仅设 transform）。
- 多体参考系：NBodyMode 船的 `BodyIndex` 仍取 `referenceBody.flightGlobalsIndex`，用于 `relPos` 计算；若 Principia 把参考系换到 barycenter，需相应调整。
