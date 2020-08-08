using System;
using System.Collections.Generic;
using System.Linq;
using Debug = UnityEngine.Debug;

namespace PersistentThrust
{
    public class EngineData
    {
        public uint PersistentPartId { get; set; }
        public Part ProtoPart { get; set; }
        public ProtoPartSnapshot ProtoPartSnapshot { get; set; }
        public PersistentEngine PersistentEngine { get; set; }
        public ProtoPartModuleSnapshot ProtoPartModuleSnapshot { get; set; }
    }

    public class SolarPanelData
    {
        public uint PersistentPartId { get; set; }
        public Part ProtoPart { get; set; }
        public ProtoPartSnapshot ProtoPartSnapshot { get; set; }

        public List<ModuleDeployableSolarPanel> ModuleDeployableSolarPanels { get; set; }
        public List<ProtoPartModuleSnapshot> ProtoPartModuleSnapshots { get; set; }
    }
    

    public class VesselData
    {
        public Vessel Vessel { get; set; }

        public double TotalVesselMassInKg { get; set; }
        public double TotalVesselMassInTon { get; set; }
        public bool? HasAnyActivePersistentEngine { get; set; }

        public Dictionary<uint, EngineData> Engines { get; set; } = new Dictionary<uint, EngineData>();
        public Dictionary<uint, SolarPanelData> SolarPanels { get; set; } = new Dictionary<uint, SolarPanelData>();

        public Dictionary<string, double> AvailableResources { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> MaxAmountResources { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> ResourceChanges { get; set; } = new Dictionary<string, double>();

        public VesselData(Vessel vessel)
        {
            Vessel = vessel;
        }
    }


    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new[] {GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT, GameScenes.EDITOR})]
    public sealed class PersistentBackgroundProcessing : ScenarioModule
    {
        public static readonly Dictionary<Guid, VesselData> VesselDataDict = new Dictionary<Guid, VesselData>();

        public static int processCycleCounter = 0; 

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
            processCycleCounter++;
            if (processCycleCounter > 100)
                processCycleCounter = 0;

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
                else
                {
                    // ensure vessel is up to date
                    if (vesselData.Vessel != vessel)
                        vesselData.Vessel = vessel;

                    vesselData.ResourceChanges.Clear(); 
                }

                // update vessel data when loaded
                if (vessel.loaded)
                {
                    ProcessesLoadedVessel(vessel, vesselData);
                    continue;
                }

                // look for relevant modules in all vessel parts
                LoadUnloadedParts(vesselData);

                // determine available resources and total vessel mass
                UpdateUnloadedVesselData(vesselData);

                // extract power from Solar Panels
                ProcessUnloadedPersistentSolarPanels(vesselData);

                // update available resources
                UpdateAvailableResourcesWithResourceChanges(vesselData);

                // process persistent engines
                ProcessUnloadedPersistentEngines(vesselData);

