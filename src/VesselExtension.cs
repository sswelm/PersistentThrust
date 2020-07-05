using System;
using UnityEngine;

namespace PersistentThrust
{
    public static class VesselExtension
    {
        public static double PersistHeading(this ModuleEngines engine, float fixedDeltaTime, float headingTolerance = 0.001f, bool forceRotation = false, bool canDropOutOfTimeWarp = true)
        {
            if (engine.getIgnitionState == false)
                return 0;

            var vessel = engine.vessel;

            if (!vessel.packed && !forceRotation)
                return 0;

            var canPersistDirection = vessel.situation == Vessel.Situations.SUB_ORBITAL || vessel.situation == Vessel.Situations.ESCAPING || vessel.situation == Vessel.Situations.ORBITING;

            if (!canPersistDirection)
                return 0;

            var sasIsActive = vessel.ActionGroups[KSPActionGroup.SAS];

            if (!sasIsActive)
                return 0;

            var requestedDirection = GetRequestedDirection(engine, Planetarium.GetUniversalTime());

            if (requestedDirection == Vector3d.zero) 
                return 1;

            var ratioHeadingVersusRequest = Vector3d.Dot(vessel.transform.up.normalized, requestedDirection);

            var finalTolerance =  Math.Min(0.995, (1 - Math.Min(1, fixedDeltaTime * headingTolerance)));

            if (forceRotation || ratioHeadingVersusRequest > finalTolerance)
            {
                vessel.transform.Rotate(Quaternion.FromToRotation(vessel.transform.up.normalized, requestedDirection).eulerAngles, Space.World);
                vessel.SetRotation(vessel.transform.rotation);
                return 1;
            }
            else if (engine.currentThrottle == 0 || canDropOutOfTimeWarp == false)
            {
                return ratioHeadingVersusRequest;
            }
            else
            {
                var directionName = Enum.GetName(typeof(VesselAutopilot.AutopilotMode), vessel.Autopilot.Mode);
                var message = "Persistent Thrust stopped - vessel is not facing " + directionName;
                ScreenMessages.PostScreenMessage(message, 5, ScreenMessageStyle.UPPER_CENTER);
                Debug.Log("[PersistentThrust]: " + message);
                TimeWarp.SetRate(0, true);

                return ratioHeadingVersusRequest;
            }
        }

        public static double VesselHeadingVersusAutopilotVector(this ModuleEngines engine, double universalTime)
        {
            var requestedDirection = GetRequestedDirection(engine, universalTime);

            return Vector3d.Dot(engine.vessel.transform.up.normalized, requestedDirection);
        }

        public static double VesselOrbitHeadingVersusManeuverVector(this Vessel vessel)
        {
            if (vessel == null || vessel.patchedConicSolver == null || vessel.orbit == null ||  vessel.patchedConicSolver.maneuverNodes == null)
                return -1;

            if (vessel.patchedConicSolver.maneuverNodes.Count > 0)
            {
                var maneuverNode = vessel.patchedConicSolver.maneuverNodes[0];

                if (maneuverNode == null)
                    return -1;

                //var forward = Vector3d.Dot(maneuverNode.patch.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime()).normalized, vessel.orbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime())) > 0;

                //if (forward)
                    return Vector3d.Dot(vessel.obt_velocity.normalized,maneuverNode.GetBurnVector(vessel.orbit).normalized);
                //else
                //    return Vector3d.Dot(-vessel.obt_velocity.normalized,maneuverNode.GetBurnVector(vessel.orbit).normalized);
            }
            else
                return 1;
        }

        public static Vector3d GetRequestedDirection(this ModuleEngines engine, double universalTime)
        {
            var vessel = engine.vessel;
            var requestedDirection = Vector3d.zero;
            var vesselPosition = vessel.orbit.getPositionAtUT(universalTime);

            switch (vessel.Autopilot.Mode)
            {
                case VesselAutopilot.AutopilotMode.Prograde:
                    requestedDirection = vessel.obt_velocity.normalized;
                    break;
                case VesselAutopilot.AutopilotMode.Retrograde:
                    requestedDirection = -vessel.obt_velocity.normalized;
                    break;
                case VesselAutopilot.AutopilotMode.Maneuver:
                    requestedDirection = vessel.patchedConicSolver.maneuverNodes.Count > 0 ? vessel.patchedConicSolver.maneuverNodes[0].GetBurnVector(vessel.orbit).normalized : vessel.obt_velocity.normalized;
                    break;
                case VesselAutopilot.AutopilotMode.Target:
                    requestedDirection = (vessel.targetObject.GetOrbit().getPositionAtUT(universalTime) - vesselPosition).normalized;
                    break;
                case VesselAutopilot.AutopilotMode.AntiTarget:
                    requestedDirection = -(vessel.targetObject.GetOrbit().getPositionAtUT(universalTime) - vesselPosition).normalized;
                    break;
                case VesselAutopilot.AutopilotMode.Normal:
                    requestedDirection = Vector3.Cross(vessel.obt_velocity, vesselPosition - vessel.mainBody.position).normalized;
                    break;
                case VesselAutopilot.AutopilotMode.Antinormal:
                    requestedDirection = -Vector3.Cross(vessel.obt_velocity, vesselPosition - vessel.mainBody.position).normalized;
                    break;
                case VesselAutopilot.AutopilotMode.RadialIn:
                    requestedDirection = -Vector3.Cross(vessel.obt_velocity, Vector3.Cross(vessel.obt_velocity, vesselPosition - vessel.mainBody.position)).normalized;
                    break;
                case VesselAutopilot.AutopilotMode.RadialOut:
                    requestedDirection = Vector3.Cross(vessel.obt_velocity, Vector3.Cross(vessel.obt_velocity, vesselPosition - vessel.mainBody.position)).normalized;
                    break;
            }

            return requestedDirection;
        }
    }
}
