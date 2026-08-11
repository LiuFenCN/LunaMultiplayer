# LMP 服务端管理器 (LmpServerManager)

LunaMultiplayer 专用服务端的**原生窗口**管理工具（C# / WinForms，.NET 10）。
替代原先基于网页的管理界面，双击即用，可随 `Server.exe` 一起部署到服务器。

## 功能

- **服务器**：启动 / 停止 `Server.exe`、实时显示运行状态 / PID / 运行时长 / 端口
- **配置**：浏览并编辑 `Config/*.xml`（按 LMP 的真实编码读写，避免中文乱码）
- **Mod 白名单**：开关 `AllowNonListedPlugins`、查看 / 增删 `OptionalPlugins/DllFile`
- **日志**：查看服务端最新日志
- **清档重开**：把 `Universe` 备份为 `_universe_backup_时间戳` 并重启服务端

## 使用

1. 把本目录（或编译后的 `LmpServerManager.exe`）放到 **LMP 服务端根目录**（`Server.exe` 所在目录）旁边。
2. 直接运行 `LmpServerManager.exe`。程序会自动以自身所在目录作为服务器目录；
   若不在同目录，可在窗口顶部点「浏览…」指定。
3. 前置：Windows + .NET 10 运行时（或自行 `dotnet publish` 为自带运行时版本）。

## 构建

```bat
dotnet build -c Release
rem 生成单文件 exe（依赖本机 .NET 运行时）:
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false
```

构建产物在 `bin/Release/net10.0-windows/`。

## 说明

- 本工具仅管理**专用服务端**（`Server.exe`），不修改任何 KSP 客户端文件。
- 联机兼容性（如 Principia 等 N 体物理 mod）属于 LMP 同步层议题，不在此工具范围内。
