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
        // UI 用：保存最近若干条日志，供游戏内窗口滚动显示
        public static readonly object Lock = new();
        public static readonly System.Collections.Generic.List<string> Recent = new();
        private const int MaxRecent = 80;

        private static void Push(string line)
        {
            lock (Lock)
            {
                Recent.Add(line);
                if (Recent.Count > MaxRecent) Recent.RemoveAt(0);
            }
        }

        public static void Info(string message) { Push(message); global::UnityEngine.Debug.Log("[LMP2] " + message); }
        public static void Warn(string message) { Push(message); global::UnityEngine.Debug.LogWarning("[LMP2] " + message); }
        public static void Error(string message) { Push(message); global::UnityEngine.Debug.LogError("[LMP2] " + message); }
        public static void Debug(string message) { Push(message); global::UnityEngine.Debug.Log("[LMP2][DBG] " + message); }
    }
}
