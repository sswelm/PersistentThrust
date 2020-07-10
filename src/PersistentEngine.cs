using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Reflection;
using UniLinq;
using UnityEngine;

namespace PersistentThrust
{
    public class PersistentEngineModule
    {
        public string powerEffectName;
        public string runningEffectName;
        public int missingPowerCountdown;
        public bool engineHasAnyMassLessPropellants;
        public bool autoMaximizePersistentIsp;
        public float finalThrust;
        public float propellantReqMetFactor;
        public float persistentIsp;
        public double averageDensity;
        public double demandMass;
        public ModuleEngines engine;
        public List<PersistentPropellant> propellants;
        public double[] fuelDemands = new double[0];
        public readonly Queue<float> propellantReqMetFactorQueue = new Queue<float>();
        public readonly Queue<float> ispQueue = new Queue<float>();
    }

    public class PersistentEngine : PartModule
    {
        // Persistant
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_PersistentThrust"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool HasPersistentThrust = true;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_PersistentHeading"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool HasPersistentHeadingEnabled = true;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_MaximizePersistentIsp"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool MaximizePersistentIsp = true;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_MaximizePersistentPower"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool MaximizePersistentPower = false;
        [KSPField(isPersistant = true)]
        public VesselAutopilot.AutopilotMode persistentAutopilotMode;
        [KSPField(isPersistant = true)]
        public double vesselAlignmentWithAutopilotMode;
        //[KSPField(isPersistant = true, guiActiveEditor = false, guiActive = true, guiName = "#LOC_PT_ManeuverTolerance", guiUnits = " %"), UI_FloatRange(stepIncrement = 1, maxValue = 180, minValue = 0, requireFullControl = false, affectSymCounterparts = UI_Scene.All)]//Beamed Power Throttle
        //public float maneuverTolerance = 180;

        // GUI
        [KSPField(guiFormat = "F1", guiActive = true, guiName = "#autoLOC_6001378", guiUnits = "#autoLOC_7001400")]
        public float realIsp;
        [KSPField(guiFormat = "F6", guiActive = true, guiName = "#autoLOC_6001377")]
        public string thrustTxt;
        [KSPField(guiFormat = "F2", guiActive = true, guiName = "#autoLOC_6001376", guiUnits = "%")]
        public float propellantReqMet;
        [KSPField(guiFormat = "F3")]
        public double masslessUsage;

        // Config Settings
        [KSPField]
        public string throttleAnimationName;
        [KSPField]
        public bool returnToRealtimeAfterKeyPressed = true;
        [KSPField]
        public bool useDynamicBuffer = false;
        [KSPField]
        public bool processMasslessSeparately = true;
        [KSPField]
        public int queueLength = 2;
        [KSPField]
        public float fudgeExponent = 0.27f;
        [KSPField]
        public int missingPowerCountdownSize = 10;
        [KSPField]
        public int bufferSizeMult = 50;
        [KSPField]
        public int propellantReqMetFactorQueueSize = 100;
        [KSPField]
        public double minimumPropellantReqMetFactor = 0.2;
        [KSPField]
        public float headingTolerance = 0.002f;
        [KSPField]
        public bool requestPropMassless = true;             // Flag whether to request massless resources
        [KSPField]
        public bool requestPropMass = true;                 // Flag whether to request resources with mass

        public List<string> powerEffectNameList = new List<string>() ;
        public List<string> runningEffectNameList = new List<string>();

        //public double demandMass;
        public double bufferSize;

        public PersistentEngineModule currentEngine;
        public PersistentEngineModule[] moduleEngines = new PersistentEngineModule[0];
        public MultiModeEngine multiModeEngine;
        public AnimationState[] throttleAnimationState;
        public BaseField masslessUsageField;

        public float persistentThrottle;                // Persistent values to use during TimeWarp
        public float previousFixedDeltaTime;

        public bool isPersistentEngine;             // Flag if using PersistentEngine features        
        public bool warpToReal;                     // Are we transitioning from TimeWarp to realtime?
        public bool isMultiMode;
        public bool useKerbalismInFlight;

        public int vesselChangedSoiCountdown = 10;
        public int fixedUpdateCount;

        private Dictionary<string, double> _availablePartResources = new Dictionary<string, double>();
        private readonly Dictionary<string, double> _kerbalismResourceChangeRequest = new Dictionary<string, double>();
        private readonly Queue<float> _mainThrottleQueue = new Queue<float>();
        private static Assembly _realFuelsAssembly;

        private List<PersistentEngine> _persistentEngines;

        //public const double Rad2Deg = 180 / Math.PI; // 57.295779513;

        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor) return;

            if (!string.IsNullOrEmpty(throttleAnimationName))
                throttleAnimationState = SetUpAnimation(throttleAnimationName, part);

            _persistentEngines = vessel.FindPartModulesImplementing<PersistentEngine>();

