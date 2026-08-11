using Lidgren.Network;
using LmpCommon.Message.Base;
using LmpCommon.Message.Types;

namespace LmpCommon.Message.Data.Vessel
{
    public class VesselPositionMsgData : VesselBaseMsgData
    {
        /// <inheritdoc />
        internal VesselPositionMsgData() { }
        public override VesselMessageType VesselMessageType => VesselMessageType.Position;

        //Avoid using reference types in this message as it can generate allocations and is sent VERY often.
        public string BodyName;
        public int BodyIndex;
        public int SubspaceId;
        public float PingSec;
        public float HeightFromTerrain;
        public bool Landed;
        public bool Splashed;
        public bool HackingGravity;
        public double[] LatLonAlt = new double[3];
        public double[] VelocityVector = new double[3];
        public double[] NormalVector = new double[3];
        public float[] SrfRelRotation = new float[4];
        public double[] Orbit = new double[8];

        /// <summary>
        /// N-body sync extension (Principia / non-Keplerian integrators).
        /// 0 = standard Keplerian sync (use Orbit/LatLonAlt fields).
        /// 1 = world-space sync (use WorldPosition/WorldVelocity fields); the receiving client
        ///     re-seeds its local N-body integrator from the world state instead of forcing on-rails.
        /// </summary>
        public byte NBodyMode;
        public double[] WorldPosition = new double[3];
        public double[] WorldVelocity = new double[3];

        public override string ClassName { get; } = nameof(VesselPositionMsgData);

        internal override void InternalSerialize(NetOutgoingMessage lidgrenMsg)
        {
            base.InternalSerialize(lidgrenMsg);

            lidgrenMsg.Write(BodyIndex);
            lidgrenMsg.Write(SubspaceId);
            lidgrenMsg.Write(PingSec);
            lidgrenMsg.Write(HeightFromTerrain);
            lidgrenMsg.Write(Landed);
            lidgrenMsg.Write(Splashed);
            lidgrenMsg.Write(HackingGravity);

            for (var i = 0; i < 3; i++)
                lidgrenMsg.Write(LatLonAlt[i]);

            for (var i = 0; i < 3; i++)
                lidgrenMsg.Write(VelocityVector[i]);

            for (var i = 0; i < 3; i++)
                lidgrenMsg.Write(NormalVector[i]);

            for (var i = 0; i < 4; i++)
                lidgrenMsg.Write(SrfRelRotation[i]);

            for (var i = 0; i < 8; i++)
                lidgrenMsg.Write(Orbit[i]);

            // N-body extension: appended last so old (pre-extension) clients can still deserialize the
            // fields they know. Each read below is individually guarded against over-reading.
            lidgrenMsg.Write(NBodyMode);
            for (var i = 0; i < 3; i++)
                lidgrenMsg.Write(WorldPosition[i]);
            for (var i = 0; i < 3; i++)
                lidgrenMsg.Write(WorldVelocity[i]);

            lidgrenMsg.Write(BodyName);
        }

        internal override void InternalDeserialize(NetIncomingMessage lidgrenMsg)
        {
            base.InternalDeserialize(lidgrenMsg);

            BodyIndex = lidgrenMsg.ReadInt32();
            SubspaceId = lidgrenMsg.ReadInt32();
            PingSec = lidgrenMsg.ReadFloat();
            HeightFromTerrain = lidgrenMsg.ReadFloat();
            Landed = lidgrenMsg.ReadBoolean();
            Splashed = lidgrenMsg.ReadBoolean();
            HackingGravity = lidgrenMsg.ReadBoolean();

            for (var i = 0; i < 3; i++)
                LatLonAlt[i] = lidgrenMsg.ReadDouble();

            for (var i = 0; i < 3; i++)
                VelocityVector[i] = lidgrenMsg.ReadDouble();

            for (var i = 0; i < 3; i++)
                NormalVector[i] = lidgrenMsg.ReadDouble();

            for (var i = 0; i < 4; i++)
                SrfRelRotation[i] = lidgrenMsg.ReadFloat();

            for (var i = 0; i < 8; i++)
                Orbit[i] = lidgrenMsg.ReadDouble();

            // N-body extension: same order as serialize. Guarded so a message without the extension
            // (older client) deserializes cleanly (NBodyMode stays 0, World* stay 0).
            if (lidgrenMsg.Position < lidgrenMsg.LengthBits)
                NBodyMode = lidgrenMsg.ReadByte();
            if (lidgrenMsg.Position < lidgrenMsg.LengthBits)
            {
                for (var i = 0; i < 3; i++)
                    WorldPosition[i] = lidgrenMsg.ReadDouble();
                for (var i = 0; i < 3; i++)
                    WorldVelocity[i] = lidgrenMsg.ReadDouble();
            }

            if (lidgrenMsg.Position < lidgrenMsg.LengthBits)
                BodyName = lidgrenMsg.ReadString();
            else
                BodyName = string.Empty;
        }

        internal override int InternalGetMessageSize()
        {
            return base.InternalGetMessageSize() + BodyName.GetByteCount() + sizeof(int) * 2 + sizeof(float) * 2 + sizeof(bool) * 3 + sizeof(double) * 3 * 3 +
                sizeof(float) * 4 * 1 + sizeof(double) * 8 + sizeof(byte) + sizeof(double) * 6;
        }
    }
}
