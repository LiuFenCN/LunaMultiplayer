// 全局 using：统一引入项目内命名空间与 KSP2 程序集命名空间，
// 避免每个文件重复写 using（早期生成代码漏写大量 using 导致 CS0103）。
// 注意：KSP2 的全局游戏访问器是 KSP.Game.GameManager.Instance.Game（GameInstance 是 MonoBehaviour，
// 没有静态 Instance；Game 是命名空间，不是类型）。
global using System;
global using System.Collections.Generic;
global using System.Collections.Concurrent;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

global using LunaMultiplayer.KSP2.Core;
global using LunaMultiplayer.KSP2.Base;
global using LunaMultiplayer.KSP2.Network;
global using LunaMultiplayer.KSP2.VesselUtilities;
global using LunaMultiplayer.KSP2.Systems.VesselPositionSys;
global using LunaMultiplayer.KSP2.Systems.VesselResourceSys;
global using LunaMultiplayer.KSP2.Systems.VesselActionGroupSys;
global using LunaMultiplayer.KSP2.Systems.VesselStructureSys;
global using LunaMultiplayer.KSP2.Systems.TimeSyncSys;

global using KSP.Game;
global using KSP.Sim;
global using KSP.Sim.impl;
global using KSP.Sim.ResourceSystem;
global using KSP.Sim.State;
global using UnityEngine;