            masslessUsageField = Fields[nameof(this.masslessUsage)];
        }

        public void VesselChangedSOI()
        {
            vesselChangedSoiCountdown = 10;
        }

        // Make "moduleEngine" and "moduleEngineFx" fields refer to the ModuleEngines and ModuleEnginesFX modules in part.Modules
        private void FindModuleEngines()
        {
            var moduleEnginesCount = 0;

            var moduleEnginesList = new List<PersistentEngineModule>();

            foreach (var partModule in part.Modules)
            {
                if (partModule is MultiModeEngine multiMode)
                    multiModeEngine = multiMode;
                else if (partModule is ModuleEngines engine)
                {
                    currentEngine = new PersistentEngineModule {engine = engine};

                    if (engine is ModuleEnginesFX engineFx)
                    {
                        powerEffectNameList.Add(engineFx.powerEffectName);
                        currentEngine.powerEffectName = engineFx.powerEffectName;
                        ApplyEffect(currentEngine.powerEffectName, 0);

                        runningEffectNameList.Add(engineFx.runningEffectName);
                        currentEngine.runningEffectName = engineFx.runningEffectName;
                        ApplyEffect(currentEngine.runningEffectName, 0);
                    }

                    moduleEnginesList.Add(currentEngine);

                    moduleEnginesCount++;
                }
            }

            moduleEngines = moduleEnginesList.ToArray();

            if (moduleEnginesCount == 1 && multiModeEngine == null)
            {
                Debug.Log("[PersistentThrust]: enabled for " + part.partInfo.title + " " + part.persistentId);
                isPersistentEngine = true;
            }
            else if (moduleEnginesCount == 0)
            {
                Debug.LogError("[PersistentThrust]: found no compatible engines, disabling PersistentThrust for " + part.partInfo.title + " " + part.persistentId);
                isPersistentEngine = false;
            }
            else if (moduleEnginesCount == 1 && multiModeEngine != null)
            {
                Debug.LogWarning("[PersistentThrust]: found Insufficient engines for MultiMode, using single engine mode PersistentThrust for " + part.partInfo.title + " " + part.persistentId);
                isPersistentEngine = false;
            }
            else if (moduleEnginesCount > 1 && multiModeEngine == null)
            {
                Debug.LogWarning("[PersistentThrust]: found multiple engines but no MultiMode PartModule, enabled multi engine PersistentThrust for " + part.partInfo.title + " " + part.persistentId);
                isPersistentEngine = true;
            }
            else if (multiModeEngine != null && moduleEnginesCount == 2)
            {
                Debug.Log("[PersistentThrust]: enabled MultiMode for " + part.partInfo.title + " " + part.persistentId);
                isPersistentEngine = true;
                isMultiMode = true;
            }
            else
            {
                Debug.LogError("[PersistentThrust]: failed to initialize for " + part.partInfo.title + " " + part.persistentId);
                isPersistentEngine = false;
            }

            if (!isPersistentEngine) return;

            var engineFxList = part.FindModulesImplementing<ModuleEnginesFX>();
            foreach (var engineFx in engineFxList)
            {
                engineFx.powerEffectName = string.Empty;
                engineFx.runningEffectName = string.Empty;
            }
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            SetAnimationRatio(0, throttleAnimationState);

            foreach (var engine in moduleEngines)
            {
                currentEngine = engine;

                UpdateFX();
            }
        }

        // Finds the active moduleEngine module from the MultiModeEngine partModule
        private void FetchActiveMode()
        {
            for (var i = 0; i < moduleEngines.Length; i++)
            {
                var persistentEngineModule = moduleEngines[i];
                persistentEngineModule.powerEffectName = powerEffectNameList[i];
                persistentEngineModule.runningEffectName = runningEffectNameList[i];

                ApplyEffect(persistentEngineModule.powerEffectName, 0);
                ApplyEffect(persistentEngineModule.runningEffectName, 0);
            }

            if (!isMultiMode)
                return;

            currentEngine = moduleEngines.FirstOrDefault(m =>
                m.engine == (multiModeEngine.runningPrimary
                    ? multiModeEngine.PrimaryEngine
                    : multiModeEngine.SecondaryEngine));
        }

        private void ApplyEffect(string effect, float ratio)
        {
            if (string.IsNullOrEmpty(name))
                return;

            part.Effect(effect, ratio, -1);
        }

        // Update is called during refresh frame, which can be less frequent than FixedUpdate which is called every processing frame
        public override void OnUpdate()
        {
            var processedEngines = isMultiMode ? new[] { currentEngine } : moduleEngines;

            for (var i = 0; i < processedEngines.Length; i++)
            {
                currentEngine = processedEngines[i];

                if (currentEngine.engine == null) continue;

                // hide stock thrust
                currentEngine.engine.Fields["finalThrust"].guiActive = false;
                currentEngine.engine.Fields["realIsp"].guiActive = false;
                currentEngine.engine.Fields["propellantReqMet"].guiActive = false;
            }

            var averagePropellantReqMetFactor = isMultiMode
                ? currentEngine.propellantReqMetFactor
                : moduleEngines.Average(m => m.propellantReqMetFactor);

            propellantReqMet = averagePropellantReqMetFactor * 100;

            var anyMasslessPropellants = isMultiMode
                ? currentEngine.engine.propellants.Any(m => m.resourceDef.density == 0)
                : moduleEngines.SelectMany(m => m.engine.propellants.Where(p => p.resourceDef.density == 0)).Any();

            var anyAutoMaximizePersistentIsp = isMultiMode
                ? currentEngine.autoMaximizePersistentIsp
                : moduleEngines.Any(m => m.autoMaximizePersistentIsp);

            realIsp = !vessel.packed && !anyMasslessPropellants
                ? currentEngine.engine.realIsp
                : vessel.packed && (MaximizePersistentIsp || anyAutoMaximizePersistentIsp) ||  persistentThrottle == 0
                    ? currentEngine.persistentIsp
                    : currentEngine.persistentIsp * averagePropellantReqMetFactor;

            if (!isPersistentEngine || !HasPersistentThrust) return;

            // When transitioning from TimeWarp to real update throttle
            if (warpToReal)
            {
                SetThrottle(persistentThrottle, true);
                warpToReal = false;
            }

            if (vessel.packed)
            {
                // maintain thrust setting during TimeWarp
                vessel.ctrlState.mainThrottle = persistentThrottle;

                // stop engines when X pressed
                if (Input.GetKeyDown(KeyCode.X))
                    SetThrottle(0, returnToRealtimeAfterKeyPressed);
                // full throttle when Z pressed
                else if (Input.GetKeyDown(KeyCode.Z))
                    SetThrottle(1, returnToRealtimeAfterKeyPressed);
                // increase throttle when Shift pressed
                else if (Input.GetKeyDown(KeyCode.LeftShift))
                    SetThrottle(Mathf.Min(1, persistentThrottle + 0.01f), returnToRealtimeAfterKeyPressed);
                // decrease throttle when Ctrl pressed
                else if (Input.GetKeyDown(KeyCode.LeftControl))
                    SetThrottle(Mathf.Max(0, persistentThrottle - 0.01f), returnToRealtimeAfterKeyPressed);
            }
            else
                TimeWarp.GThreshold = 12;
        }

        private void SetThrottle(float newSetting, bool returnToRealTime)
        {
            vessel.ctrlState.mainThrottle = newSetting;
            currentEngine.engine.requestedThrottle = newSetting;
            currentEngine.engine.currentThrottle = newSetting;

            if (!returnToRealTime) return;

            // Return to realtime
            TimeWarp.SetRate(0, true);

            if (_realFuelsAssembly == null || !(newSetting > 0)) return;

            var ignitedInfo = currentEngine.engine.GetType().GetField("ignited", BindingFlags.NonPublic | BindingFlags.Instance);

            if (ignitedInfo == null)
                return;

            ignitedInfo.SetValue(currentEngine.engine, true);
        }

        // Check if RealFuels is installed 
        public override void OnAwake()
        {
            base.OnAwake();
            if (HighLogic.LoadedScene != GameScenes.LOADING && _realFuelsAssembly == null)
                _realFuelsAssembly = AssemblyLoader.loadedAssemblies.FirstOrDefault(x => x.name.StartsWith("RealFuels"))?.assembly;
        }

        // Initialization
        public override void OnLoad(ConfigNode node)
        {
            // Run base OnLoad method
            base.OnLoad(node);

            Debug.Log("[PersistentThrust]: OnLoad called for " + part.partInfo.title + " " + part.persistentId);

            // Populate moduleEngine and moduleEngineFx fields
            FindModuleEngines();

            // Initialize PersistentPropellant list
            if (isMultiMode)
            {
                moduleEngines[0].propellants = PersistentPropellant.MakeList(moduleEngines[0].engine.propellants);
                moduleEngines[1].propellants = PersistentPropellant.MakeList(moduleEngines[1].engine.propellants);

                moduleEngines[0].averageDensity = moduleEngines[0].propellants.AverageDensity();
                moduleEngines[1].averageDensity = moduleEngines[1].propellants.AverageDensity();
            }
            else
            {
                foreach (var engine in moduleEngines)
                {
                    engine.propellants = PersistentPropellant.MakeList(engine.engine.propellants);
                    engine.averageDensity = engine.propellants.AverageDensity();
                }
            }
        }

        private void ReloadPropellantsWithoutMasslessPropellants()
        {
            var akPropellants = new ConfigNode();

            //Get the Ignition state, i.e. is the moduleEngine shutdown or activated
            var ignitionState = currentEngine.engine.getIgnitionState;

            currentEngine.engine.Shutdown();

            foreach (var propellant in currentEngine.propellants)
            {
                if (propellant.density == 0)
                    continue;

                var propellantConfig = LoadPropellant(propellant.propellant.name, propellant.propellant.ratio);
                akPropellants.AddNode(propellantConfig);
            }

            currentEngine.engine.Load(akPropellants);

            if (ignitionState)
                currentEngine.engine.Activate();
        }

        private static ConfigNode LoadPropellant(string akName, float akRatio)
        {
            Debug.Log("[PersistentThrust]: LoadPropellant: " + akName + " " + akRatio);

            var propellantNode = new ConfigNode().AddNode("PROPELLANT");
            propellantNode.AddValue("name", akName);
            propellantNode.AddValue("ratio", akRatio);
            propellantNode.AddValue("DrawGauge", true);

            return propellantNode;
        }

        private void UpdatePersistentThrottle()
        {
            _mainThrottleQueue.Enqueue(vessel.ctrlState.mainThrottle);
            if (_mainThrottleQueue.Count > queueLength)
                _mainThrottleQueue.Dequeue();
            persistentThrottle = _mainThrottleQueue.Max();
        }

        private void UpdatePersistentIsp()
        {
            currentEngine.ispQueue.Enqueue(currentEngine.engine.realIsp);
            if (currentEngine.ispQueue.Count > queueLength)
                currentEngine.ispQueue.Dequeue();
            currentEngine.persistentIsp = currentEngine.ispQueue.Max();
        }

        // Calculate demands of each resource
        public double[] CalculateDemands(double mass)
        {
            var demands = new double[currentEngine.propellants.Count];
            if (!(mass > 0)) return demands;

            // Per propellant demand
            for (var i = 0; i < currentEngine.propellants.Count; i++)
            {
                demands[i] = currentEngine.propellants[i].CalculateDemand(mass);
            }
            return demands;
        }

        public void UpdateBuffers()
        {
            for (var i = 0; i < currentEngine.propellants.Count; i++)
            {
                var currentPropellant = currentEngine.propellants[i];

                if (currentPropellant.density != 0 || i >= currentEngine.fuelDemands.Count()) continue;

                // find initial resource amount for propellant
                var availablePropellant = LoadPropellantAvailability(currentPropellant);

                // update power buffer
                bufferSize = UpdateBuffer(availablePropellant, currentEngine.fuelDemands[i]);

                // update request
                RequestResource(currentPropellant, 0);
            }
        }

        private PersistentPropellant LoadPropellantAvailability(PersistentPropellant currentPersistentPropellant)
        {
            var activePropellant = currentPersistentPropellant;

            var activePropellants = _persistentEngines.SelectMany(pe => pe.moduleEngines.SelectMany(pl =>
                pl.propellants.Where(pp =>
                    pp.missionTime == vessel.missionTime &&
                    pp.definition.id == currentPersistentPropellant.definition.id))).ToList();

            if (activePropellants.Any())
                return activePropellants.First();

            // store mission time to prevent other engines doing unnecessary work
            currentPersistentPropellant.missionTime = vessel.missionTime;
            // determine amount and maxAmount at start of PersistentEngine testing
            part.GetConnectedResourceTotals(currentPersistentPropellant.definition.id,
                currentPersistentPropellant.propellant.GetFlowMode(), out currentPersistentPropellant.amount,
                out currentPersistentPropellant.maxAmount, true);
            // calculate total demand on operational engines
            currentPersistentPropellant.totalEnginesDemand = _persistentEngines
                .Where(e => e.currentEngine.engine.getIgnitionState)
                .Sum(m => m.currentEngine.propellants
                    .Where(l => l.definition.id == currentPersistentPropellant.definition.id)
                    .Sum(l => l.normalizedDemand));

            return activePropellant;
        }

        // Apply demanded resources & return results
        // Updated depleted boolean flag if resource request failed
        public virtual double[] ApplyDemands(double[] demands, ref float finalPropellantReqMetFactor)
        {
            double overallPropellantReqMet = 1;

            var demandsOut = new double[currentEngine.propellants.Count];

            // first do a simulation run to determine the propellant availability so we don't over consume
            for (var i = 0; i < currentEngine.propellants.Count; i++)
            {
                currentEngine.autoMaximizePersistentIsp = false;

                PersistentPropellant persistentPropellant = currentEngine.propellants[i];
                // Request resources if:
                // - resource has mass & request mass flag true
                // - resource massless & request massless flag true
                if ((!(persistentPropellant.density > 0) || !requestPropMass) && (persistentPropellant.density != 0 || !requestPropMassless)) continue;

                persistentPropellant.demandIn = demands[i];
                var storageModifier = 1.0;

                // Process massless propellants like ElectricCharge separately
                if (persistentPropellant.density == 0)
                {
                    // find initial resource amount for propellant
                    var availablePropellant = LoadPropellantAvailability(persistentPropellant);

                    _availablePartResources.TryGetValue(persistentPropellant.definition.name, out var kerbalismAmount);

                    var currentPropellantAmount = useKerbalismInFlight ? kerbalismAmount : availablePropellant.amount;

                    // update power buffer
                    bufferSize = UpdateBuffer(availablePropellant, persistentPropellant.demandIn);

                    var bufferedTotalEnginesDemand = Math.Min(availablePropellant.maxAmount, availablePropellant.totalEnginesDemand * bufferSizeMult);

                    if (bufferedTotalEnginesDemand > currentPropellantAmount && availablePropellant.totalEnginesDemand > 0)
                        storageModifier = Math.Min(1, (persistentPropellant.demandIn / availablePropellant.totalEnginesDemand) + currentPropellantAmount / bufferedTotalEnginesDemand * (persistentPropellant.demandIn / availablePropellant.totalEnginesDemand));

                    if (!MaximizePersistentPower && currentPropellantAmount < bufferSize)
                        storageModifier *= currentPropellantAmount / bufferSize;
                }

                persistentPropellant.demandOut = IsInfinite(persistentPropellant.propellant) 
                    ? persistentPropellant.demandIn 
                    : RequestResource(persistentPropellant, persistentPropellant.demandIn * storageModifier, true);

                var propellantFoundRatio = persistentPropellant.demandOut >= persistentPropellant.demandIn 
                    ? 1 : persistentPropellant.demandIn > 0 ? persistentPropellant.demandOut / persistentPropellant.demandIn : 1;

                if (propellantFoundRatio < overallPropellantReqMet)
                    overallPropellantReqMet = propellantFoundRatio;

                if (persistentPropellant.propellant.resourceDef.density > 0)
                {
                    // reset stabilize Queue when out of mass propellant
                    if (propellantFoundRatio < 1)
                        currentEngine.propellantReqMetFactorQueue.Clear();
                }
                else if (propellantFoundRatio == 0)
                {
                    // reset stabilize Queue when out power for too long
                    if (currentEngine.missingPowerCountdown <= 0)
                        currentEngine.propellantReqMetFactorQueue.Clear();
                    currentEngine.missingPowerCountdown--;
                }
                else
                    currentEngine.missingPowerCountdown = missingPowerCountdownSize;
            }

            // attempt to stabilize thrust output with First In Last Out Queue 
            currentEngine.propellantReqMetFactorQueue.Enqueue((float)overallPropellantReqMet);
            if (currentEngine.propellantReqMetFactorQueue.Count() > propellantReqMetFactorQueueSize)
                currentEngine.propellantReqMetFactorQueue.Dequeue();
            var averagePropellantReqMetFactor = currentEngine.propellantReqMetFactorQueue.Average();

            if (averagePropellantReqMetFactor < minimumPropellantReqMetFactor)
                currentEngine.autoMaximizePersistentIsp = true;

            finalPropellantReqMetFactor = !vessel.packed || MaximizePersistentIsp || currentEngine.autoMaximizePersistentIsp ? averagePropellantReqMetFactor : Mathf.Pow(averagePropellantReqMetFactor, fudgeExponent);

            // secondly we can consume the resource based on propellant availability
            for (var i = 0; i < currentEngine.propellants.Count; i++)
            {
                var pp = currentEngine.propellants[i];
                // Request resources if:
                // - resource has mass & request mass flag true
                // - resource massless & request massless flag true
                if ((pp.density > 0 && requestPropMass) || (pp.density == 0 && requestPropMassless))
                {
                    var demandIn = pp.density > 0 
                        ? MaximizePersistentIsp || currentEngine.autoMaximizePersistentIsp ? averagePropellantReqMetFactor * demands[i] :  demands[i] 
                        : overallPropellantReqMet * demands[i];

                    var demandOut = IsInfinite(pp.propellant) ? demandIn : RequestResource(pp, demandIn, false);
                    demandsOut[i] = demandOut;
                }
                // Otherwise demand is 0
                else
                    demandsOut[i] = 0;
            }
            // Return demand outputs
            return demandsOut;
        }

        private double RequestResource(PersistentPropellant propellant, double demand, bool simulate = false)
        {
            if (propellant.density > 0 && !vessel.packed)
                return demand;

            if (!useKerbalismInFlight)
                return part.RequestResource(propellant.definition.id, demand, propellant.propellant.GetFlowMode(), simulate);

            _availablePartResources.TryGetValue(propellant.definition.name, out var currentAmount);

            var available = Math.Min(currentAmount, demand);

            if (simulate)
                _availablePartResources[propellant.definition.name] = Math.Max(0, currentAmount - demand);
            else
            {
                var demandPerSecond = demand / TimeWarp.fixedDeltaTime;

                _kerbalismResourceChangeRequest.TryGetValue(propellant.definition.name, out var currentDemand);
                _kerbalismResourceChangeRequest[propellant.definition.name] = currentDemand - demandPerSecond;
            }

            return available;
        }

        public double UpdateBuffer(PersistentPropellant propellant, double baseSize)
        {
            var requiredBufferSize = useDynamicBuffer 
                ? Math.Max(baseSize / TimeWarp.fixedDeltaTime * 10 * bufferSizeMult, baseSize * bufferSizeMult) 
                : Math.Max(0, propellant.maxAmount - baseSize);

            if (previousFixedDeltaTime == TimeWarp.fixedDeltaTime)
                return requiredBufferSize;

            var amountRatio = propellant.maxAmount > 0 ? Math.Min(1, propellant.amount / propellant.maxAmount) : 0;

            var dynamicBufferSize = useDynamicBuffer ? requiredBufferSize : 0;

            if (!(dynamicBufferSize > 0)) 
                return requiredBufferSize;

            var partResource = part.Resources[propellant.definition.name];
            if (partResource == null)
            {
                var node = new ConfigNode("RESOURCE");
                node.AddValue("name", propellant.definition.name);
                node.AddValue("maxAmount", 0);
                node.AddValue("amount", 0);
                part.AddResource(node);

                partResource = part.Resources[propellant.definition.name];
            }

            partResource.maxAmount = dynamicBufferSize;
            partResource.amount = dynamicBufferSize * amountRatio;

            return requiredBufferSize;
        }

        private bool IsInfinite(Propellant propellant)
        {
            if (propellant.resourceDef.density == 0)
                return CheatOptions.InfiniteElectricity;
            else
                return CheatOptions.InfinitePropellant;
        }

        // Calculate DeltaV vector
        public static Vector3d CalculateDeltaVVector(double densityPropellantAverage, double vesselMass, double deltaTime, double thrust, float isp, Vector3d thrustVector, out double mass)
        {
            // Mass flow rate
            var massFlowRate = isp > 0 ? thrust / (isp * PhysicsGlobals.GravitationalAcceleration) : 0;
            // Change in mass over time interval dT
            var deltaMass = massFlowRate * deltaTime;
            // Resource demand from propellants with mass
            mass = densityPropellantAverage > 0 ? deltaMass / densityPropellantAverage : 0;
            //// Resource demand from propellants with mass
            var remainingMass = vesselMass - deltaMass;
            // deltaV amount
            var deltaV = isp * PhysicsGlobals.GravitationalAcceleration * Math.Log(remainingMass > 0 ? vesselMass / remainingMass : 1);
            // Return deltaV vector
            return deltaV * thrustVector;
        }

        public virtual void UpdateFX()
        {
            var exhaustRatio = (float)(currentEngine.engine.maxThrust > 0 ? currentEngine.finalThrust / currentEngine.engine.maxThrust : 0);

            ApplyEffect(currentEngine.powerEffectName, exhaustRatio);
            ApplyEffect(currentEngine.runningEffectName, exhaustRatio);
        }

        private void UpdatePropellantReqMetFactorQueue()
        {
            if (currentEngine.propellantReqMetFactor > 0 && persistentThrottle > 0)
            {
                currentEngine.propellantReqMetFactorQueue.Enqueue(currentEngine.engineHasAnyMassLessPropellants
                    ? Mathf.Pow(currentEngine.propellantReqMetFactor, 1 / fudgeExponent)
                    : currentEngine.propellantReqMetFactor);

                if (currentEngine.propellantReqMetFactorQueue.Count > propellantReqMetFactorQueueSize)
                    currentEngine.propellantReqMetFactorQueue.Dequeue();
            }
            else
                currentEngine.propellantReqMetFactorQueue.Clear();
        }

        // Physics update
        public void FixedUpdate() // FixedUpdate is also called while not staged
        {
            if (this.vessel is null || currentEngine.engine is null || !isEnabled) return;

            fixedUpdateCount++;
            var universalTime = Planetarium.GetUniversalTime();

            // restore heading at load
            if (HasPersistentHeadingEnabled && fixedUpdateCount <= 60 && vesselAlignmentWithAutopilotMode > 0.995)
            {
                vessel.Autopilot.SetMode(persistentAutopilotMode);
                vessel.PersistHeading(TimeWarp.fixedDeltaTime, headingTolerance, vesselChangedSoiCountdown > 0);
            }
            else
                persistentAutopilotMode = vessel.Autopilot.Mode;

            //vesselHeadingVersusManeuver = vessel.VesselOrbitHeadingVersusManeuverVector();
            //vesselHeadingVersusManeuverInDegrees = Math.Acos(Math.Max(-1, Math.Min(1, vesselHeadingVersusManeuver))) * Rad2Deg;

            _kerbalismResourceChangeRequest.Clear();

            if (vesselChangedSoiCountdown > 0)
                vesselChangedSoiCountdown--;

            // Checks if moduleEngine mode wasn't switched
            FetchActiveMode();

            var processedEngines = isMultiMode ? new[] { currentEngine } : moduleEngines;

            // Realtime mode
            if (!vessel.packed)
            {
                vesselAlignmentWithAutopilotMode = vessel.HeadingVersusAutopilotVector(universalTime);

                // Update persistent thrust throttle if NOT transitioning from warp to realtime
                if (!warpToReal)
                    UpdatePersistentThrottle();

                for (var i = 0; i < processedEngines.Length; i++)
                {
                    currentEngine = processedEngines[i];

                    ResetMonitoringVariables();

                    // Update persistent thrust isp if NOT transitioning from warp to realtime
                    if (!warpToReal)
                        UpdatePersistentIsp();

                    currentEngine.engineHasAnyMassLessPropellants = currentEngine.engine.propellants.Any(m => m.resourceDef.density == 0);

                    if (processMasslessSeparately && currentEngine.engineHasAnyMassLessPropellants)
                        ReloadPropellantsWithoutMasslessPropellants();

                    //if (vesselHeadingVersusManeuverInDegrees > maneuverTolerance)
                    //{
                    //    moduleEngine.maxFuelFlow = 1e-10f;
                    //    finalThrust = 0;
                    //}
                    //else 

                    if (!currentEngine.engine.getIgnitionState)
                    {
                        currentEngine.finalThrust = 0;

                        // restore maximum flow
                        RestoreMaxFuelFlow();
                    }
                    else if (!currentEngine.engineHasAnyMassLessPropellants && currentEngine.engine.propellantReqMet > 0)
                    {
                        // Mass flow rate
                        var massFlowRate = currentEngine.persistentIsp > 0
                            ? currentEngine.engine.currentThrottle * currentEngine.engine.maxThrust / (currentEngine.persistentIsp * PhysicsGlobals.GravitationalAcceleration)
                            : 0;
                        // Change in mass over time interval dT
                        var deltaMass = massFlowRate * TimeWarp.fixedDeltaTime;
                        // Resource demand from propellants with mass
                        currentEngine.demandMass = currentEngine.averageDensity > 0 ? deltaMass / currentEngine.averageDensity : 0;
                        // Calculate resource demands
                        currentEngine.fuelDemands = CalculateDemands(currentEngine.demandMass);
                        // Apply resource demands & test for resource depletion
                        ApplyDemands(currentEngine.fuelDemands, ref currentEngine.propellantReqMetFactor);

                        // calculate maximum flow
                        var maxFuelFlow = currentEngine.persistentIsp > 0 ? currentEngine.engine.maxThrust / (currentEngine.persistentIsp * PhysicsGlobals.GravitationalAcceleration) : 0;

                        // adjust fuel flow 
                        currentEngine.engine.maxFuelFlow = maxFuelFlow > 0 && currentEngine.propellantReqMetFactor > 0 ? (float) (maxFuelFlow * currentEngine.propellantReqMetFactor) : 1e-10f;

                        // update displayed thrust and fx
                        currentEngine.finalThrust = currentEngine.engine.currentThrottle * currentEngine.engine.maxThrust * Math.Min(currentEngine.propellantReqMetFactor, currentEngine.engine.propellantReqMet * 0.01f);
                    }
                    else
                    {
                        // restore maximum flow
                        RestoreMaxFuelFlow();

                        currentEngine.propellantReqMetFactor = currentEngine.engine.propellantReqMet * 0.01f;

                        currentEngine.finalThrust = currentEngine.engine.GetCurrentThrust();
                    }

                    UpdatePropellantReqMetFactorQueue();

                    UpdateFX();

                    SetThrottleAnimation();

                    UpdateBuffers();
                }
            }
            else
            {
                for (var i = 0; i < processedEngines.Length; i++)
                {
                    currentEngine = processedEngines[i];

                    ResetMonitoringVariables();

                    // restore maximum flow
                    RestoreMaxFuelFlow();

                    if (persistentThrottle > 0 && currentEngine.persistentIsp > 0 && isPersistentEngine && HasPersistentThrust)
                    {
                        if (TimeWarp.CurrentRateIndex == 0)
                            warpToReal = true; // Set to true for transition to realtime

                        // Calculated requested thrust
                        //var requestedThrust = vesselHeadingVersusManeuverInDegrees <= maneuverTolerance ? moduleEngine.thrustPercentage * 0.01f * persistentThrottle * moduleEngine.maxThrust : 0;
                        var requestedThrust = currentEngine.engine.thrustPercentage * 0.01f * persistentThrottle * currentEngine.engine.maxThrust;

                        var thrustVector = part.transform.up; // Thrust direction unit vector
                        // Calculate deltaV vector & resource demand from propellants with mass
                        var deltaVVector = CalculateDeltaVVector(currentEngine.averageDensity, vessel.totalMass, TimeWarp.fixedDeltaTime, requestedThrust, currentEngine.persistentIsp, thrustVector, out currentEngine.demandMass);
                        // Calculate resource demands
                        currentEngine.fuelDemands = CalculateDemands(currentEngine.demandMass);
                        // Apply resource demands & test for resource depletion
                        ApplyDemands(currentEngine.fuelDemands, ref currentEngine.propellantReqMetFactor);

                        // Apply deltaV vector at UT & dT to orbit if resources not depleted
                        if (currentEngine.propellantReqMetFactor > 0)
                        {
                            currentEngine.finalThrust = requestedThrust * currentEngine.propellantReqMetFactor;
                            vessel.orbit.Perturb(deltaVVector * currentEngine.propellantReqMetFactor, universalTime);
                        }

                        // Otherwise log warning and drop out of TimeWarp if throttle on & depleted
                        else if (persistentThrottle > 0)
                        {
                            currentEngine.finalThrust = 0;
                            Debug.Log("[PersistentThrust]: Thrust warp stopped - propellant depleted");
                            ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_PT_StoppedDepleted"), 5.0f, ScreenMessageStyle.UPPER_CENTER);
                            // Return to realtime
                            TimeWarp.SetRate(0, true);
                            if (!vessel.IsControllable)
                            {
                                persistentThrottle = 0;
                                vessel.ctrlState.mainThrottle = 0;
                            }
                        }
                        else
                            currentEngine.finalThrust = 0;

                        SetThrottleAnimation();

                        UpdateFX();
                    }
                    else
                    {
                        currentEngine.finalThrust = 0;

                        SetThrottleAnimation();

                        UpdateFX();

                        UpdateBuffers();
                    }
                }

                if (vessel.IsControllable && HasPersistentHeadingEnabled)
                    vesselAlignmentWithAutopilotMode = vessel.PersistHeading(TimeWarp.fixedDeltaTime, headingTolerance, vesselChangedSoiCountdown > 0, vesselAlignmentWithAutopilotMode == 1);
            }
            // display final thrust in a user friendly way
            thrustTxt = Utils.FormatThrust(moduleEngines.Sum(m => m.finalThrust));

            previousFixedDeltaTime = TimeWarp.fixedDeltaTime;

            UpdateMasslessConsumption();
        }

        private void ResetMonitoringVariables()
        {
            // reset monitoring variables
            currentEngine.propellants.ForEach(m =>
            {
                m.demandIn = 0;
                m.demandOut = 0;
            });
        }

        private void UpdateMasslessConsumption()
        {
            var masslessPropellant = isMultiMode
                ? currentEngine.propellants.Where(m => m.density == 0).FirstOrDefault()
                : moduleEngines.SelectMany(m => m.propellants.Where(p => p.density == 0)).FirstOrDefault();

            if (masslessPropellant != null)
            {
                masslessUsageField.guiActive = true;
                masslessUsageField.guiName = masslessPropellant.definition.displayName;

                masslessUsage = (isMultiMode
                    ? currentEngine.propellants.Sum(m => m.demandOut)
                    : moduleEngines.Sum(m => m.propellants.Sum(l => l.demandOut))) / TimeWarp.fixedDeltaTime; ;
            }
            else
                masslessUsageField.guiActive = false;
        }

        private void RestoreMaxFuelFlow()
        {
            currentEngine.engine.maxFuelFlow = (float) (currentEngine.persistentIsp > 0 ? currentEngine.engine.maxThrust / (currentEngine.persistentIsp * PhysicsGlobals.GravitationalAcceleration) : 1e-10f);
        }

        private static AnimationState[] SetUpAnimation(string animationName, Part part)
        {
            var states = new List<AnimationState>();
            foreach (var animation in part.FindModelAnimators(animationName))
            {
                var animationState = animation[animationName];
                animationState.speed = 0;
                animationState.enabled = true;
                animationState.wrapMode = WrapMode.ClampForever;
                animation.Blend(animationName);
                states.Add(animationState);
            }
            return states.ToArray();
        }

        private void SetThrottleAnimation()
        {
            SetAnimationRatio(currentEngine.engine.maxThrust > 0 ? currentEngine.finalThrust / currentEngine.engine.maxThrust : 0, throttleAnimationState);
        }

        private static void SetAnimationRatio(float ratio, AnimationState[] animationState)
        {
            if (animationState == null) return;

            foreach (var anim in animationState)
            {
                anim.normalizedTime = ratio;
            }
        }

        #region Kerbalism

        // Called by Kerbalism every frame
        public virtual string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
        {
            useKerbalismInFlight = true;

            _availablePartResources = availableResources;

            resourceChangeRequest.Clear();

            foreach (var resourceRequest in _kerbalismResourceChangeRequest)
            {
                resourceChangeRequest.Add(new KeyValuePair<string, double>(resourceRequest.Key, resourceRequest.Value));
            }

            return part.partInfo.title;
        }

        #endregion
    }
}
