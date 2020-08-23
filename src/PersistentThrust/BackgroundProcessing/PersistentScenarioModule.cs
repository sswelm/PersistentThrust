using PersistentThrust.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentThrust.BackgroundProcessing
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new[] { GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT, GameScenes.EDITOR })]
    public sealed class PersistentScenarioModule : ScenarioModule
    {
        public static readonly Dictionary<Guid, VesselData> VesselDataDict = new Dictionary<Guid, VesselData>();
        
        public static string ScenarioName { get; set; }

        public static double UniversalTime { get; set; }


        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (ScenarioName != HighLogic.CurrentGame.Title)
                VesselDataDict.Clear();
            ScenarioName = HighLogic.CurrentGame.Title;
        }

        void FixedUpdate()
        {
            // store info for oldest unloaded vessel
            double last_time = 0.0;
            VesselData last_vd = null;

            UniversalTime = Planetarium.GetUniversalTime();

            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                // don't process a vessel until unloaded, but add it to vesselDataDict
                if (vessel.loaded)
                {
                    if (!VesselDataDict.ContainsKey(vessel.id))
                        VesselDataDict.Add(vessel.id, new VesselData(vessel));

                    continue;
                }

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
                    vesselData.Vessel = vessel;

                vesselData.Time += TimeWarp.fixedDeltaTime;

                // maintain oldest entry
                if (vesselData.Time > last_time)
                {
                    last_time = vesselData.Time;
                    last_vd = vesselData;
                }

                // determine available resources and total vessel mass
                UnityEngine.Profiling.Profiler.BeginSample("PersistentThrust.PersistentScenarioModule.FixedUpdate.UpdateUnloadedVesselData");
                vesselData.UpdateUnloadedVesselData();
                UnityEngine.Profiling.Profiler.EndSample(); 
            }

            // at most one vessel gets background processing per physics tick :
            // if there is a vessel that is not the currently loaded vessel, then
            // we will update the vessel whose most recent background update is the oldest
            if (last_vd != null)
            {
                last_vd.DeltaTime = last_time;

                // look for relevant modules in all vessel parts
                UnityEngine.Profiling.Profiler.BeginSample("PersistentThrust.PersistentScenarioModule.FixedUpdate.LoadUnloadedParts");
                LoadUnloadedParts(last_vd);
                UnityEngine.Profiling.Profiler.EndSample();

                // extract resources from Solar Panels
                UnityEngine.Profiling.Profiler.BeginSample("PersistentThrust.PersistentScenarioModule.FixedUpdate.ProcessUnloadedSolarPanels");
                ProcessUnloadedSolarPanels(last_vd);
                UnityEngine.Profiling.Profiler.EndSample();

                // extract resources from generators (RTG etc)
                UnityEngine.Profiling.Profiler.BeginSample("PersistentThrust.PersistentScenarioModule.FixedUpdate.ProcessUnloadedModuleGenerators");
                ProcessUnloadedModuleGenerators(last_vd, last_time);
                UnityEngine.Profiling.Profiler.EndSample();

                // extract resources from generators (Fuel Cells etc)
                UnityEngine.Profiling.Profiler.BeginSample("PersistentThrust.PersistentScenarioModule.FixedUpdate.ProcessUnloadedResourceConverters");
                ProcessUnloadedResourceConverters(last_vd, last_time);
                UnityEngine.Profiling.Profiler.EndSample();

                // process persistent engines
                UnityEngine.Profiling.Profiler.BeginSample("PersistentThrust.PersistentScenarioModule.FixedUpdate.ProcessUnloadedPersistentEngines");
                EngineBackgroundProcessing.ProcessUnloadedPersistentEngines(last_vd, last_time);
                UnityEngine.Profiling.Profiler.EndSample();

                // update resources on vessel
                UnityEngine.Profiling.Profiler.BeginSample("PersistentThrust.PersistentScenarioModule.FixedUpdate.UpdatePersistentResources");
                UpdatePersistentResources(last_vd, last_time);
                UnityEngine.Profiling.Profiler.EndSample();

                last_vd.Time = 0.0;
            }
        }

        private static void ProcessUnloadedModuleGenerators(VesselData vesselData, double elapsedTime)
        {
            if (DetectKerbalism.Found())
                return;

            foreach (KeyValuePair<uint, ModuleGeneratorData> keyValuePair in vesselData.Generators)
            {
                ModuleGeneratorData generatorData = keyValuePair.Value;
                generatorData.ProtoPartSnapshot = vesselData.Vessel.protoVessel.protoPartSnapshots[generatorData.PartIndex];

                for (var i = 0; i < generatorData.ModuleGenerators.Count; i++)
                {
                    ModuleGenerator moduleGenerator = generatorData.ModuleGenerators[i];

                    // load protoPartModuleSnapshot
                    var moduleIndex = generatorData.ProtoPartModuleSnapshotIndexes[i];
                    ProtoPartModuleSnapshot protoPartModuleSnapshot = generatorData.ProtoPartSnapshot.modules[moduleIndex];

                    // refresh persistent setting generatorIsActive
                    moduleGenerator.generatorIsActive = bool.Parse(protoPartModuleSnapshot.moduleValues.GetValue(nameof(moduleGenerator.generatorIsActive)));
                    if (moduleGenerator.generatorIsActive == false)
                        continue;

                    // refresh persistent setting throttle
                    moduleGenerator.throttle = float.Parse(protoPartModuleSnapshot.moduleValues.GetValue(nameof(moduleGenerator.throttle)));
                    if (moduleGenerator.throttle <= 0)
                        continue;

                    // determine found ratio
                    double finalFoundRatio = 1;
                    foreach (ModuleResource moduleResource in moduleGenerator.resHandler.inputResources)
                    {
                        vesselData.AvailableResources.TryGetValue(moduleResource.name, out double availableAmount);

                        double requestedRate = moduleGenerator.throttle * moduleResource.rate * elapsedTime * generatorData.InputMultiplier;
                        double foundRatio = requestedRate > 0 ? Math.Min(1, availableAmount / requestedRate) : 1;

                        if (foundRatio < finalFoundRatio)
                            finalFoundRatio = foundRatio;
                    }

                    if (finalFoundRatio <= 0)
                        continue;

                    // extract resource from available resources
                    foreach (ModuleResource inputResource in moduleGenerator.resHandler.inputResources)
                    {
                        double resourceChange = -inputResource.rate * moduleGenerator.throttle * finalFoundRatio * generatorData.InputMultiplier;

                        vesselData.UpdateAvailableResource(inputResource.name, resourceChange);
                        vesselData.ResourceChange(inputResource.name, resourceChange);
                    }

                    // generate resources
                    foreach (ModuleResource outputResource in moduleGenerator.resHandler.outputResources)
                    {
                        double resourceChange = outputResource.rate * moduleGenerator.throttle * finalFoundRatio * generatorData.OutputMultiplier;

                        vesselData.UpdateAvailableResource(outputResource.name, resourceChange);
                        vesselData.ResourceChange(outputResource.name, resourceChange);
                    }
                }
            }
        }

        private static void ProcessUnloadedResourceConverters(VesselData vesselData, double elapsedTime)
        {
            if (DetectKerbalism.Found())
                return;

            foreach (KeyValuePair<uint, ModuleResourceConverterData> keyValuePair in vesselData.ResourceConverters)
            {
                ModuleResourceConverterData resourceConverterData = keyValuePair.Value;
                resourceConverterData.ProtoPartSnapshot = vesselData.Vessel.protoVessel.protoPartSnapshots[resourceConverterData.PartIndex];

                for (var i = 0; i < resourceConverterData.ModuleResourceConverters.Count; i++)
                {
                    ModuleResourceConverter resourceConverter = resourceConverterData.ModuleResourceConverters[i];

                    // load protoPartModuleSnapshot
                    var moduleIndex = resourceConverterData.ProtoPartModuleSnapshotIndexes[i];
                    ProtoPartModuleSnapshot protoPartModuleSnapshot = resourceConverterData.ProtoPartSnapshot.modules[moduleIndex];

                    // read persistent IsActivated setting
                    bool.TryParse(protoPartModuleSnapshot.moduleValues.GetValue(nameof(resourceConverter.IsActivated)), out resourceConverter.IsActivated);
                    if (resourceConverter.IsActivated == false)
                        continue;

                    // read persistent EfficiencyBonus setting
                    float.TryParse(protoPartModuleSnapshot.moduleValues.GetValue(nameof(resourceConverter.EfficiencyBonus)), out resourceConverter.EfficiencyBonus);
                    if (resourceConverter.EfficiencyBonus <= 0)
                        continue;

                    // determine processRatio ratio
                    double processRatio = 1;

                    // check if we meet the requirements
                    foreach (var resourceRatio in resourceConverter.Recipe.Requirements)
                    {
                        vesselData.AvailableResources.TryGetValue(resourceRatio.ResourceName, out double availableAmount);

                        if (availableAmount < resourceRatio.Ratio * resourceConverterData.ReqMultiplier)
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

                        double requestedRate = resourceConverter.EfficiencyBonus * resourceRatio.Ratio * elapsedTime * resourceConverterData.OutputMultiplier;
                        double spaceRatio = resourceRatio.Ratio > 0 ? Math.Min(1, availableStorage / requestedRate) : 1;

                        if (spaceRatio < processRatio)
                            processRatio = spaceRatio;
                    }

                    if (processRatio <= 0)
                        continue;

                    // check if we meet the input
                    foreach (ResourceRatio resourceRatio in resourceConverter.Recipe.Inputs)
                    {
                        vesselData.AvailableResources.TryGetValue(resourceRatio.ResourceName, out double availableAmount);

                        double requestedRate = resourceConverter.EfficiencyBonus * resourceRatio.Ratio * elapsedTime * resourceConverterData.InputMultiplier;
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

                foreach (KeyValuePair<uint, SolarPanelData> keyValuePair in vesselData.SolarPanels)
                {
                    SolarPanelData solarPanelData = keyValuePair.Value;

                    foreach (ModuleDeployableSolarPanel solarPanel in solarPanelData.ModuleDeployableSolarPanels)
                    {
                        if (solarPanel.chargeRate <= 0)
                            continue;

                        if (solarPanel.deployState != ModuleDeployablePart.DeployState.EXTENDED)
                            continue;

                        double trackingModifier = solarPanel.isTracking ? 1 : 0.25;
                        double powerChange = trackingModifier * solarPanel.chargeRate * starlightRelativeLuminosity * solarPanel.efficiencyMult * solarPanelData.ChargeRateMultiplier;

                        vesselData.UpdateAvailableResource(solarPanel.resourceName, powerChange);
                        vesselData.ResourceChange(solarPanel.resourceName, powerChange);
                    }
                }
            }
        }

        /// <summary>
        /// RequestResource doesn't work when a vessel isn't unloaded, so we have to realize it ourselves
        /// </summary>
        /// <param name="vesselData"></param>
        private static void UpdatePersistentResources(VesselData vesselData, double elapsedTime)
        {
            var relevantResourceChanges = vesselData.ResourceChanges.Where(m => m.Value.Change != 0);

            foreach (var resourceRequest in relevantResourceChanges)
            {
                double fixedChange = resourceRequest.Value.Change * elapsedTime;

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
                                protoPartResourceSnapshot.amount = Math.Max(0, Math.Min(protoPartResourceSnapshot.maxAmount, protoPartResourceSnapshot.amount + fixedChange * fraction));
                            else
                                protoPartResourceSnapshot.amount = 0;
                        }
                        else
                        {
                            var partAvailableSpace = protoPartResourceSnapshot.maxAmount - protoPartResourceSnapshot.amount;

                            var totalAvailableSpace = Math.Min(0, totalAmount - available);

                            var fraction = partAvailableSpace > float.Epsilon && totalAvailableSpace > float.Epsilon ? partAvailableSpace / totalAvailableSpace : 1;

                            protoPartResourceSnapshot.amount = Math.Max(0, Math.Min(protoPartResourceSnapshot.maxAmount, protoPartResourceSnapshot.amount + fixedChange * fraction));
                        }
                    }
                }
            }
        }

        private static void LoadUnloadedParts(VesselData vesselData)
        {
            // check if vessel is initialized for background processing
            if (vesselData.HasAnyActivePersistentEngine.HasValue)
                return;

            // initially assume no active persistent engine present
            vesselData.HasAnyActivePersistentEngine = false;

            for (int partIndex = 0; partIndex < vesselData.Vessel.protoVessel.protoPartSnapshots.Count; partIndex++)
            {
                ProtoPartSnapshot protoPartSnapshot = vesselData.Vessel.protoVessel.protoPartSnapshots[partIndex];

                LoadUnloadedPart(partIndex, protoPartSnapshot, vesselData);
            }
        }

        private static void LoadUnloadedPart(int partIndex, ProtoPartSnapshot protoPartSnapshot, VesselData vesselData)
        {
            LoadTweakScalePartModules(protoPartSnapshot, vesselData);

            Part protoPart = PartLoader.getPartInfoByName(protoPartSnapshot.partName)?.partPrefab;
            if (protoPart == null)
                return;

            LoadModuleDeployableSolarPanel(partIndex, protoPartSnapshot, vesselData, protoPart);

            LoadModuleGenerator(partIndex, protoPartSnapshot, vesselData, protoPart);

            LoadModuleResourceConverters(partIndex, protoPartSnapshot, vesselData, protoPart);

            EngineBackgroundProcessing.LoadPersistentEngine(partIndex, protoPartSnapshot, vesselData, protoPart);
        }

        private static void LoadTweakScalePartModules(ProtoPartSnapshot protoPartSnapshot, VesselData vesselData)
        {
            ProtoPartModuleSnapshot protoPartModuleSnapshot = protoPartSnapshot.FindModule("TweakScale");

            if (protoPartModuleSnapshot == null)
                return;

            double.TryParse(protoPartModuleSnapshot.moduleValues.GetValue("currentScale"), out double currentScale);
            double.TryParse(protoPartModuleSnapshot.moduleValues.GetValue("defaultScale"), out double defaultScale);

            double tweakScalePartMultiplier = defaultScale > 0 && currentScale > 0 ? currentScale / defaultScale : 1;

            vesselData.PartSizeMultipliers.Add(protoPartSnapshot.persistentId, tweakScalePartMultiplier);
        }

        private static SolarPanelData LoadModuleDeployableSolarPanel(int partIndex, ProtoPartSnapshot protoPartSnapshot, VesselData vesselData, Part protoPart)
        {
            var moduleDeployableSolarPanels = protoPart?.FindModulesImplementing<ModuleDeployableSolarPanel>();

            if (moduleDeployableSolarPanels is null || moduleDeployableSolarPanels.Any() == false)
                return null;

            List<ProtoPartModuleSnapshot> protoPartModuleSnapshots = protoPartSnapshot.modules.Where(m => m.moduleName == nameof(ModuleDeployableSolarPanel)).ToList();

            var solarPanelData = new SolarPanelData
            {
                PartIndex = partIndex,
                ProtoPart = protoPart,
                ProtoPartSnapshot = protoPartSnapshot,
                PersistentPartId = protoPartSnapshot.persistentId,
                ProtoPartModuleSnapshots = protoPartModuleSnapshots,
                ModuleDeployableSolarPanels = moduleDeployableSolarPanels
            };

            // calculate tweakScale multiplier
            if (vesselData.PartSizeMultipliers.TryGetValue(protoPartSnapshot.persistentId, out double partSizeMultiplier))
            {
                double? tweakScaleExponent = TweaksSaleHelper.GetTweakScaleExponent(nameof(ModuleDeployableSolarPanel), "chargeRate");

                if (tweakScaleExponent != null)
                    solarPanelData.ChargeRateMultiplier = Math.Pow(partSizeMultiplier, tweakScaleExponent.Value);
            }

            for (int i = 0; i < moduleDeployableSolarPanels.Count; i++)
            {
                var moduleDeployableSolarPanel = moduleDeployableSolarPanels[i];
                var protoPartModuleSnapshot = protoPartModuleSnapshots[i];

                moduleDeployableSolarPanel.deployState = (ModuleDeployablePart.DeployState)Enum.Parse(typeof(ModuleDeployablePart.DeployState), protoPartModuleSnapshot.moduleValues.GetValue(nameof(moduleDeployableSolarPanel.deployState)));
            }

            // store data
            vesselData.SolarPanels.Add(protoPartSnapshot.persistentId, solarPanelData);

            return solarPanelData;
        }

        private static ModuleGeneratorData LoadModuleGenerator(int partIndex, ProtoPartSnapshot protoPartSnapshot, VesselData vesselData, Part protoPart)
        {
            var moduleGenerators = protoPart?.FindModulesImplementing<ModuleGenerator>();

            if (moduleGenerators is null || moduleGenerators.Any() == false)
                return null;

            List<int> protoPartModuleSnapshotIndexes = new List<int>();
            List<ProtoPartModuleSnapshot> protoPartModuleSnapshots = new List<ProtoPartModuleSnapshot>();

            for (int i = 0; i < protoPartSnapshot.modules.Count; i++)
            {
                ProtoPartModuleSnapshot protoPartModuleSnapshot = protoPartSnapshot.modules[i];

                if (protoPartModuleSnapshot.moduleName == nameof(ModuleGenerator))
                {
                    protoPartModuleSnapshotIndexes.Add(i);
                    protoPartModuleSnapshots.Add(protoPartModuleSnapshot);
                }
            }

            var moduleGeneratorData = new ModuleGeneratorData
            {
                PartIndex = partIndex,
                ProtoPart = protoPart,
                ProtoPartSnapshot = protoPartSnapshot,
                PersistentPartId = protoPartSnapshot.persistentId,
                ProtoPartModuleSnapshotIndexes = protoPartModuleSnapshotIndexes,
                ModuleGenerators = moduleGenerators
            };

            // calculate tweakScale multiplier
            if (vesselData.PartSizeMultipliers.TryGetValue(protoPartSnapshot.persistentId, out double partSizeMultiplier))
            {
                var tweakScaleExponents = TweaksSaleHelper.GetTweakScaleExponents(nameof(ModuleGenerator));

                if (tweakScaleExponents.Exponents.TryGetValue("outputResources.rate", out double outputExponent))
                    moduleGeneratorData.OutputMultiplier = Math.Pow(partSizeMultiplier, outputExponent);

                if (tweakScaleExponents.Exponents.TryGetValue("inputResources.rate", out double inputExponent))
                    moduleGeneratorData.InputMultiplier = Math.Pow(partSizeMultiplier, inputExponent);
            }

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

        private static ModuleResourceConverterData LoadModuleResourceConverters(int partIndex, ProtoPartSnapshot protoPartSnapshot, VesselData vesselData, Part protoPart)
        {
            var moduleResourceConverter = protoPart?.FindModulesImplementing<ModuleResourceConverter>();

            if (moduleResourceConverter is null || moduleResourceConverter.Any() == false)
                return null;

            List<int> protoPartModuleSnapshotIndexes = new List<int>();
            List<ProtoPartModuleSnapshot> protoPartModuleSnapshots = new List<ProtoPartModuleSnapshot>();

            for (int i = 0; i < protoPartSnapshot.modules.Count; i++)
            {
                ProtoPartModuleSnapshot protoPartModuleSnapshot = protoPartSnapshot.modules[i];

                if (protoPartModuleSnapshot.moduleName == nameof(ModuleResourceConverter))
                {
                    protoPartModuleSnapshotIndexes.Add(i);
                    protoPartModuleSnapshots.Add(protoPartModuleSnapshot);
                }
            }

            var resourceConverterData = new ModuleResourceConverterData
            {
                PartIndex = partIndex,
                ProtoPart = protoPart,
                ProtoPartSnapshot = protoPartSnapshot,
                PersistentPartId = protoPartSnapshot.persistentId,
                ModuleResourceConverters = moduleResourceConverter,
                ProtoPartModuleSnapshotIndexes = protoPartModuleSnapshotIndexes
            };

            // calculate tweakScale multipliers
            if (vesselData.PartSizeMultipliers.TryGetValue(protoPartSnapshot.persistentId, out double partSizeMultiplier))
            {
                var tweakScaleExponents = TweaksSaleHelper.GetTweakScaleExponents(nameof(ModuleResourceConverter));

                if (tweakScaleExponents.Exponents.TryGetValue("inputList", out double inputExponent))
                    resourceConverterData.InputMultiplier = Math.Pow(partSizeMultiplier, inputExponent);

                if (tweakScaleExponents.Exponents.TryGetValue("outputList", out double outExponent))
                    resourceConverterData.OutputMultiplier = Math.Pow(partSizeMultiplier, outExponent);

                if (tweakScaleExponents.Exponents.TryGetValue("reqList", out double reqExponent))
                    resourceConverterData.ReqMultiplier = Math.Pow(partSizeMultiplier, reqExponent);
            }

            // readout persistent data
            for (int i = 0; i < moduleResourceConverter.Count; i++)
            {
                ModuleResourceConverter resourceConverter = moduleResourceConverter[i];
                ProtoPartModuleSnapshot protoPartModuleSnapshot = protoPartModuleSnapshots[protoPartModuleSnapshotIndexes[i]];

                resourceConverter.IsActivated = bool.Parse(protoPartModuleSnapshot.moduleValues.GetValue(nameof(resourceConverter.IsActivated)));
                resourceConverter.EfficiencyBonus = float.Parse(protoPartModuleSnapshot.moduleValues.GetValue(nameof(resourceConverter.EfficiencyBonus)));
            }

            vesselData.ResourceConverters.Add(protoPartSnapshot.persistentId, resourceConverterData);

            return resourceConverterData;
        }

    }
}
