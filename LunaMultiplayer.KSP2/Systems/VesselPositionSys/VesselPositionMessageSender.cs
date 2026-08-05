using KSP.Sim.impl;
using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.Core;

namespace LunaMultiplayer.KSP2.Systems.VesselPositionSys
{
    /// <summary>
    /// 读取本机飞船的 KSP.Sim 状态，填充 VesselPositionMsgData 并发送。
    /// 对应 LMP 的 VesselPositionMessageSender.CreateMessageFromVessel。
    /// 所有字段均来自已测绘的 VesselComponent / PatchedConicsOrbit API。
    /// </summary>
    public class VesselPositionMessageSender : MessageSenderBase<VesselPositionSystem>
    {
        public void SendVesselPositionUpdate(VesselComponent vessel)
        {
            if (vessel == null) return;

            var msg = new VesselPositionMsgData
            {
                VesselId = vessel.Guid,
                BodyGuid = vessel.mainBody?.Guid,
                BodyName = vessel.mainBody?.bodyName,
                Landed = vessel.Landed,
                Splashed = vessel.Splashed,
                HeightFromTerrain = (float)vessel.AltitudeFromTerrain,
                GameTime = Ksp2Time.UniversalTime
            };

            // 速度（相对于天体的表面速度，对应 LMP 的 srf_velocity）
            var vel = vessel.OrbitalVelocity; // KSP.Sim.Vector
            msg.VelocityVector[0] = vel.vector.x;
            msg.VelocityVector[1] = vel.vector.y;
            msg.VelocityVector[2] = vel.vector.z;

            // 朝向四元数（ITransformModel.Rotation 是 KSP.Sim.Rotation，内部 localRotation 为 QuaternionD）
            var rot = vessel.transform.Rotation;
            msg.SrfRelRotation[0] = (float)rot.localRotation.x;
            msg.SrfRelRotation[1] = (float)rot.localRotation.y;
            msg.SrfRelRotation[2] = (float)rot.localRotation.z;
            msg.SrfRelRotation[3] = (float)rot.localRotation.w;

            // 经典轨道根数（与 KSP1 布局一致）
            var o = vessel.Orbit;
            if (o != null)
            {
                msg.Orbit[0] = o.inclination;
                msg.Orbit[1] = o.eccentricity;
                msg.Orbit[2] = o.semiMajorAxis;
                msg.Orbit[3] = o.longitudeOfAscendingNode;   // KSP2 命名为 longitudeOfAscendingNode（KSP1 的 LAN）
                msg.Orbit[4] = o.argumentOfPeriapsis;
                msg.Orbit[5] = o.meanAnomalyAtEpoch;
                msg.Orbit[6] = o.epoch;
                msg.Orbit[7] = 0;
            }

            // 地表坐标
            msg.LatLonAlt[0] = vessel.Latitude;
            msg.LatLonAlt[1] = vessel.Longitude;
            msg.LatLonAlt[2] = vessel.AltitudeFromTerrain;

            Ksp2Logger.Debug($"发送飞船位置 {msg.VesselId}");
            Send(msg);
        }
    }
}
