# KSP2 Redux 模组加载机制分析 —— 为什么我们的 mod 加载不出来（已修正）

> 分析日期：2026-08-06（03:40 初版 → 03:50 修正）
> 游戏版本：KSP2 Redux 0.2.8.5.103184-beta（commit ffc94930）

---

## 1. 结论速览（最新）

| 项目 | 初版结论 | 修正后结论 |
|------|---------|-----------|
| `mods/` 目录是否有效 | 认为是 Redux 阶段三的休眠目录 | **是第三方 mod 的有效加载目录** |
| 我们的 mod 能否被加载 | 认为不能 | **能**（只要放进 `mods/<ModName>/`） |
| BepInEx 是否在运行 | 否 | 否（仍然正确） |
| 之前为什么没加载 | 认为 Redux 没开放通道 | **因为我们把文件放进了 `BepInEx/plugins/`，而 Redux 的第三方 mod 路径是 `mods/`** |
| 当前剩余问题 | — | `OnInitialized()` 没有可见日志输出，疑似静默失败；`swinfo.json` 的 `version_check` 指向 markdown 导致版本检查 NRE |

---

## 2. 证据链（来自真实日志与文件系统）

### 2.1 BepInEx 仍然没运行
- 游戏根目录**没有任何 doorstop 代理 dll**（`winhttp.dll` / `winmm.dll` / `version.dll` / `dbghelp.dll`）。
- `Ksp2.log` 中搜索 `bepinex` / `chainloader` **出现 0 次**。
- `BepInEx/LogOutput.log` 停留在 **2024-10-29**，从未被本次运行覆盖。

### 2.2 `mods/` 是有效的第三方 mod 目录
用户把 `LunaMultiplayer.KSP2/`（含 `LunaMultiplayer.KSP2.dll` + `Lidgren.Network.dll` + `swinfo.json`）复制到 `F:\Program Files\Epic Games\Kerbal.Space.Program.2\mods\LunaMultiplayer.KSP2\` 后，日志出现：

```
[Space Warp] Attempting to register mod: com.liufencn.lunamultiplayer.ksp2, LunaMultiplayer KSP2
[Space Warp] Registered plugin: com.liufencn.lunamultiplayer.ksp2
[Space Warp] Pre-initializing: LunaMultiplayer KSP2?
[System] Pre-initialization for plugin LunaMultiplayer KSP2 completed in 0.0001s.
...
[System] Initialization for plugin LunaMultiplayer KSP2 completed in 0.0002s.
...
[System] Post-initialization for plugin LunaMultiplayer KSP2 completed in 0.0001s.
```

并且游戏内 **模组列表** 已显示 `LunaMultiplayer KSP2` 在「启用的模组」中。说明：
- SpaceWarp2/Redux 会扫描 `mods/<ModName>/swinfo.json` 来发现并注册第三方 mod。
- 注册后会把该 mod 当作 SpaceWarp 插件，走 `PreInitialize → Initialize(OnInitialized) → PostInitialize` 生命周期。
- `BepInEx/plugins/` 在 Redux 下**不用于第三方 mod 发现**（里面只保留 Redux 自带的 SpaceWarp/PatchManager/VSwift 等组件）。

### 2.3 当前异常与疑点

#### A. `OnInitialized()` 没有可见输出
`Plugin.cs` 的 `OnInitialized()` 开头就调用：
```csharp
Ksp2Logger.Info($"{PluginName} v{PluginVersion} 加载（SpaceWarp2 mod）");
```
`Ksp2Logger` 底层是 `global::UnityEngine.Debug.Log("[LMP2] " + message)`。但 `Ksp2.log` 中搜索 `LMP2` **出现 0 次**。

同时 `Initialization for plugin LunaMultiplayer KSP2 completed in 0.0002s` 快得不正常（0.2ms），而 `OnInitialized()` 中还应：
- 注册 5 个消息类型
- 启动 Lidgren 网络线程
- 创建 `GameObject` + `DontDestroyOnLoad` + 挂载 `Ksp2Runner`
- 实例化并启用 5 个同步系统

这些操作不可能在 0.2ms 内完成。**极大概率 `OnInitialized()` 在 `base.OnInitialized()` 或极早阶段抛出异常，被 SpaceWarp 的 init 包装器吞掉**。

#### B. `version_check` 指向 markdown 导致版本检查 NRE
`swinfo.json` 中：
```json
"version_check": "https://raw.githubusercontent.com/LiuFenCN/LunaMultiplayer/ksp2/KSP2_MP_DESIGN.md"
```
日志报：
```
[Serialization] Unexpected character encountered while parsing value: #. Path '', line 0, position 0.
[SpaceWarp.VersionChecking] Unable to check version for com.liufencn.lunamultiplayer.ksp2 due to error System.NullReferenceException
```
因为 `KSP2_MP_DESIGN.md` 是 markdown 文件，首字符是 `#`，SpaceWarp 把它当 JSON 解析失败，随后空引用。