                // update resources on vessel
                UpdatePersistentResources(vesselData);
            }
        }

        private static void ProcessUnloadedPersistentSolarPanels(VesselData vesselData)
        {
            if (DetectKerbalism.Found())
                return;

            if (!vesselData.SolarPanels.Any())
                return;

            foreach (var solarPanelData in vesselData.SolarPanels)
            {
                foreach (var solarPanel in solarPanelData.Value.ModuleDeployableSolarPanels)
                {
                    if (solarPanel.deployState != ModuleDeployablePart.DeployState.EXTENDED)
                        continue;

                    vesselData.ResourceChanges.TryGetValue(solarPanel.resourceName, out double resourceAmount);
                    vesselData.ResourceChanges[solarPanel.resourceName] = solarPanel.chargeRate + resourceAmount;
                 
                    // ToDo modify ChargeRate by distance from sun and orbital occlusion
                }
            }
        }

        private static void UpdateAvailableResourcesWithResourceChanges(VesselData vesselData)
        {
            foreach (var resourceChange in vesselData.ResourceChanges)
            {
                vesselData.AvailableResources.TryGetValue(resourceChange.Key, out double availableAmount);
                vesselData.AvailableResources[resourceChange.Key] = resourceChange.Value + availableAmount;
            }
        }

        private static void ProcessUnloadedPersistentEngines(VesselData vesselData)
        {
            if (DetectKerbalism.Found())
                return;

            if (!vesselData.Engines.Any())
                return;

            foreach (var engine in vesselData.Engines)
            {
                if (engine.Value.ProtoPartSnapshot == null)
                {
                    vesselData.HasAnyActivePersistentEngine = null;
                    Debug.LogWarning("[PersistentThrust]: Fail to find protoPartSnapshot " + engine.Value.PersistentPartId);
                    continue;
                }

                var resourceChangeRequest = new List<KeyValuePair<string, double>>();

                ProcessUnloadedPersistentEngine(engine.Value.ProtoPartSnapshot, vesselData, resourceChangeRequest);

                foreach (var keyValuePair in resourceChangeRequest)
                {
                    vesselData.ResourceChanges.TryGetValue(keyValuePair.Key, out double resourceAmount);
                    vesselData.ResourceChanges[keyValuePair.Key] = resourceAmount + keyValuePair.Value;
                }
            }
        }

        private static void UpdatePersistentResources(VesselData vesselData)
        {
            foreach (var resourceRequest in vesselData.ResourceChanges)
            {
                double fixedChange = resourceRequest.Value * TimeWarp.fixedDeltaTime;

                vesselData.AvailableResources.TryGetValue(resourceRequest.Key, out double available);
                vesselData.MaxAmountResources.TryGetValue(resourceRequest.Key, out double totalAmount);

                foreach (ProtoPartSnapshot protoPartSnapshot in vesselData.Vessel.protoVessel.protoPartSnapshots)
                {
                    foreach (var protoPartResourceSnapshot in protoPartSnapshot.resources)
                    {
                        if (protoPartResourceSnapshot.resourceName != resourceRequest.Key) continue;

                        // skip resources that are locked
                        if (protoPartResourceSnapshot.flowState == false)
                            continue;

                        if (fixedChange > 0)
                        {
                            var fraction = available < float.Epsilon ? 1 : protoPartResourceSnapshot.amount / available;

                            if (protoPartResourceSnapshot.amount > float.Epsilon)
                                protoPartResourceSnapshot.amount = Math.Max(0, protoPartResourceSnapshot.amount + fixedChange * fraction);
                            else
                                protoPartResourceSnapshot.amount = 0;
                        }
                        else
                        {
                            var partAvailableSpace = protoPartResourceSnapshot.maxAmount - protoPartResourceSnapshot.amount;

                            var fraction = partAvailableSpace > float.Epsilon ? partAvailableSpace / (totalAmount - available) : 1;

                            protoPartResourceSnapshot.amount = Math.Max(protoPartResourceSnapshot.maxAmount, protoPartResourceSnapshot.amount + fixedChange * fraction);
                        }
                    }
                }
            }
        }

        private static void UpdateUnloadedVesselData(VesselData vesselData)
        {
            vesselData.AvailableResources.Clear();
            vesselData.MaxAmountResources.Clear();
            vesselData.TotalVesselMassInTon = 0;

            foreach (ProtoPartSnapshot protoPartSnapshot in vesselData.Vessel.protoVessel.protoPartSnapshots)
            {
                vesselData.Engines.TryGetValue(protoPartSnapshot.persistentId, out EngineData engineData);

                // load engineData when not found
                if (engineData != null)
                {
                    if (engineData.ProtoPartSnapshot != protoPartSnapshot)
                        engineData.ProtoPartSnapshot = protoPartSnapshot;
                }
                else if (protoPartSnapshot.persistentId % 100 == processCycleCounter)
                {
                    LoadPersistentEngine(protoPartSnapshot, vesselData);
                }

                vesselData.SolarPanels.TryGetValue(protoPartSnapshot.persistentId, out SolarPanelData solarPanelData);
                // load solarData when not found
                if (solarPanelData != null)
                {
                    if (solarPanelData.ProtoPartSnapshot != protoPartSnapshot)
                        solarPanelData.ProtoPartSnapshot = protoPartSnapshot;
                }
                else if (protoPartSnapshot.persistentId % 100 == processCycleCounter)
                {
                    LoadModuleDeployableSolarPanel(protoPartSnapshot, vesselData);
                }

                vesselData.TotalVesselMassInTon += protoPartSnapshot.mass;
                foreach (ProtoPartResourceSnapshot protoPartResourceSnapshot in protoPartSnapshot.resources)
                {
                    vesselData.TotalVesselMassInTon += protoPartResourceSnapshot.amount * protoPartResourceSnapshot.definition.density;

                    vesselData.AvailableResources.TryGetValue(protoPartResourceSnapshot.resourceName, out double amount);
                    vesselData.MaxAmountResources.TryGetValue(protoPartResourceSnapshot.resourceName, out double maxAmount);

                    vesselData.AvailableResources[protoPartResourceSnapshot.resourceName] = amount + protoPartResourceSnapshot.amount;
                    vesselData.MaxAmountResources[protoPartResourceSnapshot.resourceName] = maxAmount + protoPartResourceSnapshot.maxAmount;
                }
            }
            vesselData.TotalVesselMassInKg = vesselData.TotalVesselMassInTon * 1000;
        }

        private static void LoadUnloadedParts(VesselData vesselData)
        {
            // check if initialized
            if (vesselData.HasAnyActivePersistentEngine.HasValue)
                return;

            // initially assume no active persistent engine present   
            vesselData.HasAnyActivePersistentEngine = false;

            foreach (ProtoPartSnapshot protoPartSnapshot in vesselData.Vessel.protoVessel.protoPartSnapshots)
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

        private static void LoadUnloadedPart(ProtoPartSnapshot protoPartSnapshot, VesselData vesselData)
        {
            AvailablePart availablePart = PartLoader.getPartInfoByName(protoPartSnapshot.partName);

            Part protoPart = availablePart?.partPrefab;

            if (protoPart == null)
                return;

            LoadModuleDeployableSolarPanel(protoPartSnapshot, vesselData, protoPart);

            LoadPersistentEngine(protoPartSnapshot, vesselData, protoPart);
        }

        private static SolarPanelData LoadModuleDeployableSolarPanel(ProtoPartSnapshot protoPartSnapshot, VesselData vesselData, Part protoPart = null)
        {
            if (protoPart == null)
                protoPart = PartLoader.getPartInfoByName(protoPartSnapshot.partName)?.partPrefab;

            var moduleDeployableSolarPanels = protoPart?.FindModulesImplementing<ModuleDeployableSolarPanel>();

            if (moduleDeployableSolarPanels is null || moduleDeployableSolarPanels.Any() == false)
                return null;

            List<ProtoPartModuleSnapshot> protoPartModuleSnapshots = protoPartSnapshot.modules.Where(m => m.moduleName == nameof(ModuleDeployableSolarPanel)).ToList();

            for (var i = 0; i  < moduleDeployableSolarPanels.Count; i++)
            {
                var moduleDeployableSolarPanel = moduleDeployableSolarPanels[i];
                var protoPartModuleSnapshot = protoPartModuleSnapshots[i];

                // Load ModuleDeployableSolarPanels from ProtoPartModuleSnapshots
                moduleDeployableSolarPanel.deployState = (ModuleDeployablePart.DeployState)Enum.Parse(typeof(ModuleDeployablePart.DeployState), protoPartModuleSnapshot.moduleValues.GetValue(nameof(moduleDeployableSolarPanel.deployState)));
            }

            var solarPanelData = new SolarPanelData
            {
                PersistentPartId = protoPartSnapshot.persistentId,
                ProtoPart = protoPart,
                ProtoPartModuleSnapshots = moduleDeployableSolarPanels.Select(m => m.snapshot).ToList(),
                ModuleDeployableSolarPanels = moduleDeployableSolarPanels
            };

            // store data
            vesselData.SolarPanels.Add(protoPartSnapshot.persistentId, solarPanelData);

            return solarPanelData;
        }

        private static EngineData LoadPersistentEngine(ProtoPartSnapshot protoPartSnapshot, VesselData vesselData, Part protoPart = null)
        {
            if (protoPart == null)
                protoPart = PartLoader.getPartInfoByName(protoPartSnapshot.partName)?.partPrefab;

            var persistentEngine = protoPart?.FindModuleImplementing<PersistentEngine>();

            if (persistentEngine is null)
                return null;

            ProtoPartModuleSnapshot protoPartModuleSnapshot = protoPartSnapshot.FindModule(nameof(PersistentEngine));

            if (protoPartModuleSnapshot == null)
                return null;

            // Load persistentThrust from ProtoPartModuleSnapshots
            protoPartModuleSnapshot.moduleValues.TryGetValue(nameof(persistentEngine.persistentThrust), ref persistentEngine.persistentThrust);

            var engineData = new EngineData
            {
                PersistentPartId = protoPartSnapshot.persistentId,
                ProtoPart = protoPart,
                ProtoPartModuleSnapshot = protoPartModuleSnapshot,
                PersistentEngine = persistentEngine
            };

            // store data
            vesselData.Engines.Add(protoPartSnapshot.persistentId, engineData);

            // check if there any active persistent engines that need to be processed
            if (persistentEngine.persistentThrust > 0)
                vesselData.HasAnyActivePersistentEngine = true;
            else
                return null;

            return engineData;
        }

        private static void ProcessUnloadedPersistentEngine(
            ProtoPartSnapshot protoPartSnapshot, 
            VesselData vesselData, 
            List<KeyValuePair<string, double>> resourceChangeRequest)
        {
            // lookup engine data
            vesselData.Engines.TryGetValue(protoPartSnapshot.persistentId, out EngineData engineData);

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
    }
}
