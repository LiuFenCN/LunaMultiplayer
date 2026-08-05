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
| 当前剩余问题 | — | ~~`OnInitialized()` 没有可见日志输出~~ 已定位最终根因：**`swinfo.json` 缺失 `main_assembly` 字段 → SpaceWarp 把 mod 当成 `AssetOnlyMod`（仅资源、不实例化、不调用 `OnInitialized()`）**。修复：swinfo 加 `"main_assembly": "LunaMultiplayer.KSP2.dll"`，依赖 `Lidgren.Network.dll` 移入 `lib/`，`Plugin` 加 `AssemblyResolve` 回退。 |

---

## 0. 最终验证结果（2026-08-06 04:44 启动确认 ✅）

按 `main_assembly` + `lib/` 修复后重新部署并启动游戏，`Ksp2.log` 完整跑通 mod 生命周期，**零 mod 相关报错**：

```
[LOG 04:43:36.783] [LMP2] >>> OnInitialized ENTRY
[LOG 04:43:36.783] [LMP2] base.OnInitialized OK
[LOG 04:43:36.783] [LMP2] LunaMultiplayer KSP2 v0.1.0 加载（SpaceWarp2 mod）
[LOG 04:43:36.785] [LMP2] [LMP2] MessageRegistry 注册完成
[LOG 04:43:37.087] [LMP2] 网络线程已启动
[LOG 04:43:37.087] [LMP2] [LMP2] NetworkMain.Start() 完成
[LOG 04:43:37.089] [LMP2] [LMP2] Ksp2Runner 已挂载
[LOG 04:43:37.092] [LMP2] 系统已启用。联机 API：NetworkConnection.Host(port) / Connect(host, port)
[LOG 04:43:37.093] [LMP2] <<< OnInitialized SUCCESS
[LOG 04:43:37.093] [System] Initialization for plugin LunaMultiplayer KSP2 completed in 0.3100s.
[LOG 04:43:37.163] [System] Post-initialization for plugin LunaMultiplayer KSP2 completed in 0.0001s.
[LOG 04:44:30.895] [LMP2] LunaMultiplayer KSP2 卸载
```

- `mods/LunaMultiplayer.KSP2/LunaMultiplayer.KSP2.dll` 与本地编译版 sha256 一致（`caae194f…`），布局为 `根 dll + swinfo.json + lib/Lidgren.Network.dll`。
- 末尾 `[LMP2] LunaMultiplayer KSP2 卸载` 证明 `Application.quitting` 清理钩子也生效。
- 日志里其余的 `Discord RPC` / `DOTWEEN` / `GraphicsManager.NullReferenceException(OnDestroy)` 警告与异常**均与本 mod 无关**（游戏本体/Discord SDK/渲染管理器关闭时的噪音）。
- **结论：KSP2 Redux + SpaceWarp2 第三方 mod 的加载通道完全打通**，加载规范见第 3 节，已沉淀为可复用 skill `ksp2-redux-sw-mod-loading`。

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

### 2.3 最终根因（2026-08-06 04:36 反编译 `SpaceWarp2.dll` 定位）

#### A. swinfo 缺 `main_assembly` → mod 被当成 `AssetOnlyMod`
用 Mono.Cecil 反编译 `KSP2_x64_Data/Managed/SpaceWarp2.dll`，在
`SpaceWarp2.API.Backend.Modding.PluginRegister.RegisterMods` 中看到：

```
ldfld ModInfo.MainAssembly
brfalse → new AssetOnlyMod(name)        // MainAssembly 为 null 时，只建资源型 mod
...
// MainAssembly 非空时才 Assembly.LoadFile(...MainAssembly)
// 加载后在程序集中找 ISpaceWarpMod 实现（非抽象）→ new UnloadedMod(type)
```

并且生命周期调用方 `SpaceWarp2.Patching.LoadingActions.InitializeModAction.DoAction` 的 IL 是：

