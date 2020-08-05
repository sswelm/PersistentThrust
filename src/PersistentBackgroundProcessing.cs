using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PersistentThrust
{
    public class EngineData
    {
        public uint PersistentPartId { get; set; }
        public Part ProtoPart { get; set; }
        public PersistentEngine PersistentEngine { get; set; }
        public ProtoPartModuleSnapshot ProtoPartModuleSnapshot { get; set; }
    }

    public class VesselData
    {
        public uint VesselPersistentId { get; set; }

        public double TotalVesselMassInKg { get; set; }
        public double TotalVesselMass { get; set; }
        public bool? HasAnyActivePersistentEngine { get; set; }

        public Dictionary<uint, EngineData> Engines { get; set; } = new Dictionary<uint, EngineData>();

        public VesselData(uint vesselPersistentId)
        {
            VesselPersistentId = vesselPersistentId;
        }

        public void UpdateMass(Vessel vessel)
        {
            TotalVesselMass = 0;

            // for each part
            foreach (ProtoPartSnapshot protoPartSnapshot in vessel.protoVessel.protoPartSnapshots)
            {
                TotalVesselMass += protoPartSnapshot.mass;
                foreach (var resource in protoPartSnapshot.resources)
                {
                    TotalVesselMass += resource.amount * resource.definition.density;
                }
            }

            TotalVesselMassInKg = TotalVesselMass * 1000;
        }
    }


    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new[] {GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT, GameScenes.EDITOR})]
    public sealed class PersistentBackgroundProcessing : ScenarioModule
    {
        public static readonly Dictionary<uint, VesselData> VesselDataDict = new Dictionary<uint, VesselData>();

        /// <summary>
        /// Called by the part every refresh frame where it is active, which can be less frequent than FixedUpdate which is called every processing frame
        /// </summary>
        void FixedUpdate()
        {
            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                // ignore Kerbals
                if (vessel.isEVA)
                    continue;

                // ignore landed or floating vessels
                if (vessel.LandedOrSplashed)
                    continue;

                // ignore irrelevant vessel types
                if (vessel.vesselType == VesselType.Debris
                    || vessel.vesselType == VesselType.Flag
                    || vessel.vesselType == VesselType.SpaceObject
                    || vessel.vesselType == VesselType.DeployedSciencePart
                    || vessel.vesselType == VesselType.DeployedScienceController
                )
                    continue;

                // ignore irrelevant vessel situations
                if ((vessel.situation & Vessel.Situations.PRELAUNCH) == Vessel.Situations.PRELAUNCH
                    || (vessel.situation & Vessel.Situations.FLYING) == Vessel.Situations.FLYING)
                    continue;

                // lookup cashed vessel data
                if (!VesselDataDict.TryGetValue(vessel.persistentId, out VesselData vesselData))
                {
                    vesselData = new VesselData(vessel.persistentId);
                    VesselDataDict.Add(vessel.persistentId, vesselData);
                }

                // update vessel data when loaded
                if (vessel.loaded)
                {
                    ProcessesLoadedVessel(vessel, vesselData);
                    continue;
                }

                // update mass if any engines
                if (vesselData.Engines.Any())
                {
                    vesselData.UpdateMass(vessel);
                }

                // skip further processing if no persistent engine active present
                if (vesselData.HasAnyActivePersistentEngine.HasValue && vesselData.HasAnyActivePersistentEngine.Value == false)
                    continue;

                // look for active persistent engines in all vessel parts
                LoadUnloadedParts(vessel, vesselData);

                // process persistent engine if we already found any active persistent engine
                ProcessUnloadedPersistentEngines(vesselData, vessel);
            }
        }

        private static void ProcessUnloadedPersistentEngines(VesselData vesselData, Vessel vessel)
        {
            if (!vesselData.Engines.Any())
                return;

            vesselData.UpdateMass(vessel);

            // determine available resources
            GetAvailableResources(vessel, out Dictionary<string, double> availableResources, out Dictionary<string, double> maxAmountResources);

            var totalResourceChangeRequests = new Dictionary<string, double>();

            foreach (var engine in vesselData.Engines)
            {
                ProtoPartSnapshot protoPartSnapshot = vessel.protoVessel.protoPartSnapshots
                    .FirstOrDefault(m => m.persistentId == engine.Value.PersistentPartId);

                if (protoPartSnapshot == null)
                {
                    vesselData.HasAnyActivePersistentEngine = null;
                    Debug.LogWarning("[PersistentThrust]: Fail to find protoPartSnapshot " + engine.Value.PersistentPartId);
                    continue;
                }

                var resourceChangeRequest = new List<KeyValuePair<string, double>>();

                ProcessUnloadedPersistentEngine(protoPartSnapshot, vessel, vesselData, availableResources, resourceChangeRequest);

                foreach (var keyValuePair in resourceChangeRequest)
                {
                    totalResourceChangeRequests.TryGetValue(keyValuePair.Key, out double requestedAmount);
                    totalResourceChangeRequests[keyValuePair.Key] = requestedAmount - keyValuePair.Value;
                }
            }

            UpdateResources(vessel, totalResourceChangeRequests, availableResources);
        }

        private static void UpdateResources(
            Vessel vessel, 
            Dictionary<string, double> totalResourceChangeRequests,
            Dictionary<string, double> availableResources)
        {
            foreach (var resourceRequest in totalResourceChangeRequests)
            {
                availableResources.TryGetValue(resourceRequest.Key, out double available);

                if (available < float.Epsilon)
                    available = 0;

                foreach (ProtoPartSnapshot protoPartSnapshot in vessel.protoVessel.protoPartSnapshots)
                {
                    foreach (var protoPartResourceSnapshot in protoPartSnapshot.resources)
                    {
                        if (protoPartResourceSnapshot.resourceName != resourceRequest.Key) continue;

                        var fraction = available < float.Epsilon ? 1 : protoPartResourceSnapshot.amount / available;

                        var resourceChange = resourceRequest.Value * fraction * TimeWarp.fixedDeltaTime;

                        if (protoPartResourceSnapshot.amount > float.Epsilon)
                            protoPartResourceSnapshot.amount = System.Math.Max(0, protoPartResourceSnapshot.amount - resourceChange);
                        else
                            protoPartResourceSnapshot.amount = 0;
                    }
                }
            }
        }

        private static void GetAvailableResources(Vessel vessel, out Dictionary<string, double> availableResources, out Dictionary<string, double> maxAmountResources)
        {
            availableResources = new Dictionary<string, double>();
            maxAmountResources = new Dictionary<string, double>();

            foreach (ProtoPartSnapshot protoPartSnapshot in vessel.protoVessel.protoPartSnapshots)
            {
                foreach (var protoPartResourceSnapshot in protoPartSnapshot.resources)
                {
                    availableResources.TryGetValue(protoPartResourceSnapshot.resourceName, out double amount);
                    maxAmountResources.TryGetValue(protoPartResourceSnapshot.resourceName, out double maxAmount);

                    availableResources[protoPartResourceSnapshot.resourceName] = amount + protoPartResourceSnapshot.amount;
                    maxAmountResources[protoPartResourceSnapshot.resourceName] = maxAmount + protoPartResourceSnapshot.maxAmount;
                }
            }
        }

        private static void LoadUnloadedParts(Vessel vessel, VesselData vesselData)
        {
            if (vesselData.Engines.Any())
                return;

            // initially assume no active persistent engine present   
            vesselData.HasAnyActivePersistentEngine = false;

            foreach (ProtoPartSnapshot protoPartSnapshot in vessel.protoVessel.protoPartSnapshots)
            {
                LoadUnloadedPart(protoPartSnapshot, vesselData);
            }
        }

        private static void ProcessesLoadedVessel(Vessel vessel, VesselData vesselData)
        {
            var persistentEngines = vessel.FindPartModulesImplementing<PersistentEngine>().ToList();

            foreach (PersistentEngine persistentEngine in persistentEngines)
            {
                vesselData.Engines.TryGetValue(persistentEngine.part.persistentId, out EngineData engineData);

                if (engineData != null)
                {
                    engineData.PersistentEngine.persistentThrust = persistentEngine.persistentThrust;
                }
            }

            vesselData.HasAnyActivePersistentEngine = persistentEngines.Any(m => m.persistentThrust > 0);
        }

        private static bool LoadUnloadedPart(ProtoPartSnapshot protoPartSnapshot, VesselData vesselData)
        {
            AvailablePart availablePart = PartLoader.getPartInfoByName(protoPartSnapshot.partName);

            Part protoPart = availablePart?.partPrefab;

            var persistentEngine = protoPart?.FindModuleImplementing<PersistentEngine>();

            if (persistentEngine is null)
                return false;

            ProtoPartModuleSnapshot protoPartModuleSnapshot = protoPartSnapshot.modules.FirstOrDefault(m => m.moduleName == nameof(PersistentEngine));

            if (protoPartModuleSnapshot == null)
                return false;

            // Load persistentThrust from ProtoPartModuleSnapshot
            protoPartModuleSnapshot.moduleValues.TryGetValue(nameof(persistentEngine.persistentThrust), ref persistentEngine.persistentThrust);

            // store data
            vesselData.Engines.Add(protoPartSnapshot.persistentId, new EngineData
            {
                PersistentPartId = protoPartSnapshot.persistentId,
                ProtoPart = protoPart,
                ProtoPartModuleSnapshot = protoPartModuleSnapshot,
                PersistentEngine = persistentEngine
            });

            // check if there any active persistent engines that need to be processed
            if (persistentEngine.persistentThrust > 0)
                vesselData.HasAnyActivePersistentEngine = true;
            else
                return false;

            return true;
        }

        private static void ProcessUnloadedPersistentEngine(
            ProtoPartSnapshot protoPartSnapshot, 
            Vessel vessel, 
            VesselData vesselData, 
            Dictionary<string, double> availableResources, 
            List<KeyValuePair<string, double>> resourceChangeRequest)
        {
            if (DetectKerbalism.Found())
                return;

            // lookup engine data
            vesselData.Engines.TryGetValue(protoPartSnapshot.persistentId, out EngineData engineData);

            if (engineData == null)
            {
                Debug.LogWarning("[PersistentThrust]: Fail to find Engine Data for persistentId " + protoPartSnapshot.persistentId);
                return;
            }

            // execute persistent engine BackgroundUpdate
            PersistentEngine.BackgroundUpdate(
                vessel: vessel, 
                part_snapshot: protoPartSnapshot, 
                module_snapshot: engineData.ProtoPartModuleSnapshot, 
                proto_part_module: engineData.PersistentEngine, 
                proto_part: engineData.ProtoPart, 
                availableResources: availableResources, 
                resourceChangeRequest: resourceChangeRequest, 
                elapsed_s: TimeWarp.fixedDeltaTime);
        }
    }
}
