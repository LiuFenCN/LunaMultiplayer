using System;
using System.Collections.Generic;

namespace LunaMultiplayer.KSP2.Base
{
    /// <summary>
    /// 所有子系统的基类。对应 LMP 的 System&lt;T&gt;。
    /// 负责 Enable/Disable 生命周期，并把 Update/LateUpdate/FixedUpdate 统一派发给已启用的系统。
    /// 实际驱动来自 Ksp2Runner（一个挂在 Unity 主循环的 MonoBehaviour）。
    /// </summary>
    public abstract class SystemBase
    {
        public bool Enabled { get; private set; }

        public abstract string SystemName { get; }

        private static readonly List<SystemBase> All = new List<SystemBase>();

        protected SystemBase()
        {
            lock (All)
            {
                All.Add(this);
            }
        }

        public void SetEnabled(bool value)
        {
            if (value && !Enabled)
            {
                Enabled = true;
                try { OnEnabled(); }
                catch (Exception e) { Ksp2Logger.Error($"{SystemName} OnEnabled 异常: {e}"); }
            }
            else if (!value && Enabled)
            {
                Enabled = false;
                try { OnDisabled(); }
                catch (Exception e) { Ksp2Logger.Error($"{SystemName} OnDisabled 异常: {e}"); }
            }
        }

        protected virtual void OnEnabled() { }
        protected virtual void OnDisabled() { }

        protected virtual void OnUpdate() { }
        protected virtual void OnLateUpdate() { }
        protected virtual void OnFixedUpdate() { }

        public static void UpdateAll()
        {
            lock (All)
            {
                foreach (var s in All)
                    if (s.Enabled) s.OnUpdate();
            }
        }

        public static void LateUpdateAll()
        {
            lock (All)
            {
                foreach (var s in All)
                    if (s.Enabled) s.OnLateUpdate();
            }
        }

        public static void FixedUpdateAll()
        {
            lock (All)
            {
                foreach (var s in All)
                    if (s.Enabled) s.OnFixedUpdate();
            }
        }
    }
}
