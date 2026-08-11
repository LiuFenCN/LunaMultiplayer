using LmpClient.Systems.TimeSync;
using LmpCommon;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace LmpClient.Systems.VesselPositionSys.ExtensionMethods
{
    public static class VesselPositioner
    {
        public static void SetVesselPosition(this Vessel vessel, VesselPositionUpdate update, VesselPositionUpdate target, float percentage)
        {
            if (vessel == null || update == null || target == null) return;

            // N-body (Principia / non-Keplerian) sync: apply world position/velocity directly and let the
            // local integrator on each client re-propagate from this identical state. We must NOT force the
            // vessel on-rails (the standard path does that and it breaks N-body trajectories).
            if (update.NBodyMode == 1 || target.NBodyMode == 1)
            {
                ApplyNBodyVesselPosition(vessel, update, target, percentage);
                return;
            }

            var lerpedBody = percentage < 0.5 ? update.Body : target.Body;

            ApplyOrbitInterpolation(vessel, update, target, lerpedBody, percentage);

            //Do not use CoM. It's not needed and it generate issues when you patch the protovessel with it as it generate weird commnet lines
            //It's important to set the static pressure as otherwise the vessel situation is not updated correctly when
            //Vessel.updateSituation() is called in the Vessel.LateUpdate(). Same applies for landed and splashed
            vessel.staticPressurekPa = FlightGlobals.getStaticPressure(target.LatLonAlt[2], lerpedBody);
            vessel.heightFromTerrain = target.HeightFromTerrain;

            ApplyInterpolationsToVessel(vessel, update, target, lerpedBody, percentage);

            vessel.protoVessel.UpdatePositionValues(vessel);
        }

        /// <summary>
        /// N-body sync: interpolate the world position/velocity between the two updates and drive the vessel
        /// transform directly. The KSP orbit is refreshed from the world state only so internal systems
        /// (CommNet, situation) keep working; the N-body integrator overwrites it on its next tick.
        /// </summary>
        private static void ApplyNBodyVesselPosition(Vessel vessel, VesselPositionUpdate update, VesselPositionUpdate target, float percentage)
        {
            var currentPos = new Vector3d(update.WorldPosition[0], update.WorldPosition[1], update.WorldPosition[2]);
            var targetPos = new Vector3d(target.WorldPosition[0], target.WorldPosition[1], target.WorldPosition[2]);
            var currentVel = new Vector3d(update.WorldVelocity[0], update.WorldVelocity[1], update.WorldVelocity[2]);
            var targetVel = new Vector3d(target.WorldVelocity[0], target.WorldVelocity[1], target.WorldVelocity[2]);

            var lerpedPos = Vector3d.Lerp(currentPos, targetPos, percentage);
            var lerpedVel = Vector3d.Lerp(currentVel, targetVel, percentage);

            var lerpedBody = percentage < 0.5 ? update.Body : target.Body;

            // Keep KSP's own orbit roughly consistent so internal systems don't break. Principia (or any
            // N-body integrator) recomputes the real trajectory from the world state on its next tick.
            // relPos is world position minus the body position (=> body-relative); relVel is the body-frame
            // orbital velocity sent by the owner (same frame), so UpdateFromStateVectors stays consistent.
            var relPos = lerpedPos - lerpedBody.position;
            var relVel = lerpedVel;
            if (vessel.orbit != null)
            {
                vessel.orbit.UpdateFromStateVectors(relPos, relVel, lerpedBody, TimeSyncSystem.UniversalTime);
            }

            vessel.Landed = percentage < 0.5 ? update.Landed : target.Landed;
            vessel.Splashed = percentage < 0.5 ? update.Splashed : target.Splashed;
            vessel.heightFromTerrain = target.HeightFromTerrain;
            var altitude = relPos.magnitude - lerpedBody.Radius;
            vessel.staticPressurekPa = FlightGlobals.getStaticPressure(altitude, lerpedBody);

            var rotation = (Quaternion)lerpedBody.rotation * Quaternion.Slerp(update.SurfaceRelRotation, target.SurfaceRelRotation, percentage);
            SetVesselWorldPositionAndRotation(vessel, lerpedPos, lerpedVel, rotation);

            vessel.protoVessel.UpdatePositionValues(vessel);
        }

        private static void ApplyOrbitInterpolation(Vessel vessel, VesselPositionUpdate update, VesselPositionUpdate target, CelestialBody lerpedBody, float percentage)
        {
            var currentPos = update.KspOrbit.getRelativePositionAtUT(TimeSyncSystem.UniversalTime);
            var targetPos = target.KspOrbit.getRelativePositionAtUT(TimeSyncSystem.UniversalTime);

            var currentVel = update.KspOrbit.getOrbitalVelocityAtUT(TimeSyncSystem.UniversalTime);
            var targetVel = target.KspOrbit.getOrbitalVelocityAtUT(TimeSyncSystem.UniversalTime);

            var lerpedPos = Vector3d.Lerp(currentPos, targetPos, percentage);
            var lerpedVel = Vector3d.Lerp(currentVel, targetVel, percentage);

            //This call will update the orbit PARAMETERS (ecc, sma, inc, etc) based on the vectors you pass as parameters
            //Bear in mind that this method will NOT reposition the vessel!!
            vessel.orbit.UpdateFromStateVectors(lerpedPos, lerpedVel, lerpedBody, TimeSyncSystem.UniversalTime);
        }

        private static void ApplyInterpolationsToVessel(Vessel vessel, VesselPositionUpdate update, VesselPositionUpdate target, CelestialBody lerpedBody, float percentage)
        {
            var currentSurfaceRelRotation = Quaternion.Slerp(update.SurfaceRelRotation, target.SurfaceRelRotation, percentage);

            //If you don't set srfRelRotation and vessel is packed it won't change it's rotation
            vessel.srfRelRotation = currentSurfaceRelRotation;

            vessel.Landed = percentage < 0.5 ? update.Landed : target.Landed;
            vessel.Splashed = percentage < 0.5 ? update.Splashed : target.Splashed;

            vessel.latitude = LunaMath.Lerp(update.LatLonAlt[0], target.LatLonAlt[0], percentage);
            vessel.longitude = LunaMath.Lerp(update.LatLonAlt[1], target.LatLonAlt[1], percentage);
            vessel.altitude = LunaMath.Lerp(update.LatLonAlt[2], target.LatLonAlt[2], percentage);

            var rotation = (Quaternion)lerpedBody.rotation * currentSurfaceRelRotation;
            var position = vessel.situation <= Vessel.Situations.FLYING ?
                lerpedBody.GetWorldSurfacePosition(vessel.latitude, vessel.longitude, vessel.altitude) :
                vessel.orbit.getPositionAtUT(TimeSyncSystem.UniversalTime);

            SetVesselPositionAndRotation(vessel, position, rotation);
        }

        /// <summary>
        /// Here we set the position and the rotation of every part at once, this is much more optimized than calling SetRotation and SetPosition
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
        private static void SetVesselPositionAndRotation(Vessel vessel, Vector3d position, Quaternion rotation)
        {
            if (!vessel.loaded)
            {
                vessel.vesselTransform.position = position;
                vessel.vesselTransform.rotation = rotation;
            }
            else
            {
                for (var i = 0; i < vessel.parts.Count; i++)
                {
                    var part = vessel.parts[i];
                    var partRotation = rotation * part.orgRot;
                    part.partTransform.rotation = partRotation;

                    if (vessel.packed || part.physicalSignificance == Part.PhysicalSignificance.FULL)
                    {
                        // Use the interpolated rotation for part offsets — vessel.vesselTransform.rotation
                        // is stale (previous frame) and causes rotational position lag on large vessels
                        var partPosition = position + rotation * part.orgPos;
                        part.partTransform.position = partPosition;
                    }

                    // For unpacked parts with rigidbodies, sync rb directly so the physics engine
                    // doesn't fight LMP's positioning on the next step (setting only transform.position
                    // on a non-kinematic Rigidbody causes visible oscillation as physics snaps it back)
                    if (!vessel.packed && part.rb)
                    {
                        part.rb.rotation = partRotation;
                        if (part.physicalSignificance == Part.PhysicalSignificance.FULL)
                            part.rb.position = part.partTransform.position;
                    }

                    //We always need to set the part velocity (and it's rigidbody velocity)! Otherwise during dockings it won't be possible to dock
                    part.ResumeVelocity();
                }
            }
        }

        /// <summary>
        /// N-body counterpart of SetVesselPositionAndRotation: positions every part from a world-space position
        /// and also feeds the interpolated world velocity into the rigidbodies so the local integrator has a
        /// correct seed state.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
        private static void SetVesselWorldPositionAndRotation(Vessel vessel, Vector3d position, Vector3d velocity, Quaternion rotation)
        {
            if (!vessel.loaded)
            {
                vessel.vesselTransform.position = position;
                vessel.vesselTransform.rotation = rotation;
            }
            else
            {
                for (var i = 0; i < vessel.parts.Count; i++)
                {
                    var part = vessel.parts[i];
                    var partRotation = rotation * part.orgRot;
                    part.partTransform.rotation = partRotation;

                    var partPosition = position + rotation * part.orgPos;
                    part.partTransform.position = partPosition;

                    if (!vessel.packed && part.rb)
                    {
                        part.rb.rotation = partRotation;
                        if (part.physicalSignificance == Part.PhysicalSignificance.FULL)
                        {
                            part.rb.position = part.partTransform.position;
                            part.rb.velocity = velocity;
                        }
                    }

                    part.ResumeVelocity();
                }
            }
        }
    }
}
