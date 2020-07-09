using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Reflection;
using UniLinq;
using UnityEngine;

namespace PersistentThrust
{
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

        [KSPField]
        public string powerEffectName;
        [KSPField]
        public string runningEffectName;
        //[KSPField]
        //public double vesselHeadingVersusManeuver;
        //[KSPField]
        //public double vesselHeadingVersusManeuverInDegrees;

        public List<string> powerEffectNameList = new List<string>() ;
        public List<string> runningEffectNameList = new List<string>();

        public double demandMass;
        public double dynamicBufferSize;

        // Engine module on the same part
        public List<ModuleEngines> engines = new List<ModuleEngines>();
        public ModuleEngines moduleEngine;
        public MultiModeEngine multiModeEngine;
        public AnimationState[] throttleAnimationState;

        public float throttlePersistent;                // Persistent values to use during TimeWarp
        public float ispPersistent;

        public float finalThrust;
        [KSPField(guiActive = true)]
        public float propellantReqMetFactor;
        public float previousFixedDeltaTime;

        public bool isPersistentEngine;             // Flag if using PersistentEngine features        
        public bool warpToReal;                     // Are we transitioning from TimeWarp to realtime?
        public bool engineHasAnyMassLessPropellants;
        public bool isMultiMode;

        public bool autoMaximizePersistentIsp;
        public bool useKerbalismInFlight;

        public int vesselChangedSoiCountdown = 10;
        public int missingPowerCountdown;
        public int fixedUpdateCount;
        public int moduleEnginesCount;

        public double[] fuelDemands = new double[0];

        // Propellant data
        public List<PersistentPropellant> currentPropellants = new List<PersistentPropellant>();
        public List<PersistentPropellant>[] propellantsList = new List<PersistentPropellant>[2];

        // Average density of propellants
        public double densityAverage;
        public double bufferSize;
        public double consumedPower;

        private readonly Queue<float> _propellantReqMetFactorQueue = new Queue<float>();
        private readonly Queue<float> _throttleQueue = new Queue<float>();
        private readonly Queue<float> _ispQueue = new Queue<float>();

        private Dictionary<string, double> _availablePartResources = new Dictionary<string, double>();
        private readonly Dictionary<string, double> _kerbalismResourceChangeRequest = new Dictionary<string, double>();
        private static Assembly _realFuelsAssembly;

        private List<PersistentEngine> _persistentEngines;

