using Lidgren.Network;
using LunaMultiplayer.KSP2.Base;

namespace LunaMultiplayer.KSP2.Systems.VesselPositionSys
{
    /// <summary>
    /// 飞船位置同步线格式。数组布局刻意与 LMP 的 VesselPositionMsgData 保持一致
    /// （Orbit[8]、LatLonAlt[3]、VelocityVector[3]、SrfRelRotation[4]），
    /// 以便未来 KSP2 客户端可与 LMP 服务端互通。
    /// 与 KSP1 的区别：用 BodyGuid（KSP2 用 IGGuid 字符串标识天体）替代 BodyIndex。
    /// </summary>
    [MessageTypeId(101)]
    public class VesselPositionMsgData : IMessageData
    {
        static VesselPositionMsgData()
        {
            MessageRegistry.Register(typeof(VesselPositionMsgData));
        }

        public ushort SubType => 0;

        public string VesselId;        // KSP2 用 Guid 字符串标识飞船
        public string BodyGuid;        // 参考天体 Guid（KSP2 无 flightGlobalsIndex）
        public string BodyName;        // 调试用
        public int SubspaceId;
        public float PingSec;
        public float HeightFromTerrain;
        public bool Landed;
        public bool Splashed;
        public double GameTime;

        public double[] LatLonAlt = new double[3];
        public double[] VelocityVector = new double[3];
        public double[] NormalVector = new double[3];   // KSP2 暂无直接 terrainNormal，占位
        public float[] SrfRelRotation = new float[4];   // 飞船朝向四元数 x,y,z,w
        public double[] Orbit = new double[8];          // inclination, eccentricity, semiMajorAxis,
                                                        // longitudeOfAscendingNode, argumentOfPeriapsis,
                                                        // meanAnomalyAtEpoch, epoch, (reserved)

        public void Serialize(NetOutgoingMessage m)
        {
            m.Write(VesselId ?? "");
            m.Write(BodyGuid ?? "");
            m.Write(BodyName ?? "");
            m.Write(SubspaceId);
            m.Write(PingSec);
            m.Write(HeightFromTerrain);
            m.Write(Landed);
            m.Write(Splashed);
            m.Write(GameTime);
            for (var i = 0; i < 3; i++) m.Write(LatLonAlt[i]);
            for (var i = 0; i < 3; i++) m.Write(VelocityVector[i]);
            for (var i = 0; i < 3; i++) m.Write(NormalVector[i]);
            for (var i = 0; i < 4; i++) m.Write(SrfRelRotation[i]);
            for (var i = 0; i < 8; i++) m.Write(Orbit[i]);
        }

        public void Deserialize(NetIncomingMessage m)
        {
            VesselId = m.ReadString();
            BodyGuid = m.ReadString();
            BodyName = m.ReadString();
            SubspaceId = m.ReadInt32();
            PingSec = m.ReadFloat();
            HeightFromTerrain = m.ReadFloat();
            Landed = m.ReadBoolean();
            Splashed = m.ReadBoolean();
            GameTime = m.ReadDouble();
            for (var i = 0; i < 3; i++) LatLonAlt[i] = m.ReadDouble();
            for (var i = 0; i < 3; i++) VelocityVector[i] = m.ReadDouble();
            for (var i = 0; i < 3; i++) NormalVector[i] = m.ReadDouble();
            for (var i = 0; i < 4; i++) SrfRelRotation[i] = m.ReadFloat();
            for (var i = 0; i < 8; i++) Orbit[i] = m.ReadDouble();
        }

        public int GetMessageSize()
        {
            return 3 * 64 + sizeof(int) + 2 * sizeof(float) + 2 * sizeof(bool) + sizeof(double)
                 + 3 * 3 * sizeof(double) + 4 * sizeof(float) + 8 * sizeof(double);
        }
    }
}
