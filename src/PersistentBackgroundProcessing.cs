using System;
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

    public class SolarPanelData
    {
        public uint PersistentPartId { get; set; }
        public Part ProtoPart { get; set; }
        public ModuleDeployableSolarPanel ModuleDeployableSolarPanel { get; set; }
        public ProtoPartModuleSnapshot ProtoPartModuleSnapshot { get; set; }
    }
    

    public class VesselData
    {
        public Vessel Vessel { get; set; }
        
        public double TotalVesselMassInKg { get; set; }
        public double TotalVesselMassInTon { get; set; }
        public bool? HasAnyActivePersistentEngine { get; set; }

        public Dictionary<uint, EngineData> Engines { get; set; } = new Dictionary<uint, EngineData>();
        public Dictionary<uint, SolarPanelData> SolarPanels { get; set; } = new Dictionary<uint, SolarPanelData>();

        public Dictionary<string, double> AvailableResources { get; set; }  = new Dictionary<string, double>();
        public Dictionary<string, double> MaxAmountResources { get; set; } = new Dictionary<string, double>();

        public VesselData(Vessel vessel)
        {
            Vessel = vessel;
        }
    }


    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new[] {GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT, GameScenes.EDITOR})]
    public sealed class PersistentBackgroundProcessing : ScenarioModule
    {
        public static readonly Dictionary<Guid, VesselData> VesselDataDict = new Dictionary<Guid, VesselData>();

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            VesselDataDict.Clear();
        }

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
                if (!VesselDataDict.TryGetValue(vessel.id, out VesselData vesselData))
                {
                    vesselData = new VesselData(vessel);
                    VesselDataDict.Add(vessel.id, vesselData);
                }

                // update vessel data when loaded
                if (vessel.loaded)
                {
                    ProcessesLoadedVessel(vessel, vesselData);
                    continue;
                }

                // update when active engine or uninitialized
                if (vesselData.HasAnyActivePersistentEngine.HasValue == false || vesselData.Engines.Any())
                {
                    // determine available resources and total vessel mass
                    vesselData.TotalVesselMassInTon = GetResourcesAndTotalVesselMass(vessel, vesselData.AvailableResources, vesselData.MaxAmountResources);
                    vesselData.TotalVesselMassInKg = vesselData.TotalVesselMassInTon * 1000;
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

                ProcessUnloadedPersistentEngine(protoPartSnapshot, vessel, vesselData, resourceChangeRequest);

                foreach (var keyValuePair in resourceChangeRequest)
                {
                    totalResourceChangeRequests.TryGetValue(keyValuePair.Key, out double requestedAmount);
                    totalResourceChangeRequests[keyValuePair.Key] = requestedAmount - keyValuePair.Value;
                }
            }

            UpdateResources(vessel, totalResourceChangeRequests, vesselData.AvailableResources);
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

        private static double GetResourcesAndTotalVesselMass(Vessel vessel, Dictionary<string, double> availableResources, Dictionary<string, double> maxAmountResources)
        {
            availableResources.Clear();
            maxAmountResources.Clear();
            double totalVesselMass = 0;

            foreach (ProtoPartSnapshot protoPartSnapshot in vessel.protoVessel.protoPartSnapshots)
            {
                totalVesselMass += protoPartSnapshot.mass;
                foreach (ProtoPartResourceSnapshot protoPartResourceSnapshot in protoPartSnapshot.resources)
                {
                    totalVesselMass += protoPartResourceSnapshot.amount * protoPartResourceSnapshot.definition.density;

                    availableResources.TryGetValue(protoPartResourceSnapshot.resourceName, out double amount);
                    maxAmountResources.TryGetValue(protoPartResourceSnapshot.resourceName, out double maxAmount);

                    availableResources[protoPartResourceSnapshot.resourceName] = amount + protoPartResourceSnapshot.amount;
                    maxAmountResources[protoPartResourceSnapshot.resourceName] = maxAmount + protoPartResourceSnapshot.maxAmount;
                }
            }

            return totalVesselMass;
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

            if (protoPart == null)
                return false;

            LoadModuleDeployableSolarPanel(protoPartSnapshot, vesselData, protoPart);

            return LoadPersistentEngine(protoPartSnapshot, vesselData, protoPart);
        }

        private static void LoadModuleDeployableSolarPanel(ProtoPartSnapshot protoPartSnapshot, VesselData vesselData, Part protoPart)
        {
            var moduleDeployableSolarPanel = protoPart?.FindModuleImplementing<ModuleDeployableSolarPanel>();

            if (moduleDeployableSolarPanel is null)
                return;

            ProtoPartModuleSnapshot protoPartModuleSnapshot = protoPartSnapshot.FindModule(nameof(ModuleDeployableSolarPanel));

            if (protoPartModuleSnapshot == null)
                return;

            // Load ModuleDeployableSolarPanel from ProtoPartModuleSnapshot
            moduleDeployableSolarPanel.deployState = (ModuleDeployablePart.DeployState)Enum.Parse(typeof(ModuleDeployablePart.DeployState), protoPartModuleSnapshot.moduleValues.GetValue(nameof(moduleDeployableSolarPanel.deployState)));

            // store data
            vesselData.SolarPanels.Add(protoPartSnapshot.persistentId, new SolarPanelData
            {
                PersistentPartId = protoPartSnapshot.persistentId,
                ProtoPart = protoPart,
                ProtoPartModuleSnapshot = protoPartModuleSnapshot,
                ModuleDeployableSolarPanel = moduleDeployableSolarPanel
            });
        }

        private static bool LoadPersistentEngine(ProtoPartSnapshot protoPartSnapshot, VesselData vesselData, Part protoPart)
        {
            var persistentEngine = protoPart?.FindModuleImplementing<PersistentEngine>();

            if (persistentEngine is null)
                return false;

            ProtoPartModuleSnapshot protoPartModuleSnapshot = protoPartSnapshot.FindModule(nameof(PersistentEngine));

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
                availableResources: vesselData.AvailableResources, 
                resourceChangeRequest: resourceChangeRequest, 
                elapsed_s: TimeWarp.fixedDeltaTime);
        }
    }
}