```
ldfld _plugin.DoLoadingActions
brfalse → 跳过（不调用 OnInitialized）
ldfld _plugin.Plugin
dup
brtrue → callvirt ISpaceWarpMod.OnInitialized()   // 仅当 Plugin 实例非 null
```

结论：**只有当 `swinfo.json` 声明 `main_assembly` 时，SpaceWarp 才会加载我们的程序集、
实例化 `Plugin`（Activator.CreateInstance）、并在 `InitializeModAction` 里调用 `OnInitialized()`。**
我们之前没写 `main_assembly` → 走 `AssetOnlyMod` 分支 → `Plugin` 永远不被实例化 →
日志里虽出现「Registered plugin」和 `Initialization ... completed`，但那是流程包装日志，
`OnInitialized()` 从未执行，所以没有任何 `[LMP2]` 输出。

#### B. 依赖程序集必须放 `lib/`
`RegisterMods` 仅在 `<ModDir>/lib` 目录存在时预加载 `lib/*.dll`；入口 dll 由 `main_assembly` 指定。
因此 `Lidgren.Network.dll` 必须置于 `mods/LunaMultiplayer.KSP2/lib/`，否则 `OnInitialized` 中
`NetworkMain.Start()` 引用 Lidgren 时会 `FileNotFoundException`。`Plugin` 静态构造里另挂了
`AssemblyResolve` 回退（从 `<ModDir>/lib/Lidgren.Network.dll` 解析），doubly 保证可解析。

#### C. 修复清单（已落实到代码）
1. `swinfo.json` 增加 `"main_assembly": "LunaMultiplayer.KSP2.dll"`。
2. `LunaMultiplayer.KSP2.csproj`：post-build 把 `Lidgren.Network.dll` 拷到 `$(OutDir)lib`（原拷到根）。
3. `Plugin.cs`：新增静态构造，挂 `AppDomain.CurrentDomain.AssemblyResolve` 解析 Lidgren 回退。
4. `dotnet build -c Debug` 通过（0 错误）；布局变为 `LunaMultiplayer.KSP2.dll` + `swinfo.json` + `lib/Lidgren.Network.dll`。

#### D. 验证方法（待用户重新安装后启动）
游戏日志应出现：
```
[LMP2] >>> OnInitialized ENTRY
[LMP2] base.OnInitialized OK
[LMP2] MessageRegistry 注册完成
[LMP2] NetworkMain.Start() 完成
[LMP2] Ksp2Runner 已挂载
[LMP2] <<< OnInitialized SUCCESS
```
若出现 `[LMP2] OnInitialized body FAILED: ...`，把异常贴回即可定位下一处。

这些操作不可能在 0.2ms 内完成。

**但后续排查发现真正原因不是异常，而是 `mods/LunaMultiplayer.KSP2/LunaMultiplayer.KSP2.dll` 还是旧版本。**

- 已安装的 dll：37,888 字节，修改时间 **03:34**，不含 LMP2 入口日志。
- 本地最新编译 dll：38,400 字节，修改时间 **04:01**，已加 `global::UnityEngine.Debug.Log("[LMP2] >>> OnInitialized ENTRY")` 与 try-catch。
- 两者 sha256 不同。

所以不是 `OnInitialized` 失败，而是运行时加载的 dll 本身就没有这些日志代码。更新 dll 后再次启动即可验证。

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

**先确认把最新 dll 复制过去**，再启动游戏：

```powershell
$src = 'F:\缓存\软件缓存\workboddy\2026-08-02-17-40-23\ksp2_mp\LunaMultiplayer.KSP2\bin\Debug\netstandard2.1'
$dst = 'F:\Program Files\Epic Games\Kerbal.Space.Program.2\mods\LunaMultiplayer.KSP2'
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item "$src\LunaMultiplayer.KSP2.dll" $dst -Force
Copy-Item "$src\Lidgren.Network.dll"      $dst -Force
Copy-Item "$src\swinfo.json"              $dst -Force
Get-ChildItem $dst | Select-Object Name, LastWriteTime, @{N='Size';E={$_.Length}}
```

启动游戏，在 `Ksp2.log` 中检查：

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
