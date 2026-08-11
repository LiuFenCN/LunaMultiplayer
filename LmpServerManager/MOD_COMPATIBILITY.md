# LMP 联机 Mod 兼容性说明

> 本文件针对当前客户端 `G:\Game\KerbalSpaceProgram\Simplified_Chinese\GameData` 内的 mod 列表整理。所有玩家必须使用**完全相同版本**的 mod，否则会出现不同步/白名单校验失败。

## 不兼容 / 高风险（必须禁用）

| Mod | 原因 |
|-----|------|
| **Principia** | N 体物理替换原版飞行积分器与时间 warp，LMP 的飞船同步模型基于原版二体轨道。两者在架构上冲突，联机几乎必然卡死/崩溃。 |
| **Kopernicus** | 重写星系系统。服务端本身不加载 GameData，LMP 服务端无法识别改造后的行星/轨道；所有客户端即使版本一致，也可能因服务端轨道权威而崩。 |
| **OPM** / **OPX-*** (JoolPlus/SarnusPlus/UrlumPlus/NeidonPlus/InnerWorlds) | 外行星扩展，新增/移动天体。服务端当前 Universe 是原版星系，客户端进入新增天体轨道或场景时同步会 timeout。 |

## 可用但需注意（建议全队统一版本/配置）

| Mod | 说明 |
|-----|------|
| **FreeIva** | 舱内自由移动。KSP 1.12 + LMP 下通常可用，但舱内乘客状态可能不同步，建议所有玩家一致开启/关闭。 |
| **KIS / KAS** | 物品建造与缆绳。物理缆绳状态同步不完整，可能出现"一方看到缆绳、一方没有"，建议联机时避免使用。 |
| **MechJeb2** | 自动驾驶信息展示安全；但**自动执行机动节点/着陆**可能与服务端飞船权威冲突，导致不同步。建议仅当信息插件使用。 |
| **RealAntennas** | 通信链路计算。通常兼容，但天线状态（展开/收起）必须同步，MOD 本身版本需全队一致。 |
| **ContractConfigurator** | 服务端不生成任务，客户端任务进度不共享。单人任务可用，但不影响联机同步。 |
| **TweakScale** / **InterstellarFuelSwitch** / **B9PartSwitch** | 变体/缩放类零件必须加入 `LMPModControl.xml` 白名单（Dll + 零件配置），否则会被服务端拒绝发射。 |
| **NearFuture** / **StationPartsExpansionRedux** / **KerbalEngineer** 等零件/信息 mod | 一般兼容。只要白名单包含对应 dll，所有客户端版本一致即可。 |

## 通常安全（视觉/信息/辅助类）

- **视觉**: ParallaxContinued + 三套贴图包、Scatterer、EVE、Waterfall、WaterfallRestock、StockVolumetricClouds、TUFX、ReStock/ReStockPlus、Resurfaced、Shabby
- **信息**: KerbalEngineer、PreciseManeuver、Chatterer（音效）
- **辅助**: KSPCommunityFixes、LoadFix、KeepItStraight、KerbalJointReinforcement、CommunityCategoryKit
- **框架依赖**: ModuleManager、Harmony、KSPBurst、ClickThroughBlocker、ToolbarControl、ModularFlightIntegrator

## 服务端不需要装这些 mod

LMP 服务端 `Server.exe` 不加载 KSP GameData，只管理 `Universe/` 场景同步。因此：
- 视觉 mod、零件 mod、信息 mod **完全不需要**复制到服务端。
- 唯一需要服务端一致的是**星系改造类 mod**（Kopernicus/OPM/OPX），但服务端没有 GameData 机制，所以这条路实际上走不通；联机请用原版星系。

## 推荐联机配置

1. 所有玩家使用**同一套 mod 包**（同版本、同配置）。
2. 联机前服务端和客户端都**禁用 Principia、Kopernicus、OPM、OPX-***。
3. 把其他 mod 的 dll 全部加入 `LMPModControl.xml`（LmpServerManager 白名单页可一键编辑保存）。
4. `AllowNonListedPlugins=true` 可放宽限制，但建议关闭，强制 everyone 使用相同 dll。