        //public const double Rad2Deg = 180 / Math.PI; // 57.295779513;

        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor) return;

            if (!string.IsNullOrWhiteSpace(throttleAnimationName))
                throttleAnimationState = SetUpAnimation(throttleAnimationName, part);

            _persistentEngines = vessel.FindPartModulesImplementing<PersistentEngine>();
        }

        public void VesselChangedSOI()
        {
            vesselChangedSoiCountdown = 10;
        }

        // Make "moduleEngine" and "moduleEngineFx" fields refer to the ModuleEngines and ModuleEnginesFX modules in part.Modules
        private void FindModuleEngines()
        {
            moduleEnginesCount = 0;

            foreach (var partModule in part.Modules)
            {
                if (partModule is MultiModeEngine multiMode)
                    multiModeEngine = multiMode;

                if (partModule is ModuleEnginesFX moduleEngineFx)
                {
                    powerEffectName = moduleEngineFx.powerEffectName;
                    ApplyEffect(powerEffectName, 0);
                    powerEffectNameList.Add(powerEffectName);
                    moduleEngineFx.powerEffectName = string.Empty;

                    runningEffectName = moduleEngineFx.runningEffectName;
                    ApplyEffect(runningEffectName, 0);
                    runningEffectNameList.Add(runningEffectName);
                    moduleEngineFx.runningEffectName = string.Empty;
                }

                if (partModule is ModuleEngines engine)
                {
                    moduleEngine = engine;
                    engines.Add(moduleEngine);
                    moduleEnginesCount++;
                }
            }

            if (moduleEnginesCount == 0)
            {
                Debug.LogError("[PersistentThrust]: found no compatible engines, disabling PersistentThrust");
                isPersistentEngine = false;
            }
            else if (moduleEnginesCount == 1 && multiModeEngine != null)
            {
                Debug.LogWarning("[PersistentThrust]: Insufficient engines found for MultiMode, using single engine mode PersistentThrust");
                isPersistentEngine = false;
            }
            else if (moduleEnginesCount > 1 && multiModeEngine != null)
            {
                Debug.LogWarning("[PersistentThrust]: found multiple engines  but no MultiMode PartModule, disabling PersistentThrust");
                isPersistentEngine = false;
            }
            else if (multiModeEngine != null && moduleEnginesCount == 2)
            {
                Debug.Log("[PersistentThrust]: enabled MultiMode");
                isPersistentEngine = true;
                isMultiMode = true;
            }
            else
            {
                Debug.Log("[PersistentThrust]: enabled");
                isPersistentEngine = true;
            }
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            SetAnimationRatio(0, throttleAnimationState);
            UpdateFX(0);
        }

        // Finds the active moduleEngine module from the MultiModeEngine partModule
        private void FetchActiveMode()
        {
            if (!isMultiMode)
                return;

            moduleEngine = multiModeEngine.runningPrimary ? multiModeEngine.PrimaryEngine : multiModeEngine.SecondaryEngine;

            if (!isMultiMode) return;

            var index = multiModeEngine.runningPrimary ? 0 : 1;

            for (var i = 0 ; i < powerEffectNameList.Count ; i++)
            {
                var effect = powerEffectNameList[i];

                if (i == index)
                    powerEffectName = powerEffectNameList[i];
                else
                    ApplyEffect(effect, 0);
            }

            for (var i = 0; i < runningEffectNameList.Count; i++)
            {
                var effect = runningEffectNameList[i];

                if (i == index)
                    runningEffectName = runningEffectNameList[i];
                else
                    ApplyEffect(effect, 0);
            }

            // select active propellant list
            currentPropellants = propellantsList[index];

            // Initialize density of propellant used in deltaV and mass calculations
            densityAverage = currentPropellants.AverageDensity();
        }

        private void ApplyEffect(string effect, float ratio)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            part.Effect(effect, ratio, -1);
        }

        // Update is called during refresh frame, which can be less frequent than FixedUpdate which is called every processing frame
        public override void OnUpdate()
        {
            if (moduleEngine == null) return;

            // hide stock thrust
            moduleEngine.Fields["finalThrust"].guiActive = false;
            moduleEngine.Fields["realIsp"].guiActive = false;
            moduleEngine.Fields["propellantReqMet"].guiActive = false;

            propellantReqMet = propellantReqMetFactor * 100;
            realIsp = !vessel.packed && !moduleEngine.propellants.Any(m => m.resourceDef.density == 0)
                ? moduleEngine.realIsp 
                : (vessel.packed && (MaximizePersistentIsp || autoMaximizePersistentIsp)) || throttlePersistent == 0 
                        ? ispPersistent 
                        : ispPersistent * propellantReqMetFactor;

            if (!isPersistentEngine || !HasPersistentThrust) return;

            // When transitioning from TimeWarp to real update throttle
            if (warpToReal)
            {
                SetThrottle(throttlePersistent, true);
                warpToReal = false;
            }

            if (vessel.packed)
            {
                // maintain thrust setting during TimeWarp
                vessel.ctrlState.mainThrottle = throttlePersistent;

                // stop engines when X pressed
                if (Input.GetKeyDown(KeyCode.X))
                    SetThrottle(0, returnToRealtimeAfterKeyPressed);
                // full throttle when Z pressed
                else if (Input.GetKeyDown(KeyCode.Z))
                    SetThrottle(1, returnToRealtimeAfterKeyPressed);
                // increase throttle when Shift pressed
                else if (Input.GetKeyDown(KeyCode.LeftShift))
                    SetThrottle(Mathf.Min(1, throttlePersistent + 0.01f), returnToRealtimeAfterKeyPressed);
                // decrease throttle when Ctrl pressed
                else if (Input.GetKeyDown(KeyCode.LeftControl))
                    SetThrottle(Mathf.Max(0, throttlePersistent - 0.01f), returnToRealtimeAfterKeyPressed);
            }
            else
                TimeWarp.GThreshold = 12;
        }

        private void SetThrottle(float newSetting, bool returnToRealTime)
        {
            vessel.ctrlState.mainThrottle = newSetting;
            moduleEngine.requestedThrottle = newSetting;
            moduleEngine.currentThrottle = newSetting;

            if (!returnToRealTime) return;

            // Return to realtime
            TimeWarp.SetRate(0, true);

            if (_realFuelsAssembly == null || !(newSetting > 0)) return;

            var ignitedInfo = moduleEngine.GetType().GetField("ignited", BindingFlags.NonPublic | BindingFlags.Instance);

            if (ignitedInfo == null)
                return;

            ignitedInfo.SetValue(moduleEngine, true);
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

            // Populate moduleEngine and moduleEngineFx fields
            FindModuleEngines();

            // Initialize PersistentPropellant list
            if (isMultiMode)
            {
                propellantsList[0] = PersistentPropellant.MakeList(engines[0].propellants);
                propellantsList[1] = PersistentPropellant.MakeList(engines[1].propellants);
            }
            else
            {
                propellantsList[0] = PersistentPropellant.MakeList(moduleEngine.propellants);
                propellantsList[1] = new List<PersistentPropellant>();
            }

            // select active propellant list
            currentPropellants = propellantsList[0];

            // Initialize density of propellant used in deltaV and mass calculations
            densityAverage = currentPropellants.AverageDensity();
        }

        private void ReloadPropellantsWithoutMasslessPropellants(IEnumerable<PersistentPropellant> pplist)
        {
            var akPropellants = new ConfigNode();

            //Get the Ignition state, i.e. is the moduleEngine shutdown or activated
            var ignitionState = moduleEngine.getIgnitionState;

            moduleEngine.Shutdown();

            foreach (var propellant in pplist)
            {
                if (propellant.density == 0)
                    continue;

                var propellantConfig = LoadPropellant(propellant.propellant.name, propellant.propellant.ratio);
                akPropellants.AddNode(propellantConfig);
            }

            moduleEngine.Load(akPropellants);

            if (ignitionState)
                moduleEngine.Activate();
        }

        private ConfigNode LoadPropellant(string akName, float akRatio)
        {
            Debug.Log("[PersistentThrust]: LoadPropellant: " + akName + " " + akRatio);

            var propellantNode = new ConfigNode().AddNode("PROPELLANT");
            propellantNode.AddValue("name", akName);
            propellantNode.AddValue("ratio", akRatio);
            propellantNode.AddValue("DrawGauge", true);

            return propellantNode;
        }

        private void UpdatePersistentParameters()
        {
            _throttleQueue.Enqueue(vessel.ctrlState.mainThrottle);
            if (_throttleQueue.Count > queueLength)
                _throttleQueue.Dequeue();
            throttlePersistent = _throttleQueue.Max();

            _ispQueue.Enqueue(moduleEngine.realIsp);
            if (_ispQueue.Count > queueLength)
                _ispQueue.Dequeue();
            ispPersistent = _ispQueue.Max();
        }

        // Calculate demands of each resource
        public virtual double[] CalculateDemands(double mass)
        {
            var demands = new double[currentPropellants.Count];
            if (!(mass > 0)) return demands;

            // Per propellant demand
            for (var i = 0; i < currentPropellants.Count; i++)
            {
                demands[i] = currentPropellants[i].CalculateDemand(mass);
            }
            return demands;
        }

        public void UpdateBuffers(double[] demands)
        {
            for (var i = 0; i < currentPropellants.Count; i++)
            {
                var currentPropellant = currentPropellants[i];

                if (currentPropellant.density != 0 || i >= demands.Count()) continue;

                // find initial resource amount for propellant
                var availablePropellant = LoadPropellantAvailability(currentPropellant);

                // update power buffer
                bufferSize = UpdateBuffer(availablePropellant, demands[i]);

                // update request
                RequestResource(currentPropellant, 0);
            }
        }

        private PersistentPropellant LoadPropellantAvailability(PersistentPropellant pp)
        {
            var activePropellant = pp;
            var firstProcessedEngine = _persistentEngines.FirstOrDefault(m => m.currentPropellants.Any(l => l.missionTime == vessel.missionTime && l.definition.id == pp.definition.id));
            if (firstProcessedEngine == null)
            {
                // store mission time to prevent other engines doing unnecessary work
                pp.missionTime = vessel.missionTime;
                // determine amount and maxAmount at start of PersistentEngine testing
                part.GetConnectedResourceTotals(pp.definition.id, pp.propellant.GetFlowMode(), out pp.amount, out pp.maxAmount, true);
                // calculate total demand on operational engines
                pp.totalEnginesDemand = _persistentEngines
                    .Where(e => e.moduleEngine.getIgnitionState)
                    .Sum(m => m.currentPropellants
                        .Where(l => l.definition.id == pp.definition.id)
                        .Sum(l => l.normalizedDemand));
            }
            else
                activePropellant = firstProcessedEngine.currentPropellants.First(m => m.definition.id == pp.definition.id);

            return activePropellant;
        }

        // Apply demanded resources & return results
        // Updated depleted boolean flag if resource request failed
        public virtual double[] ApplyDemands(double[] demands, ref float finalPropellantReqMetFactor)
        {
            double overallPropellantReqMet = 1;

            autoMaximizePersistentIsp = false;

            var demandsOut = new double[currentPropellants.Count];

            // first do a simulation run to determine the propellant availability so we don't over consume
            for (var i = 0; i < currentPropellants.Count; i++)
            {
                var pp = currentPropellants[i];
                // Request resources if:
                // - resource has mass & request mass flag true
                // - resource massless & request massless flag true
                if ((!(pp.density > 0) || !requestPropMass) && (pp.density != 0 || !requestPropMassless)) continue;

                var demandIn = demands[i];
                var storageModifier = 1.0;

                if (pp.density == 0)
                {
                    // find initial resource amount for propellant
                    var availablePropellant = LoadPropellantAvailability(pp);

                    _availablePartResources.TryGetValue(pp.definition.name, out var kerbalismAmount);

                    var currentPropellantAmount = useKerbalismInFlight ? kerbalismAmount : availablePropellant.amount;

                    // update power buffer
                    bufferSize = UpdateBuffer(availablePropellant, demandIn);

                    var bufferedTotalEnginesDemand = Math.Min(availablePropellant.maxAmount, availablePropellant.totalEnginesDemand * bufferSizeMult);

                    if (bufferedTotalEnginesDemand > currentPropellantAmount && availablePropellant.totalEnginesDemand > 0)
                        storageModifier = Math.Min(1, (demandIn / availablePropellant.totalEnginesDemand) + currentPropellantAmount / bufferedTotalEnginesDemand * (demandIn / availablePropellant.totalEnginesDemand));

                    if (!MaximizePersistentPower && currentPropellantAmount < bufferSize)
                        storageModifier *= currentPropellantAmount / bufferSize;
                }

                var demandOut = IsInfinite(pp.propellant) ? demandIn : RequestResource(pp, demandIn * storageModifier, true);

                var propellantFoundRatio = demandOut >= demandIn ? 1 : demandIn > 0 ? demandOut / demandIn : 1;

                if (propellantFoundRatio < overallPropellantReqMet)
                    overallPropellantReqMet = propellantFoundRatio;

                if (pp.propellant.resourceDef.density > 0)
                {
                    // reset stabilize Queue when out of mass propellant
                    if (propellantFoundRatio < 1)
                        _propellantReqMetFactorQueue.Clear();
                }
                else if (propellantFoundRatio == 0)
                {
                    // reset stabilize Queue when out power for too long
                    if (missingPowerCountdown <= 0)
                        _propellantReqMetFactorQueue.Clear();
                    missingPowerCountdown--;
                }
                else
                    missingPowerCountdown = missingPowerCountdownSize;
            }

            // attempt to stabilize thrust output with First In Last Out Queue 
            _propellantReqMetFactorQueue.Enqueue((float)overallPropellantReqMet);
            if (_propellantReqMetFactorQueue.Count() > propellantReqMetFactorQueueSize)
                _propellantReqMetFactorQueue.Dequeue();
            var averagePropellantReqMetFactor = _propellantReqMetFactorQueue.Average();

            if (averagePropellantReqMetFactor < minimumPropellantReqMetFactor)
                autoMaximizePersistentIsp = true;

            finalPropellantReqMetFactor = !vessel.packed || MaximizePersistentIsp || autoMaximizePersistentIsp ? averagePropellantReqMetFactor : Mathf.Pow(averagePropellantReqMetFactor, fudgeExponent);

            // secondly we can consume the resource based on propellant availability
            for (var i = 0; i < currentPropellants.Count; i++)
            {
                var pp = currentPropellants[i];
                // Request resources if:
                // - resource has mass & request mass flag true
                // - resource massless & request massless flag true
                if ((pp.density > 0 && requestPropMass) || (pp.density == 0 && requestPropMassless))
                {
                    var demandIn = pp.density > 0 
                        ? ((MaximizePersistentIsp || autoMaximizePersistentIsp) ? averagePropellantReqMetFactor * demands[i] :  demands[i]) 
                        : overallPropellantReqMet * demands[i];

                    if (pp.density == 0)
                        consumedPower = demandIn;

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
            var updateAmount = Math.Max(0, currentAmount - demand);

            if (simulate)
                _availablePartResources[propellant.definition.name] = updateAmount;
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

            dynamicBufferSize = useDynamicBuffer ? requiredBufferSize : 0;
            if (dynamicBufferSize > 0)
            {
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
            }

            previousFixedDeltaTime = TimeWarp.fixedDeltaTime;

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
        public virtual Vector3d CalculateDeltaVVector(double vesselMass, double deltaTime, double thrust, float isp, Vector3d thrustVector, out double mass)
        {
            // Mass flow rate
            var massFlowRate = isp > 0 ? thrust / (isp * PhysicsGlobals.GravitationalAcceleration) : 0;
            // Change in mass over time interval dT
            var deltaMass = massFlowRate * deltaTime;
            // Resource demand from propellants with mass
            mass = densityAverage > 0 ? deltaMass / densityAverage : 0;
            //// Resource demand from propellants with mass
            var remainingMass = vesselMass - deltaMass;
            // deltaV amount
            var deltaV = isp * PhysicsGlobals.GravitationalAcceleration * Math.Log(remainingMass > 0 ? vesselMass / remainingMass : 1);
            // Return deltaV vector
            return deltaV * thrustVector;
        }

        public virtual void UpdateFX(double currentThrust)
        {
            if (!moduleEngine.getIgnitionState)
                currentThrust = 0;

            var exhaustRatio = (float)(moduleEngine.maxThrust > 0 ? currentThrust / moduleEngine.maxThrust : 0);

            ApplyEffect(powerEffectName, exhaustRatio);
            ApplyEffect(runningEffectName, exhaustRatio);
        }

        private void UpdatePropellantReqMetFactorQueue()
        {
            if (propellantReqMetFactor > 0 && throttlePersistent > 0)
            {
                _propellantReqMetFactorQueue.Enqueue(engineHasAnyMassLessPropellants
                    ? Mathf.Pow(propellantReqMetFactor, 1 / fudgeExponent)
                    : propellantReqMetFactor);

                if (_propellantReqMetFactorQueue.Count > propellantReqMetFactorQueueSize)
                    _propellantReqMetFactorQueue.Dequeue();
            }
            else
                _propellantReqMetFactorQueue.Clear();
        }

        // Physics update
        public void FixedUpdate() // FixedUpdate is also called while not staged
        {
            if (this.vessel is null || moduleEngine is null || !isEnabled) return;

            fixedUpdateCount++;
            var universalTime = Planetarium.GetUniversalTime();

            // restore heading at load
            if (HasPersistentHeadingEnabled && fixedUpdateCount <= 60 && vesselAlignmentWithAutopilotMode > 0.995)
            {
                vessel.Autopilot.SetMode(persistentAutopilotMode);
                moduleEngine.PersistHeading(TimeWarp.fixedDeltaTime, headingTolerance, vesselChangedSoiCountdown > 0);
            }
            else
                persistentAutopilotMode = vessel.Autopilot.Mode;

            //vesselHeadingVersusManeuver = vessel.VesselOrbitHeadingVersusManeuverVector();
            //vesselHeadingVersusManeuverInDegrees = Math.Acos(Math.Max(-1, Math.Min(1, vesselHeadingVersusManeuver))) * Rad2Deg;

            _kerbalismResourceChangeRequest.Clear();

            if (vesselChangedSoiCountdown > 0)
                vesselChangedSoiCountdown--;

            // Realtime mode
            if (!vessel.packed)
            {
                // Checks if moduleEngine mode wasn't switched
                FetchActiveMode();

                // Update persistent thrust parameters if NOT transitioning from warp to realtime
                if (!warpToReal)
                    UpdatePersistentParameters();

                vesselAlignmentWithAutopilotMode = moduleEngine.VesselHeadingVersusAutopilotVector(universalTime);

                engineHasAnyMassLessPropellants = moduleEngine.propellants.Any(m => m.resourceDef.density == 0);

                if (processMasslessSeparately && engineHasAnyMassLessPropellants)
                    ReloadPropellantsWithoutMasslessPropellants(currentPropellants);

                //if (vesselHeadingVersusManeuverInDegrees > maneuverTolerance)
                //{
                //    moduleEngine.maxFuelFlow = 1e-10f;
                //    finalThrust = 0;
                //}
                //else 
                if (!engineHasAnyMassLessPropellants && moduleEngine.propellantReqMet > 0)
                {
                    // Mass flow rate
                    var massFlowRate = ispPersistent > 0 ?  moduleEngine.currentThrottle * moduleEngine.maxThrust / (ispPersistent * PhysicsGlobals.GravitationalAcceleration): 0;
                    // Change in mass over time interval dT
                    var deltaMass = massFlowRate * TimeWarp.fixedDeltaTime;
                    // Resource demand from propellants with mass
                    demandMass = densityAverage > 0 ? deltaMass / densityAverage : 0;

                    // Calculate resource demands
                    fuelDemands = CalculateDemands(demandMass);
                    // Apply resource demands & test for resource depletion
                    ApplyDemands(fuelDemands, ref propellantReqMetFactor);

                    // calculate maximum flow
                    var maxFuelFlow = ispPersistent > 0 ? moduleEngine.maxThrust / (ispPersistent * PhysicsGlobals.GravitationalAcceleration) : 0;
                    
                    // adjust fuel flow 
                    moduleEngine.maxFuelFlow = maxFuelFlow > 0 && propellantReqMetFactor > 0 ? (float)(maxFuelFlow * propellantReqMetFactor) : 1e-10f;

                    // update displayed thrust and fx
                    finalThrust = moduleEngine.currentThrottle * moduleEngine.maxThrust * Math.Min(propellantReqMetFactor, moduleEngine.propellantReqMet * 0.01f);
                }
                else
                {
                    // restore maximum flow
                    RestoreMaxFuelFlow();

                    propellantReqMetFactor = moduleEngine.propellantReqMet * 0.01f;

                    finalThrust = moduleEngine.GetCurrentThrust();
                }

                UpdatePropellantReqMetFactorQueue();

                UpdateFX(finalThrust);

                SetThrottleAnimation(finalThrust);

                thrustTxt = Utils.FormatThrust(finalThrust);

                UpdateBuffers(fuelDemands);
            }
            else
            {
                // restore maximum flow
                RestoreMaxFuelFlow();

                if (throttlePersistent > 0 && ispPersistent > 0 && isPersistentEngine && HasPersistentThrust)
                {
                    if(TimeWarp.CurrentRateIndex == 0)
                        warpToReal = true; // Set to true for transition to realtime

                    if (vessel.IsControllable && HasPersistentHeadingEnabled)
                    {
                        vesselAlignmentWithAutopilotMode = moduleEngine.PersistHeading(TimeWarp.fixedDeltaTime, headingTolerance, vesselChangedSoiCountdown > 0, vesselAlignmentWithAutopilotMode == 1);
                        if (vesselAlignmentWithAutopilotMode != 1)
                        {
                            finalThrust = 0;
                            return;
                        }
                    }

                    // Calculated requested thrust
                    //var requestedThrust = vesselHeadingVersusManeuverInDegrees <= maneuverTolerance ? moduleEngine.thrustPercentage * 0.01f * throttlePersistent * moduleEngine.maxThrust : 0;
                    var requestedThrust = moduleEngine.thrustPercentage * 0.01f * throttlePersistent * moduleEngine.maxThrust;

                    var thrustVector = part.transform.up; // Thrust direction unit vector
                    // Calculate deltaV vector & resource demand from propellants with mass
                    var deltaVVector = CalculateDeltaVVector(vessel.totalMass, TimeWarp.fixedDeltaTime, requestedThrust, ispPersistent, thrustVector, out demandMass);
                    // Calculate resource demands
                    fuelDemands = CalculateDemands(demandMass);
                    // Apply resource demands & test for resource depletion
                    ApplyDemands(fuelDemands, ref propellantReqMetFactor);

                    // Apply deltaV vector at UT & dT to orbit if resources not depleted
                    if (propellantReqMetFactor > 0)
                    {
                        finalThrust = requestedThrust * propellantReqMetFactor;
                        vessel.orbit.Perturb(deltaVVector * propellantReqMetFactor, universalTime);
                    }

                    // Otherwise log warning and drop out of timewarp if throttle on & depleted
                    else if (throttlePersistent > 0)
                    {
                        finalThrust = 0;
                        Debug.Log("[PersistentThrust]: Thrust warp stopped - propellant depleted");
                        ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_PT_StoppedDepleted"), 5.0f, ScreenMessageStyle.UPPER_CENTER);
                        // Return to realtime
                        TimeWarp.SetRate(0, true);
                        if (!vessel.IsControllable)
                        {
                            throttlePersistent = 0;
                            vessel.ctrlState.mainThrottle = 0;
                        }
                    }
                    else
                        finalThrust = 0;

                    SetThrottleAnimation(finalThrust);
                    UpdateFX(finalThrust);
                    thrustTxt = Utils.FormatThrust(finalThrust);
                }
                else
                {
                    SetThrottleAnimation(0);

                    finalThrust = 0;
                    if (vessel.IsControllable && HasPersistentHeadingEnabled)
                        vesselAlignmentWithAutopilotMode = moduleEngine.PersistHeading(TimeWarp.fixedDeltaTime, headingTolerance, vesselChangedSoiCountdown > 0);
                    UpdateFX(0);

                    UpdateBuffers(fuelDemands);
                }
            }
        }

        private void RestoreMaxFuelFlow()
        {
            moduleEngine.maxFuelFlow = (float) (ispPersistent > 0 ? moduleEngine.maxThrust / (ispPersistent * PhysicsGlobals.GravitationalAcceleration) : 1e-10f);
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

        private void SetThrottleAnimation(float thrust)
        {
            SetAnimationRatio(moduleEngine.maxThrust > 0 ? thrust / moduleEngine.maxThrust : 0, throttleAnimationState);
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
