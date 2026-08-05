using UnityEngine;
using LunaMultiplayer.KSP2.Base;

namespace LunaMultiplayer.KSP2.Core
{
    /// <summary>
    /// 挂在 Unity 主循环的驱动器。每帧调用各已启用系统的 Update/LateUpdate/FixedUpdate。
    /// 这是同步逻辑的"心跳"，对应 LMP 用 TimingManager 注册例程的做法，
    /// 但改为标准的 BepInEx MonoBehaviour 方式，避免依赖 KSP2 内部 TimingManager。
    /// </summary>
    public class Ksp2Runner : MonoBehaviour
    {
        private void Update() => SystemBase.UpdateAll();
        private void LateUpdate() => SystemBase.LateUpdateAll();
        private void FixedUpdate() => SystemBase.FixedUpdateAll();
    }
}