---

## 3. Redux 自带 mod 与第三方 mod 的加载区别

### Redux 自带组件（SpaceWarp2 / Ksp2Redux / PatchManager / VSwift）
- 由核心程序集 `ReduxLib.dll`（`KSP2_x64_Data/Managed/`）的私有引导器**硬编码加载**。
- 它们的 dll 文件虽然位于 `BepInEx/plugins/` 下，但**不是**被 BepInEx 扫出来的（BepInEx 没运行）。

### 第三方 mod（如 LunaMultiplayer KSP2）
- 放到游戏根目录的 **`mods/<ModName>/swinfo.json`** 下即可被 SpaceWarp2 发现并注册。
- 入口类需要继承 `SpaceWarp2.API.Mods.GeneralMod`（实现 `ISpaceWarpMod`），初始化写在 `OnInitialized()`。
- 普通 `BaseUnityPlugin` 在 Redux 下不会被加载（因为 BepInEx 没运行）。

---

## 4. 立即修复项

### 4.1 修复 `Plugin.cs`：给 `OnInitialized()` 加 try-catch + 入口日志
用裸 `UnityEngine.Debug.Log` 打入口标记，并把 `base.OnInitialized()` 和主体逻辑分别包在 try-catch 里，这样即使异常也能在 `Ksp2.log` 看到具体错误。

已修改 `LunaMultiplayer.KSP2/Plugin.cs`：
- 第一行输出 `[LMP2] >>> OnInitialized ENTRY`
- `base.OnInitialized()` 单独 try-catch
- 主体逻辑 try-catch，异常用 `global::UnityEngine.Debug.LogError` 输出

### 4.2 修复 `swinfo.json`：移除 `version_check`
已删除指向 markdown 的 `version_check` 字段，避免版本检查 NRE。

### 4.3 更新安装路径文档
`README.md` 已更新：
- 安装目录从 `BepInEx/plugins/LunaMultiplayer.KSP2/` 改为 `mods/LunaMultiplayer.KSP2/`。
- PowerShell 一键安装脚本同步改为写入 `mods/`。
- 提示用户同时检查 `[Space Warp] Registered plugin: ...`（注册成功）和 `[LMP2]` 日志（`OnInitialized` 实际执行）。

---

## 5. 下一步验证

用户重新编译并复制到 `mods/LunaMultiplayer.KSP2/` 后，启动游戏，在 `Ksp2.log` 中检查：

1. 是否还有版本检查 NRE —— 应该消失。
2. 是否有 `[LMP2] >>> OnInitialized ENTRY`。
3. 是否有 `[LMP2] base.OnInitialized OK` 或 `FAILED`。
4. 是否有 `[LMP2] OnInitialized body FAILED: ...` 及完整堆栈。
5. `Initialization for plugin LunaMultiplayer KSP2 completed in ...` 是否变为合理耗时（> 数毫秒）。

根据这些日志即可定位 `OnInitialized()` 实际失败的点。

---

## 6. 历史误判记录

- **03:40 初版误判**：「`mods/` 是阶段三休眠目录，Redux 当前不加载任何第三方 mod。」
- **03:50 修正**：`mods/` 是有效加载目录；之前失败只因 mod 放在了错误的 `BepInEx/plugins/`。Redux 官方说的「阶段三」可能指更完整的 mod 管理/工坊/校验，但基础 `mods/` 目录发现机制已经可用。
