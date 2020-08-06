using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PersistentThrust
{
    public static class EngineBackgroundProcessing
    {
        public static string BackgroundUpdateExecution(
            Vessel vessel,
            ProtoPartSnapshot part_snapshot,
            ProtoPartModuleSnapshot module_snapshot,
            PartModule proto_part_module,
            Part proto_part,
            Dictionary<string, double> availableResources,
            List<KeyValuePair<string, double>> resourceChangeRequest,
            double elapsed_s)
        {
            PersistentBackgroundProcessing.VesselDataDict.TryGetValue(vessel.id, out VesselData vesselData);
            if (vesselData == null)
                return proto_part.partInfo.title;

            double persistentThrust = 0;
            if (!module_snapshot.moduleValues.TryGetValue(nameof(persistentThrust), ref persistentThrust))
                return proto_part.partInfo.title;

            // ignore background update when no thrust generated
            if (persistentThrust <= 0)
                return proto_part.partInfo.title;

            double vesselAlignmentWithAutopilotMode = 0;
            if (!module_snapshot.moduleValues.TryGetValue(nameof(vesselAlignmentWithAutopilotMode),
                ref vesselAlignmentWithAutopilotMode))
                return proto_part.partInfo.title;

            // ignore background update when not aligned with autopilot mode
            if (vesselAlignmentWithAutopilotMode < 0.995)
                return proto_part.partInfo.title;

            string persistentResourceChange = module_snapshot.moduleValues.GetValue(nameof(persistentResourceChange));

            if (string.IsNullOrEmpty(persistentResourceChange))
                return proto_part.partInfo.title;

            Dictionary<string, double> resourceChanges = persistentResourceChange
                .Split(';').Select(s => s.Trim().Split('='))
                .ToDictionary(a => a[0], a => double.Parse(a[1]));

            if (resourceChanges.Any(m => m.Value == 0))
                return proto_part.partInfo.title;

            VesselAutopilot.AutopilotMode persistentAutopilotMode = (VesselAutopilot.AutopilotMode)Enum.Parse(
                typeof(VesselAutopilot.AutopilotMode),
                module_snapshot.moduleValues.GetValue(nameof(persistentAutopilotMode)));

            Orbit orbit = vessel.GetOrbit();
            double UT = Planetarium.GetUniversalTime();
            Vector3d orbitalVelocityAtUt = orbit.getOrbitalVelocityAtUT(UT).xzy;

            Vector3d thrustVector = Vector3d.zero;
            switch (persistentAutopilotMode)
            {
                case VesselAutopilot.AutopilotMode.StabilityAssist:
                    thrustVector = vessel.GetTransform().up.normalized;
                    break;
                case VesselAutopilot.AutopilotMode.Prograde:
                    thrustVector = orbitalVelocityAtUt;
                    break;
                case VesselAutopilot.AutopilotMode.Retrograde:
                    thrustVector = -orbitalVelocityAtUt;
                    break;
                case VesselAutopilot.AutopilotMode.Normal:
                    thrustVector = Vector3.Cross(orbitalVelocityAtUt,
                        orbit.getPositionAtUT(UT) - vessel.mainBody.getPositionAtUT(UT));
                    break;
                case VesselAutopilot.AutopilotMode.Antinormal:
                    thrustVector = -Vector3.Cross(orbitalVelocityAtUt,
                        orbit.getPositionAtUT(UT) - vessel.mainBody.getPositionAtUT(UT));
                    break;
                case VesselAutopilot.AutopilotMode.RadialIn:
                    thrustVector = -Vector3.Cross(orbitalVelocityAtUt,
                        Vector3.Cross(orbitalVelocityAtUt,
                            vessel.orbit.getPositionAtUT(UT) - orbit.referenceBody.position));
                    break;
                case VesselAutopilot.AutopilotMode.RadialOut:
                    thrustVector = Vector3.Cross(orbitalVelocityAtUt,
                        Vector3.Cross(orbitalVelocityAtUt,
                            vessel.orbit.getPositionAtUT(UT) - orbit.referenceBody.position));
                    break;
                case VesselAutopilot.AutopilotMode.Target:
                    thrustVector = GetThrustVectorToTarget(vessel, module_snapshot, UT);
                    break;
                case VesselAutopilot.AutopilotMode.AntiTarget:
                    thrustVector = -GetThrustVectorToTarget(vessel, module_snapshot, UT);
                    break;
                    //case VesselAutopilot.AutopilotMode.Maneuver:
                    //    thrustVector = orbit.GetThrustVectorToManeuver(moduleSnapshot);
                    //    break;
            }

            if (thrustVector == Vector3d.zero)
                return proto_part.partInfo.title;

            double fuelRequirementMet = 1;
            foreach (var resourceChange in resourceChanges)
            {
                var fixedRequirement = -resourceChange.Value * elapsed_s;

                if (availableResources.TryGetValue(resourceChange.Key, out double availableAmount))
                {
                    var resourceDefinition = PartResourceLibrary.Instance.GetDefinition(resourceChange.Key);

                    if ((resourceDefinition.density <= 0 && CheatOptions.InfiniteElectricity) ||
                        resourceDefinition.density > 0 && CheatOptions.InfinitePropellant)
                        fuelRequirementMet = 1;
                    else
                        fuelRequirementMet = availableAmount > 0 && fixedRequirement > 0
                            ? Math.Min(fuelRequirementMet, availableAmount / fixedRequirement)
                            : 0;
                }
                else
                    fuelRequirementMet = 0;
            }

            if (fuelRequirementMet <= 0)
                return proto_part.partInfo.title;

            foreach (var resourceChange in resourceChanges)
            {
                var resourceDefinition = PartResourceLibrary.Instance.GetDefinition(resourceChange.Key);

                if ((resourceDefinition.density <= 0 && CheatOptions.InfiniteElectricity) ||
                    resourceDefinition.density > 0 && CheatOptions.InfinitePropellant)
                    continue;

                resourceChangeRequest.Add(new KeyValuePair<string, double>(resourceChange.Key,
                    resourceChange.Value * fuelRequirementMet));
            }

            double persistentAverageDensity = 0;
            if (!module_snapshot.moduleValues.TryGetValue(nameof(persistentAverageDensity),
                ref persistentAverageDensity))
                return proto_part.partInfo.title;

            float persistentIsp = 0;
            if (!module_snapshot.moduleValues.TryGetValue(nameof(persistentIsp), ref persistentIsp))
                return proto_part.partInfo.title;

            Vector3d deltaVVector = Utils.CalculateDeltaVVector(persistentAverageDensity, vesselData.TotalVesselMassInTon, elapsed_s,
                persistentThrust * fuelRequirementMet, persistentIsp, thrustVector.normalized);

            orbit.Perturb(deltaVVector, UT);

            return proto_part.partInfo.title;
        }

        private static Vector3d GetThrustVectorToTarget(Vessel vessel, ProtoPartModuleSnapshot moduleSnapshot, double UT)
        {
            string persistentVesselTargetBodyName = string.Empty;
            moduleSnapshot.moduleValues.TryGetValue(nameof(persistentVesselTargetBodyName), ref persistentVesselTargetBodyName);

            string persistentVesselTargetId = Guid.Empty.ToString();
            moduleSnapshot.moduleValues.TryGetValue(nameof(persistentVesselTargetId), ref persistentVesselTargetId);

            Guid persistentVesselTargetGuid = new Guid(persistentVesselTargetId);

            Orbit targetOrbit = null;
            if (persistentVesselTargetGuid != Guid.Empty)
            {
                Vessel targetVessel = FlightGlobals.Vessels.SingleOrDefault(m => m.id == persistentVesselTargetGuid);
                if (targetVessel != null)
                    targetOrbit = targetVessel.GetOrbit();
            }
            else if (!string.IsNullOrEmpty(persistentVesselTargetBodyName))
            {
                CelestialBody body = FlightGlobals.Bodies.SingleOrDefault(m => m.bodyName == persistentVesselTargetBodyName);
                if (body != null)
                    targetOrbit = body.GetOrbit();
            }

            return targetOrbit != null ? targetOrbit.getPositionAtUT(UT) - vessel.orbit.getPositionAtUT(UT) : Vector3d.zero;
        }
    }
}
