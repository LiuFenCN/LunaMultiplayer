using LunaMultiplayer.KSP2.VesselUtilities;
using System;
using System.Collections.Generic;

namespace LunaMultiplayer.KSP2.Systems.VesselPositionSys
{
    /// <summary>
    /// 一艘远端飞船的插值状态机。
    /// 持有最近 N 条入站位置样本（带本地时间戳），并在每帧把
    /// "renderTime = 本地统一时间 - InterpolationDelay" 处插值出的状态写回该飞船的 VesselComponent。
    /// 对应 LMP 的 VesselPositionUpdate（其 LerpArrays + 双样本混合），此处改为显式时间缓冲。
    ///
    /// 插值策略（针对 KSP2 已测绘 API 选定的安全路径）：
    ///  - 轨道根数[8] 在亚秒级窗口内近似静止，直接对 8 个数线性插值即可平滑表现
    ///    "沿轨道运动"（meanAnomalyAtEpoch/epoch 一并插值，在当前统一时刻求值即得到平滑位置）；
    ///  - 朝向四元数[4] 用 Slerp；
    ///  - 落地的 ApplyVesselUpdate 复用既有 TeleportSimObjectToOrbit 路径，不引入新的 VERIFY 面。
    /// 说明：对高速环绕飞船，更精确的做法是用 KSP2 的 PatchedConicsOrbit/OrbiterComponent 传播
    ///       平近点角到 renderTime（需编译期接入 KSP2 轨道解算器），列为后续优化。
    /// </summary>
    public class VesselPositionUpdate
    {
        /// <summary>回退延迟：略大于一个发送周期（100ms）以消除抖动。可按网络质量调。</summary>
        public const double InterpolationDelay = 0.20;

        private readonly List<Sample> _samples = new List<Sample>();
        private const int MaxSamples = 24;

        private struct Sample
        {
            public double LocalTime;     // 转换到本客户端的统一游戏时间
            public VesselPositionMsgData Data;
        }

        public VesselPositionUpdate(VesselPositionMsgData initial)
        {
            AddSample(initial);
            ApplyInterpolated(); // 首样本立即落地
        }

        public void AddSample(VesselPositionMsgData data)
        {
            _samples.Add(new Sample
            {
                LocalTime = ToLocalTime(data.GameTime),
                Data = data
            });
            if (_samples.Count > MaxSamples)
                _samples.RemoveAt(0);
        }

        /// <summary>每帧调用：把插值后的状态写回远端飞船。</summary>
        public void ApplyInterpolated()
        {
            if (_samples.Count == 0) return;
            if (_samples.Count == 1)
            {
                VesselCommon.ApplyVesselUpdate(_samples[0].Data);
                return;
            }

            double renderTime = Ksp2Time.UniversalTime - InterpolationDelay;

            // 找到包围 renderTime 的两个样本 i（<=renderTime）与 i+1（>=renderTime）
            int older = 0;
            for (int i = 0; i < _samples.Count; i++)
            {
                if (_samples[i].LocalTime <= renderTime)
                    older = i;
            }
            int newer = Math.Min(older + 1, _samples.Count - 1);

            var a = _samples[older];
            var b = _samples[newer];

            double span = b.LocalTime - a.LocalTime;
            double t = span > 1e-6 ? (renderTime - a.LocalTime) / span : 0.0;
            t = Math.Max(0.0, Math.Min(1.0, t));

            // 落在样本范围之外（落后/超前）时夹紧到端点，避免外推抖动
            if (renderTime <= _samples[0].LocalTime) { a = b = _samples[0]; t = 0; }
            else if (renderTime >= _samples[_samples.Count - 1].LocalTime) { a = b = _samples[_samples.Count - 1]; t = 0; }

            var interp = Interpolate(a.Data, b.Data, t);
            VesselCommon.ApplyVesselUpdate(interp);
        }

        private static VesselPositionMsgData Interpolate(VesselPositionMsgData a, VesselPositionMsgData b, double t)
        {
            var r = new VesselPositionMsgData
            {
                VesselId = b.VesselId,
                BodyGuid = b.BodyGuid,
                BodyName = b.BodyName,
                Landed = b.Landed,
                Splashed = b.Splashed,
                HeightFromTerrain = b.HeightFromTerrain,
                GameTime = b.GameTime
            };

            for (int i = 0; i < 8; i++)
                r.Orbit[i] = a.Orbit[i] + (b.Orbit[i] - a.Orbit[i]) * t;

            QuatSlerp(a.SrfRelRotation, b.SrfRelRotation, t, r.SrfRelRotation);

            for (int i = 0; i < 3; i++)
            {
                r.LatLonAlt[i] = a.LatLonAlt[i] + (b.LatLonAlt[i] - a.LatLonAlt[i]) * t;
                r.VelocityVector[i] = a.VelocityVector[i] + (b.VelocityVector[i] - a.VelocityVector[i]) * t;
            }
            return r;
        }

        /// <summary>把发送端 GameTime（其本地统一时间估计）映射到本客户端本地时间。</summary>
        private static double ToLocalTime(double senderGameTime)
        {
            // ServerOffset = 服务端时间 - 本客户端时间；故 本地时间 = 服务端时间 - ServerOffset
            return senderGameTime - TimeSyncSystem.ServerOffset;
        }

        /// <summary>四元数球面线性插值（x,y,z,w 布局），无外部依赖。</summary>
        private static void QuatSlerp(float[] from, float[] to, double t, float[] outQ)
        {
            double x1 = from[0], y1 = from[1], z1 = from[2], w1 = from[3];
            double x2 = to[0], y2 = to[1], z2 = to[2], w2 = to[3];

            double dot = x1 * x2 + y1 * y2 + z1 * z2 + w1 * w2;
            if (dot < 0) { x2 = -x2; y2 = -y2; z2 = -z2; w2 = -w2; dot = -dot; }

            const double eps = 1e-6;
            if (dot > 1.0 - eps)
            {
                // 夹角极小：线性插值即可
                outQ[0] = (float)(x1 + (x2 - x1) * t);
                outQ[1] = (float)(y1 + (y2 - y1) * t);
                outQ[2] = (float)(z1 + (z2 - z1) * t);
                outQ[3] = (float)(w1 + (w2 - w1) * t);
            }
            else
            {
                double theta0 = Math.Acos(dot);
                double theta = theta0 * t;
                double sin0 = Math.Sin(theta0);
                double s1 = Math.Sin(theta0 - theta) / sin0;
                double s2 = Math.Sin(theta) / sin0;
                outQ[0] = (float)(x1 * s1 + x2 * s2);
                outQ[1] = (float)(y1 * s1 + y2 * s2);
                outQ[2] = (float)(z1 * s1 + z2 * s2);
                outQ[3] = (float)(w1 * s1 + w2 * s2);
            }
            // 归一化
            double len = Math.Sqrt(outQ[0] * outQ[0] + outQ[1] * outQ[1] + outQ[2] * outQ[2] + outQ[3] * outQ[3]);
            if (len > eps) { outQ[0] /= (float)len; outQ[1] /= (float)len; outQ[2] /= (float)len; outQ[3] /= (float)len; }
        }
    }
}
