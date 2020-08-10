using System.Collections.Generic;
using System.Linq;
using LibNoise;
using UnityEngine;

namespace PersistentThrust
{
    public class VesselData
    {
        public uint VesselPersistentId { get; set; }
        public HashSet<uint> PersistentEnginePartIds { get; set; } = new HashSet<uint>();

        public double TotalVesselMassInKg { get; set; }
        public double TotalVesselMass { get; set; }
        public bool? HasAnyActivePersistentEngine { get; set; }

        public Dictionary<uint, Part> ProtoParts { get; set; } = new Dictionary<uint, Part>();
        public Dictionary<uint, PersistentEngine> PersistentEngines { get; set; } = new Dictionary<uint, PersistentEngine>();
        public Dictionary<uint, ProtoPartModuleSnapshot> ProtoPartModuleSnapshots { get; set; } = new Dictionary<uint, ProtoPartModuleSnapshot>();

        public VesselData(uint vesselPersistentId)
        {
            VesselPersistentId = vesselPersistentId;
        }

        public void UpdateMass(Vessel vessel)
        {
            if (TotalVesselMass != 0)
                return;

            // for each part
            foreach (ProtoPartSnapshot protoPartSnapshot in vessel.protoVessel.protoPartSnapshots)
            {
                TotalVesselMass += protoPartSnapshot.mass;
                foreach (var resource in protoPartSnapshot.resources)
                {
                    TotalVesselMass += resource.amount * resource.definition.density;
                }
            }

            TotalVesselMassInKg = TotalVesselMass / 1000;
        }
    }


    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new[] {GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT, GameScenes.EDITOR})]
    public sealed class BackgroundProcessing : ScenarioModule
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

                // skip further processing if no persistent engine active present
                if (vesselData.HasAnyActivePersistentEngine.HasValue && vesselData.HasAnyActivePersistentEngine.Value == false)
                    continue;

                // look for active persistent engines in all vessel parts
                LoadUnloadedParts(vessel, vesselData);

                if (DetectKerbalism.Found())
                    continue;

                // process persistent engine if we already found any active persistent engine
                ProcessUnloadedPersistentEngines(vesselData, vessel);
            }
        }

        private static void ProcessUnloadedPersistentEngines(VesselData vesselData, Vessel vessel)
        {
            if (!vesselData.PersistentEnginePartIds.Any())
                return;

            //reset mass to initiate a recalculation
            vesselData.TotalVesselMass = 0;

            // determine available resources
            GetAvailableResources(vessel, out Dictionary<string, double> availableResources, out Dictionary<string, double> maxAmountResources);

            var totalResourceChangeRequests = new Dictionary<string, double>();

            foreach (var vesselDataPersistentEnginePartId in vesselData.PersistentEnginePartIds)
            {
                ProtoPartSnapshot protoPartSnapshot = vessel.protoVessel.protoPartSnapshots
                    .FirstOrDefault(m => m.persistentId == vesselDataPersistentEnginePartId);

                if (protoPartSnapshot == null)
                {
                    vesselData.HasAnyActivePersistentEngine = null;
                    Debug.Log("[PersistentThrust]: Fail to find protoPartSnapshot " + vesselDataPersistentEnginePartId);
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

            foreach (var resourceRequest in totalResourceChangeRequests)
            {
                availableResources.TryGetValue(resourceRequest.Key, out double available);

                if (available < float.Epsilon)
                    available = 0;

                foreach (ProtoPartSnapshot protoPartSnapshot in vessel.protoVessel.protoPartSnapshots)
                {
                    foreach (var protoPartResourceSnapshot in protoPartSnapshot.resources)
                    {
                        if (protoPartResourceSnapshot.resourceName == resourceRequest.Key)
                        {
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

            return;
        }

        private static void LoadUnloadedParts(Vessel vessel, VesselData vesselData)
        {
            if (vesselData.PersistentEnginePartIds.Any())
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
            vesselData.PersistentEngines = vessel.FindPartModulesImplementing<PersistentEngine>().ToDictionary(m => m.part.persistentId);

            vesselData.HasAnyActivePersistentEngine = vesselData.PersistentEngines.Any(m => m.Value.persistentThrust > 0);
        }

        private static bool LoadUnloadedPart(ProtoPartSnapshot protoPartSnapshot, VesselData vesselData)
        {
            AvailablePart availablePart = PartLoader.getPartInfoByName(protoPartSnapshot.partName);

            var protoPart = availablePart?.partPrefab;

            var persistentEngines = protoPart?.FindModulesImplementing<PersistentEngine>();

            if (persistentEngines is null || persistentEngines.Any() == false)
                return false;

            foreach (var engine in persistentEngines)
            {
                vesselData.PersistentEngines.Add(protoPartSnapshot.persistentId, engine);
            }

            ProtoPartModuleSnapshot protoPartModuleSnapshot = protoPartSnapshot.modules.FirstOrDefault(m => m.moduleName == nameof(PersistentEngine));

            if (protoPartModuleSnapshot == null)
                return false;

            // store data
            vesselData.ProtoParts.Add(protoPartSnapshot.persistentId, protoPart);
            vesselData.PersistentEnginePartIds.Add(protoPartSnapshot.persistentId);
            vesselData.ProtoPartModuleSnapshots.Add(protoPartSnapshot.persistentId, protoPartModuleSnapshot);

            // Load persistentThrust from ProtoPartModuleSnapshot
            foreach (var vesselDataPersistentEngine in persistentEngines)
            {
                // load persistentThrust
                protoPartModuleSnapshot.moduleValues.TryGetValue(nameof(vesselDataPersistentEngine.persistentThrust), ref vesselDataPersistentEngine.persistentThrust);
            }

            // check if there any active persistent engines that need to be processed
            if (persistentEngines.Any(m => m.persistentThrust > 0))
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
            // lookup data
            vesselData.ProtoPartModuleSnapshots.TryGetValue(protoPartSnapshot.persistentId, out ProtoPartModuleSnapshot protoPartModuleSnapshot);
            vesselData.PersistentEngines.TryGetValue(protoPartSnapshot.persistentId, out PersistentEngine persistentEngine);
            vesselData.ProtoParts.TryGetValue(protoPartSnapshot.persistentId, out Part protoPart);

            // execute persistent engine BackgroundUpdate
            PersistentEngine.BackgroundUpdate(
                vessel: vessel,
                part_snapshot: protoPartSnapshot,
                module_snapshot: protoPartModuleSnapshot,
                proto_part_module: persistentEngine,
                proto_part: protoPart,
                availableResources: availableResources,
                resourceChangeRequest: resourceChangeRequest,
                elapsed_s: TimeWarp.fixedDeltaTime);

            // ToDo: extract resource evenly
        }
    }
}
