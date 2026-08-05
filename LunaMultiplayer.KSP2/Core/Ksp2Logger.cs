using UnityEngine;

namespace LunaMultiplayer.KSP2.Core
{
    /// <summary>
    /// 极简日志包装，替换 LMP 的 LunaLog。
    /// 底层用 UnityEngine.Debug（在 KSP2 / Redux 环境下始终可用，
    /// 不依赖 BepInEx 链式加载器是否运行）。输出带 [LMP2] 前缀。
    /// </summary>
    public static class Ksp2Logger
    {
        public static void Info(string message) => global::UnityEngine.Debug.Log("[LMP2] " + message);
        public static void Warn(string message) => global::UnityEngine.Debug.LogWarning("[LMP2] " + message);
        public static void Error(string message) => global::UnityEngine.Debug.LogError("[LMP2] " + message);
        public static void Debug(string message) => global::UnityEngine.Debug.Log("[LMP2][DBG] " + message);
    }
}
