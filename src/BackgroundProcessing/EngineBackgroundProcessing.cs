using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PersistentThrust.BackgroundProcessing
{
    public static class EngineBackgroundProcessing
    {
        public static PersistentEngineData LoadPersistentEngine(ProtoPartSnapshot protoPartSnapshot, VesselData vesselData, Part protoPart)
        {
            var persistentEngine = protoPart?.FindModuleImplementing<PersistentEngine>();

            if (persistentEngine is null)
                return null;

            ProtoPartModuleSnapshot persistentEngineModuleSnapshot = protoPartSnapshot.FindModule(nameof(PersistentEngine));

            if (persistentEngineModuleSnapshot == null)
                return null;

            var engineData = new PersistentEngineData
            {
                ProtoPart = protoPart,
                ProtoPartSnapshot = protoPartSnapshot,
                PersistentPartId = protoPartSnapshot.persistentId,
                ProtoPartModuleSnapshot = persistentEngineModuleSnapshot,
                PersistentEngine = persistentEngine
            };

            // store data
            vesselData.Engines.Add(protoPartSnapshot.persistentId, engineData);

            // Load persistentThrust from ProtoPartModuleSnapshots
            persistentEngineModuleSnapshot.moduleValues.TryGetValue(nameof(persistentEngine.persistentThrust), ref persistentEngine.persistentThrust);

            var moduleEngines = protoPart.FindModulesImplementing<ModuleEngines>();

            if (moduleEngines == null || moduleEngines.Any() == false)
                return null;

            // assume all engine modules are of the same partModule
            var engineModuleName = moduleEngines.First().moduleName;

            List<ProtoPartModuleSnapshot> moduleEngineSnapshots = protoPartSnapshot.modules.Where(m => m.moduleName == engineModuleName).ToList();

            var persistentEngineModules = new List<PersistentEngineModule>();
            for (int i = 0; i < moduleEngines.Count; i++)
            {
                ModuleEngines moduleEngine = moduleEngines[i];
                ProtoPartModuleSnapshot moduleEngineSnapshot = moduleEngineSnapshots[i];

                List<PersistentPropellant> persistentPropellants = PersistentPropellant.MakeList(moduleEngine.propellants);

                var persistentEngineModule = new PersistentEngineModule
                {
                    propellants = persistentPropellants,
                    averageDensity = persistentPropellants.AverageDensity()
                };
                persistentEngineModules.Add(persistentEngineModule);
            }

            persistentEngine.moduleEngines = persistentEngineModules.ToArray();

            // check if there any active persistent engines that need to be processed
            if (persistentEngine.persistentThrust > 0)
                vesselData.HasAnyActivePersistentEngine = true;
            else
                return null;

            return engineData;
        }

        public static void ProcessUnloadedPersistentEngines(VesselData vesselData)
        {
            // ignore landed or floating vessels
            if (vesselData.Vessel.LandedOrSplashed)
                return;

            vesselData.PersistentThrust = 0;

            foreach (KeyValuePair<uint, PersistentEngineData> keyValuePair in vesselData.Engines)
            {
                PersistentEngineData persistentEngineData = keyValuePair.Value;

                double.TryParse(persistentEngineData.ProtoPartModuleSnapshot.moduleValues.GetValue(nameof(persistentEngineData.PersistentEngine.persistentThrust)), out persistentEngineData.PersistentEngine.persistentThrust);
                if (persistentEngineData.PersistentEngine.persistentThrust <= 0)
                    continue;

                vesselData.PersistentThrust += persistentEngineData.PersistentEngine.persistentThrust;

                var resourceChangeRequests = new List<KeyValuePair<string, double>>();

                ProcessUnloadedPersistentEngine(persistentEngineData.ProtoPartSnapshot, vesselData, resourceChangeRequests);

                foreach (KeyValuePair<string, double> resourceChangeRequest in resourceChangeRequests)
                {
                    vesselData.ResourceChange(resourceChangeRequest.Key, resourceChangeRequest.Value);
                }
            }
        }

        private static void ProcessUnloadedPersistentEngine(
            ProtoPartSnapshot protoPartSnapshot,
            VesselData vesselData,
            List<KeyValuePair<string, double>> resourceChangeRequest)
        {
            if (DetectKerbalism.Found())
                return;

            // lookup engine data
            vesselData.Engines.TryGetValue(protoPartSnapshot.persistentId, out PersistentEngineData engineData);

            if (engineData == null)
            {
                Debug.LogWarning("[PersistentThrust]: Fail to find Engine Data for persistentId " + protoPartSnapshot.persistentId);
                return;
            }

            // execute persistent engine BackgroundUpdate
            PersistentEngine.BackgroundUpdate(
                vessel: vesselData.Vessel,
                part_snapshot: protoPartSnapshot,
                module_snapshot: engineData.ProtoPartModuleSnapshot,
                proto_part_module: engineData.PersistentEngine,
                proto_part: engineData.ProtoPart,
                availableResources: vesselData.AvailableResources,
                resourceChangeRequest: resourceChangeRequest,
                elapsed_s: TimeWarp.fixedDeltaTime);
        }

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

            //Orbit orbit = vessel.GetOrbit();
            double UT = Planetarium.GetUniversalTime();

            PersistentScenarioModule.VesselDataDict.TryGetValue(vessel.id, out VesselData vesselData);
            if (vesselData == null)
                return proto_part.partInfo.title;

            // calculate thrust vector only once
            if (vesselData.ThrustVector == Vector3d.zero)
                vesselData.ThrustVector = GetThrustVectorForAutoPilot(vessel, module_snapshot, persistentAutopilotMode, vesselData, UT);

            if (vesselData.ThrustVector == Vector3d.zero)
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

            vesselData.Engines.TryGetValue(part_snapshot.persistentId, out PersistentEngineData engineData);
            if (engineData == null)
                return proto_part.partInfo.title;

            engineData.DeltaVVector = Utils.CalculateDeltaVVector(persistentAverageDensity, vesselData.TotalVesselMassInTon, elapsed_s,
                persistentThrust * fuelRequirementMet, persistentIsp, vesselData.ThrustVector.normalized);

            return proto_part.partInfo.title;
        }

        public static Vector3d GetThrustVectorForAutoPilot(
            Vessel vessel, 
            ProtoPartModuleSnapshot moduleSnapshot,
            VesselAutopilot.AutopilotMode persistentAutopilotMode, 
            VesselData vesselData, 
            double UT)
        {
            Vector3d thrustVector = Vector3d.zero;
            switch (persistentAutopilotMode)
            {
                case VesselAutopilot.AutopilotMode.StabilityAssist:
                    thrustVector = vessel.GetTransform().up.normalized;
                    break;
                case VesselAutopilot.AutopilotMode.Prograde:
                    thrustVector = vesselData.OrbitalVelocityAtUt;
                    break;
                case VesselAutopilot.AutopilotMode.Retrograde:
                    thrustVector = -vesselData.OrbitalVelocityAtUt;
                    break;
                case VesselAutopilot.AutopilotMode.Normal:
                    thrustVector = Vector3.Cross(vesselData.OrbitalVelocityAtUt,
                        vesselData.Orbit.getPositionAtUT(UT) - vessel.mainBody.getPositionAtUT(UT));
                    break;
                case VesselAutopilot.AutopilotMode.Antinormal:
                    thrustVector = -Vector3.Cross(vesselData.OrbitalVelocityAtUt,
                        vesselData.Orbit.getPositionAtUT(UT) - vessel.mainBody.getPositionAtUT(UT));
                    break;
                case VesselAutopilot.AutopilotMode.RadialIn:
                    thrustVector = -Vector3.Cross(vesselData.OrbitalVelocityAtUt,
                        Vector3.Cross(vesselData.OrbitalVelocityAtUt,
                            vesselData.Orbit.getPositionAtUT(UT) - vesselData.Orbit.referenceBody.position));
                    break;
                case VesselAutopilot.AutopilotMode.RadialOut:
                    thrustVector = Vector3.Cross(vesselData.OrbitalVelocityAtUt,
                        Vector3.Cross(vesselData.OrbitalVelocityAtUt,
                            vesselData.Orbit.getPositionAtUT(UT) - vesselData.Orbit.referenceBody.position));
                    break;
                case VesselAutopilot.AutopilotMode.Target:
                    thrustVector = GetThrustVectorToTarget(vessel, moduleSnapshot, UT);
                    break;
                case VesselAutopilot.AutopilotMode.AntiTarget:
                    thrustVector = -GetThrustVectorToTarget(vessel, moduleSnapshot, UT);
                    break;
                case VesselAutopilot.AutopilotMode.Maneuver:
                    thrustVector = vesselData.Orbit.GetThrustVectorToManeuver(moduleSnapshot);
                    break;
            }

            return thrustVector;
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
