using BepInEx.Logging;

namespace LunaMultiplayer.KSP2.Core
{
    /// <summary>
    /// 极简日志包装，替换 LMP 的 LunaLog。
    /// 底层用 BepInEx 的 ManualLogSource，输出到 BepInEx/LogOutput.log 与控制台。
    /// </summary>
    public static class Ksp2Logger
    {
        public static ManualLogSource Log { get; private set; }

        public static void Init(ManualLogSource source)
        {
            Log = source;
        }

        public static void Info(string message) => Log?.LogInfo("[LMP2] " + message);
        public static void Warn(string message) => Log?.LogWarning("[LMP2] " + message);
        public static void Error(string message) => Log?.LogError("[LMP2] " + message);
        public static void Debug(string message) => Log?.LogDebug("[LMP2] " + message);
    }
}
