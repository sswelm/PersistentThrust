using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentThrust
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new[] {GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT, GameScenes.EDITOR})]
    public sealed class PersistentScenarioModule : ScenarioModule
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

                // ignore irrelevant vessel types
                if (vessel.vesselType == VesselType.Debris
                    || vessel.vesselType == VesselType.Flag
                    || vessel.vesselType == VesselType.SpaceObject
                    || vessel.vesselType == VesselType.DeployedSciencePart
                    || vessel.vesselType == VesselType.DeployedScienceController
                )
                    continue;

                // lookup cashed vessel data
                if (!VesselDataDict.TryGetValue(vessel.id, out VesselData vesselData))
                {
                    vesselData = new VesselData(vessel);
                    VesselDataDict.Add(vessel.id, vesselData);
                }
                else
                {
                    // reset change
                    foreach (var vesselDataResourceChange in vesselData.ResourceChanges)
                    {
                        vesselDataResourceChange.Value.Change = 0;
                    }
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
                vesselData.UpdateUnloadedVesselData();

                // extract resources from Solar Panels
                ProcessUnloadedSolarPanels(vesselData);

                // extract resources from generators (RTG etc)
                ProcessUnloadedModuleGenerators(vesselData);

                // extract resources from generators (Fuel Cells etc)
                ProcessUnloadedResourceConverters(vesselData);

                // process persistent engines
                EngineBackgroundProcessing.ProcessUnloadedPersistentEngines(vesselData);

                // update resources on vessel
                UpdatePersistentResources(vesselData);
            }
        }

        private static void ProcessUnloadedModuleGenerators(VesselData vesselData)
        {
            if (DetectKerbalism.Found())
                return;

            foreach (KeyValuePair<uint, ModuleGeneratorData> vesselDataGenerator in vesselData.Generators)
            {
                for (var i = 0; i < vesselDataGenerator.Value.ModuleGenerators.Count; i++)
                {
                    ModuleGenerator moduleGenerator = vesselDataGenerator.Value.ModuleGenerators[i];
                    ProtoPartModuleSnapshot protoPartModuleSnapshot = vesselDataGenerator.Value.ProtoPartModuleSnapshots[i];

                    // read persistent settings
                    moduleGenerator.generatorIsActive = bool.Parse(protoPartModuleSnapshot.moduleValues.GetValue(nameof(moduleGenerator.generatorIsActive)));
                    if (moduleGenerator.generatorIsActive == false)
                        continue;

                    moduleGenerator.throttle = float.Parse(protoPartModuleSnapshot.moduleValues.GetValue(nameof(moduleGenerator.throttle)));
                    if (moduleGenerator.throttle <= 0)
                        continue;

                    // determine found ratio
                    double finalFoundRatio = 1;
                    foreach (ModuleResource moduleResource in moduleGenerator.resHandler.inputResources)
                    {
                        vesselData.AvailableResources.TryGetValue(moduleResource.name, out double availableAmount);

                        double requestedRate = moduleGenerator.throttle * moduleResource.rate * TimeWarp.fixedDeltaTime;
                        double foundRatio = requestedRate > 0 ? Math.Min(1, availableAmount / requestedRate) : 1;

                        if (foundRatio < finalFoundRatio)
                            finalFoundRatio = foundRatio;
                    }

                    if (finalFoundRatio <= 0)
                        continue;

                    // extract resource from available resources
                    foreach (ModuleResource inputResource in moduleGenerator.resHandler.inputResources)
                    {
                        double resourceChange = -inputResource.rate * moduleGenerator.throttle * finalFoundRatio;

                        vesselData.UpdateAvailableResource(inputResource.name, resourceChange);
                        vesselData.ResourceChange(inputResource.name, resourceChange);
                    }

                    // generate resources
                    foreach (ModuleResource outputResource in moduleGenerator.resHandler.outputResources)
                    {
                        double resourceChange = outputResource.rate * moduleGenerator.throttle * finalFoundRatio;

                        vesselData.UpdateAvailableResource(outputResource.name, resourceChange);
                        vesselData.ResourceChange(outputResource.name, resourceChange);
                    }
                }
            }
        }

        private static void ProcessUnloadedResourceConverters(VesselData vesselData)
        {
            if (DetectKerbalism.Found())
                return;

            foreach (KeyValuePair<uint, ModuleResourceConverterData> vesselDataGenerator in vesselData.ResourceConverters)
            {
                for (var i = 0; i < vesselDataGenerator.Value.ModuleResourceConverters.Count; i++)
                {
                    ModuleResourceConverter resourceConverter = vesselDataGenerator.Value.ModuleResourceConverters[i];
                    ProtoPartModuleSnapshot protoPartModuleSnapshot = vesselDataGenerator.Value.ProtoPartModuleSnapshots[i];

                    // read persistent settings
                    resourceConverter.IsActivated = bool.Parse(protoPartModuleSnapshot.moduleValues.GetValue(nameof(resourceConverter.IsActivated)));
                    if (resourceConverter.IsActivated == false)
                        continue;

                    resourceConverter.EfficiencyBonus = float.Parse(protoPartModuleSnapshot.moduleValues.GetValue(nameof(resourceConverter.EfficiencyBonus)));
                    if (resourceConverter.EfficiencyBonus <= 0)
                        continue;

                    // determine processRatio ratio
                    double processRatio = 1;

                    // check if we meet the requirements
                    foreach (var resourceRatio in resourceConverter.Recipe.Requirements)
                    {
                        vesselData.AvailableResources.TryGetValue(resourceRatio.ResourceName, out double availableAmount);

                        if (availableAmount < resourceRatio.Ratio)
                        {
                            processRatio = 0;
                            break;
                        }
                    }

                    if (processRatio <= 0)
                        continue;

                    // check if we meet the output storage
                    foreach (ResourceRatio resourceRatio in resourceConverter.Recipe.Outputs)
                    {
                        if (resourceRatio.DumpExcess)
                            continue;

                        vesselData.AvailableStorage.TryGetValue(resourceRatio.ResourceName, out double availableStorage);

                        double requestedRate = resourceConverter.EfficiencyBonus * resourceRatio.Ratio * TimeWarp.fixedDeltaTime;
                        double spaceRatio = resourceRatio.Ratio > 0 ? Math.Min(1, availableStorage / requestedRate): 1;

                        if (spaceRatio < processRatio)
                            processRatio = spaceRatio;
                    }

                    if (processRatio <= 0)
                        continue;

                    // check if we meet the input
                    foreach (ResourceRatio resourceRatio in resourceConverter.Recipe.Inputs)
                    {
                        vesselData.AvailableResources.TryGetValue(resourceRatio.ResourceName, out double availableAmount);

                        double requestedRate = resourceConverter.EfficiencyBonus * resourceRatio.Ratio * TimeWarp.fixedDeltaTime;
                        double foundRatio = requestedRate > 0 ? Math.Min(1, availableAmount / requestedRate) : 1;

                        if (foundRatio < processRatio)
                            processRatio = foundRatio;
                    }

                    if (processRatio <= 0)
                        continue;

                    // extract resource from available resources
                    foreach (ResourceRatio inputResource in resourceConverter.Recipe.Inputs)
                    {
                        double resourceChange = -inputResource.Ratio * resourceConverter.EfficiencyBonus * processRatio;

                        vesselData.UpdateAvailableResource(inputResource.ResourceName, resourceChange);
                        vesselData.ResourceChange(inputResource.ResourceName, resourceChange);
                    }

                    // generate resources
                    foreach (ResourceRatio outputResource in resourceConverter.Recipe.Outputs)
                    {
                        double resourceChange = outputResource.Ratio * resourceConverter.EfficiencyBonus * processRatio;

                        vesselData.UpdateAvailableResource(outputResource.ResourceName, resourceChange);
                        vesselData.ResourceChange(outputResource.ResourceName, resourceChange);
                    }
                }
            }
        }

        private static void ProcessUnloadedSolarPanels(VesselData vesselData)
        {
            if (DetectKerbalism.Found())
                return;

            if (!vesselData.SolarPanels.Any())
                return;

            foreach (StarLight starlight in KopernicusHelper.Stars)
            {
                double starlightRelativeLuminosity = KopernicusHelper.GetSolarDistanceMultiplier(
                    vesselPosition: vesselData.Position, 
                    star: starlight.star, 
                    astronomicalUnit: KopernicusHelper.AstronomicalUnit);

                starlightRelativeLuminosity *= starlight.relativeLuminosity;

                // ignore stars that are too far away to give any meaningful energy
                if (starlightRelativeLuminosity < 0.001)
                    continue;

                // ignore if there is no line of sight between the star and the vessel
                if (!KopernicusHelper.LineOfSightToSun(vesselData.Position, starlight.star))
                    continue;

                foreach (var solarPanelData in vesselData.SolarPanels)
                {
                    foreach (ModuleDeployableSolarPanel solarPanel in solarPanelData.Value.ModuleDeployableSolarPanels)
                    {
                        if (solarPanel.chargeRate <= 0)
                            continue;

                        if (solarPanel.deployState != ModuleDeployablePart.DeployState.EXTENDED)
                            continue;

                        double trackingModifier = solarPanel.isTracking ? 1 : 0.25;
                        double powerChange = trackingModifier * solarPanel.chargeRate * starlightRelativeLuminosity * solarPanel.efficiencyMult;

                        vesselData.UpdateAvailableResource(solarPanel.resourceName, powerChange);
                        vesselData.ResourceChange(solarPanel.resourceName, powerChange);
                    }
                }
            }
        }

        private static void UpdatePersistentResources(VesselData vesselData)
        {
            var relevantResourceChanges = vesselData.ResourceChanges.Where(m => m.Value.Change != 0);

            foreach (var resourceRequest in relevantResourceChanges)
            {
                double fixedChange = resourceRequest.Value.Change * TimeWarp.fixedDeltaTime;

                vesselData.AvailableResources.TryGetValue(resourceRequest.Key, out double available);
                vesselData.MaxAmountResources.TryGetValue(resourceRequest.Key, out double totalAmount);

                foreach (ProtoPartSnapshot protoPartSnapshot in vesselData.Vessel.protoVessel.protoPartSnapshots)
                {
                    foreach (ProtoPartResourceSnapshot protoPartResourceSnapshot in protoPartSnapshot.resources)
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

                            protoPartResourceSnapshot.amount = Math.Min(protoPartResourceSnapshot.maxAmount, protoPartResourceSnapshot.amount + fixedChange * fraction);
                        }
                    }
                }
            }
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
                    engineData.PersistentEngine.persistentThrust = persistentEngine.persistentThrust;
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

            LoadModuleGenerator(protoPartSnapshot, vesselData, protoPart);

            LoadModuleResourceConverters(protoPartSnapshot, vesselData, protoPart);

            EngineBackgroundProcessing.LoadPersistentEngine(protoPartSnapshot, vesselData, protoPart);
        }

        private static SolarPanelData LoadModuleDeployableSolarPanel(ProtoPartSnapshot protoPartSnapshot, VesselData vesselData, Part protoPart = null)
        {
            if (protoPart == null)
                protoPart = PartLoader.getPartInfoByName(protoPartSnapshot.partName)?.partPrefab;

            var moduleDeployableSolarPanels = protoPart?.FindModulesImplementing<ModuleDeployableSolarPanel>();

            if (moduleDeployableSolarPanels is null || moduleDeployableSolarPanels.Any() == false)
                return null;

            List<ProtoPartModuleSnapshot> protoPartModuleSnapshots = protoPartSnapshot.modules.Where(m => m.moduleName == nameof(ModuleDeployableSolarPanel)).ToList();

            var solarPanelData = new SolarPanelData
            {
                ProtoPart = protoPart, 
                ProtoPartSnapshot = protoPartSnapshot,
                PersistentPartId = protoPartSnapshot.persistentId,
                ProtoPartModuleSnapshots = protoPartModuleSnapshots,
                ModuleDeployableSolarPanels = moduleDeployableSolarPanels
            };

            for (int i = 0; i  < moduleDeployableSolarPanels.Count; i++)
            {
                var moduleDeployableSolarPanel = moduleDeployableSolarPanels[i];
                var protoPartModuleSnapshot = protoPartModuleSnapshots[i];

                moduleDeployableSolarPanel.deployState = (ModuleDeployablePart.DeployState)Enum.Parse(typeof(ModuleDeployablePart.DeployState), protoPartModuleSnapshot.moduleValues.GetValue(nameof(moduleDeployableSolarPanel.deployState)));
            }

            // store data
            vesselData.SolarPanels.Add(protoPartSnapshot.persistentId, solarPanelData);

            return solarPanelData;
        }

        private static ModuleGeneratorData LoadModuleGenerator(ProtoPartSnapshot protoPartSnapshot, VesselData vesselData, Part protoPart = null)
        {
            if (protoPart == null)
                protoPart = PartLoader.getPartInfoByName(protoPartSnapshot.partName)?.partPrefab;

            var moduleGenerators = protoPart?.FindModulesImplementing<ModuleGenerator>();

            if (moduleGenerators is null || moduleGenerators.Any() == false)
                return null;

            List<ProtoPartModuleSnapshot> protoPartModuleSnapshots = protoPartSnapshot.modules.Where(m => m.moduleName == nameof(ModuleGenerator)).ToList();

            var moduleGeneratorData = new ModuleGeneratorData
            {
                ProtoPart = protoPart, 
                ProtoPartSnapshot = protoPartSnapshot,
                PersistentPartId = protoPartSnapshot.persistentId,
                ProtoPartModuleSnapshots = protoPartModuleSnapshots,
                ModuleGenerators = moduleGenerators
            };

            // readout persistent data
            for (int i = 0; i < moduleGenerators.Count; i++)
            {
                ModuleGenerator moduleGenerator = moduleGenerators[i];
                ProtoPartModuleSnapshot protoPartModuleSnapshot = protoPartModuleSnapshots[i];

                moduleGenerator.generatorIsActive = bool.Parse(protoPartModuleSnapshot.moduleValues.GetValue(nameof(moduleGenerator.generatorIsActive)));
                moduleGenerator.throttle = float.Parse(protoPartModuleSnapshot.moduleValues.GetValue(nameof(moduleGenerator.throttle)));
            }

            vesselData.Generators.Add(protoPartSnapshot.persistentId, moduleGeneratorData);

            return moduleGeneratorData;
        }

        private static ModuleResourceConverterData LoadModuleResourceConverters(ProtoPartSnapshot protoPartSnapshot, VesselData vesselData, Part protoPart = null)
        {
            if (protoPart == null)
                protoPart = PartLoader.getPartInfoByName(protoPartSnapshot.partName)?.partPrefab;

            var moduleResourceConverter = protoPart?.FindModulesImplementing<ModuleResourceConverter>();

            if (moduleResourceConverter is null || moduleResourceConverter.Any() == false)
                return null;

            List<ProtoPartModuleSnapshot> protoPartModuleSnapshots = protoPartSnapshot.modules.Where(m => m.moduleName == nameof(ModuleResourceConverter)).ToList();

            var resourceConverterData = new ModuleResourceConverterData
            {
                ProtoPart = protoPart,
                ProtoPartSnapshot = protoPartSnapshot,
                PersistentPartId = protoPartSnapshot.persistentId,
                ProtoPartModuleSnapshots = protoPartModuleSnapshots,
                ModuleResourceConverters = moduleResourceConverter
            };

            // readout persistent data
            for (int i = 0; i < moduleResourceConverter.Count; i++)
            {
                ModuleResourceConverter resourceConverter = moduleResourceConverter[i];
                ProtoPartModuleSnapshot protoPartModuleSnapshot = protoPartModuleSnapshots[i];

                resourceConverter.IsActivated = bool.Parse(protoPartModuleSnapshot.moduleValues.GetValue(nameof(resourceConverter.IsActivated)));
                resourceConverter.EfficiencyBonus = float.Parse(protoPartModuleSnapshot.moduleValues.GetValue(nameof(resourceConverter.EfficiencyBonus)));
            }

            vesselData.ResourceConverters.Add(protoPartSnapshot.persistentId, resourceConverterData);

            return resourceConverterData;
        }

    }
}
