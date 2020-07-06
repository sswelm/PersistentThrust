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

        public string[] powerEffectNameList = {"", ""};
        public string[] runningEffectNameList = { "", ""};

        public double demandMass;
        public double dynamicBufferSize;

        // Engine module on the same part
        public ModuleEngines[] engines = new ModuleEngines[2];
        public ModuleEngines moduleEngine;
        public ModuleEnginesFX moduleEngineFx;
        public AnimationState[] throttleAnimationState;
        public MultiModeEngine multiMode;

        public float throttlePersistent;                // Persistent values to use during timewarp
        public float ispPersistent;

        public float finalThrust;
        public float propellantReqMetFactor;
        public float previousFixedDeltaTime;

        public bool isPersistentEngine;             // Flag if using PersistentEngine features        
        public bool warpToReal;                     // Are we transitioning from timewarp to reatime?
        public bool engineHasAnyMassLessPropellants;
        public bool isMultiMode;

        public bool autoMaximizePersistentIsp;
        public bool useKerbalismInFlight;

        public int vesselChangedSoiCountdown = 10;
        public int missingPowerCountdown;
        public int fixedUpdateCount;

        public double[] fuelDemands = new double[0];

        // Propellant data
        public List<PersistentPropellant> currentPropellants = new List<PersistentPropellant>();
        public List<PersistentPropellant>[] propellantsList = new List<PersistentPropellant>[2];

        // Average density of propellants
        public double densityAverage;
        public double bufferSize;
        public double consumedPower;

        private readonly Queue<float> propellantReqMetFactorQueue = new Queue<float>();
        private readonly Queue<float> throttleQueue = new Queue<float>();
        private readonly Queue<float> ispQueue = new Queue<float>();

        private Dictionary<string, double> availablePartResources = new Dictionary<string, double>();
        private readonly Dictionary<string, double> kerbalismResourceChangeRequest = new Dictionary<string, double>();
        private static Assembly RealFuelsAssembly = null;

        private List<PersistentEngine> persistentEngines;

        public const double Rad2Deg = 180 / Math.PI; // 57.295779513;

        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor) return;

            if (!string.IsNullOrEmpty(throttleAnimationName))
            {
                throttleAnimationState = SetUpAnimation(throttleAnimationName, part);
            }

            persistentEngines = vessel.FindPartModulesImplementing<PersistentEngine>();
        }

        public void VesselChangedSOI()
        {
            vesselChangedSoiCountdown = 10;
        }

        // Make "moduleEngine" and "moduleEngineFx" fields refer to the ModuleEngines and ModuleEnginesFX modules in part.Modules
        void FindModuleEngines()
        {
            var moduleEnginesFXCount = 0;

            foreach (var pm in part.Modules)
            {
                if (pm is ModuleEngines)
                {
                    moduleEngine = pm as ModuleEngines;
                    engines[moduleEnginesFXCount] = moduleEngine;
                    isPersistentEngine = true;
                }

                if (pm is MultiModeEngine)
                {
                    multiMode = pm as MultiModeEngine;

                    isMultiMode = true;
                }

                if (pm is ModuleEnginesFX)
                {
                    moduleEngineFx = pm as ModuleEnginesFX;
                    engines[moduleEnginesFXCount] = moduleEngineFx;
                    isPersistentEngine = true;

                    if (!string.IsNullOrEmpty(moduleEngineFx.powerEffectName))
                    {
                        powerEffectName = moduleEngineFx.powerEffectName;
                        part.Effect(powerEffectName, 0);
                        powerEffectNameList[moduleEnginesFXCount] = moduleEngineFx.powerEffectName;
                        moduleEngineFx.powerEffectName = "";
                    }

                    if (!string.IsNullOrEmpty(moduleEngineFx.runningEffectName))
                    {
                        runningEffectName = moduleEngineFx.runningEffectName;
                        part.Effect(runningEffectName, 0);
                        runningEffectNameList[moduleEnginesFXCount] = moduleEngineFx.runningEffectName;
                        moduleEngineFx.runningEffectName = "";
                    }

                    moduleEnginesFXCount++;
                }
            }

            if (!isPersistentEngine)
            {
                Debug.Log("[PersistentThrust] No ModuleEngine found.");
            }
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            SetAnimationRatio(0, throttleAnimationState);
            UpdateFX(0);
        }

        // Finds the active moduleEngine module from the MultiModeEngine partmodule
        private void FetchActiveMode()
        {
            if (!isMultiMode)
                return;

            moduleEngine = multiMode.runningPrimary ? multiMode.PrimaryEngine : multiMode.SecondaryEngine;
            moduleEngineFx = multiMode.runningPrimary ? multiMode.PrimaryEngine : multiMode.SecondaryEngine;

            if (!isMultiMode) return;

            var index = multiMode.runningPrimary ? 0 : 1;

            for (int i = 0 ; i < powerEffectNameList.Length ; i++)
            {
                var effect = powerEffectNameList[i];

                if (i == index)
                    powerEffectName = powerEffectNameList[i];
                else
                    ApplyEffect(effect, 0);
            }

            for (int i = 0; i < runningEffectNameList.Length; i++)
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
            if (string.IsNullOrEmpty(name))
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

            // When transitioning from timewarp to real update throttle
            if (warpToReal)
            {
                SetThrottle(throttlePersistent, true);
                warpToReal = false;
            }

            if (vessel.packed)
            {
                // maintain thrust setting durring timewarp
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
                TimeWarp.GThreshold = 12f;
        }

        private void SetThrottle(float newSetting, bool returnToRealTime)
        {
            vessel.ctrlState.mainThrottle = newSetting;
            moduleEngine.requestedThrottle = newSetting;
            moduleEngine.currentThrottle = newSetting;

            if (!returnToRealTime) return;

            // Return to realtime
            TimeWarp.SetRate(0, true);

            if (RealFuelsAssembly == null || !(newSetting > 0)) return;

            FieldInfo ignitedInfo = moduleEngine.GetType().GetField("ignited", BindingFlags.NonPublic | BindingFlags.Instance);

            if (ignitedInfo == null)
                return;

            ignitedInfo.SetValue(moduleEngine, true);
        }

        // Check if RealFuels is installed 
        public override void OnAwake()
        {
            base.OnAwake();
            if (HighLogic.LoadedScene != GameScenes.LOADING && RealFuelsAssembly == null)
                RealFuelsAssembly = AssemblyLoader.loadedAssemblies.FirstOrDefault(x => x.name.StartsWith("RealFuels"))?.assembly;
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

        private void ReloadPropellantsWithoutMasslessPropellants(List<PersistentPropellant> pplist)
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
            Debug.Log("[PersistenThrust]: LoadPropellant: " + akName + " " + akRatio);

            var propellantNode = new ConfigNode().AddNode("PROPELLANT");
            propellantNode.AddValue("name", akName);
            propellantNode.AddValue("ratio", akRatio);
            propellantNode.AddValue("DrawGauge", true);

            return propellantNode;
        }

        void UpdatePersistentParameters()
        {
            throttleQueue.Enqueue(vessel.ctrlState.mainThrottle);
            if (throttleQueue.Count > queueLength)
                throttleQueue.Dequeue();
            throttlePersistent = throttleQueue.Max();

            ispQueue.Enqueue(moduleEngine.realIsp);
            if (ispQueue.Count > queueLength)
                ispQueue.Dequeue();
            ispPersistent = ispQueue.Max();
        }

        // Calculate demands of each resource
        public virtual double[] CalculateDemands(double demandMass)
        {
            var demands = new double[currentPropellants.Count];
            if (!(demandMass > 0)) return demands;

            // Per propellant demand
            for (var i = 0; i < currentPropellants.Count; i++)
            {
                demands[i] = currentPropellants[i].CalculateDemand(demandMass);
            }
            return demands;
        }

        public void UpdateBuffers(double[] demands)
        {
            for (var i = 0; i < currentPropellants.Count; i++)
            {
                var pp = currentPropellants[i];

                if (pp.density == 0 && i < demands.Count())
                {
                    // find initial resource amount for propellant
                    var availablePropellant = LoadPropellantAvailability(pp);

                    // update power buffer
                    bufferSize = UpdateBuffer(availablePropellant, demands[i]);

                    // update request
                    RequestResource(pp, 0);
                }
            }
        }

        private PersistentPropellant LoadPropellantAvailability(PersistentPropellant pp)
        {
            var activePropellant = pp;
            var firstProcessedEngine = persistentEngines.FirstOrDefault(m => m.currentPropellants.Any(l => l.missionTime == vessel.missionTime && l.definition.id == pp.definition.id));
            if (firstProcessedEngine == null)
            {
                // store mission time to prevent other engines doing unnesisary work
                pp.missionTime = vessel.missionTime;
                // determine amount and maxamount at start of PersistenEngine testing
                part.GetConnectedResourceTotals(pp.definition.id, pp.propellant.GetFlowMode(), out pp.amount, out pp.maxamount, true);
                // calculate total demand on operational engines
                pp.totalEnginesDemand = persistentEngines
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

                    availablePartResources.TryGetValue(pp.definition.name, out var kerbalismAmount);

                    var currentPropellantAmount = useKerbalismInFlight ? kerbalismAmount : availablePropellant.amount;

                    // update power buffer
                    bufferSize = UpdateBuffer(availablePropellant, demandIn);

                    var bufferedTotalEnginesDemand = Math.Min(availablePropellant.maxamount, availablePropellant.totalEnginesDemand * bufferSizeMult);

                    if (bufferedTotalEnginesDemand > currentPropellantAmount && availablePropellant.totalEnginesDemand > 0)
                        storageModifier = Math.Min(1, (demandIn / availablePropellant.totalEnginesDemand) + ((currentPropellantAmount / bufferedTotalEnginesDemand) * (demandIn / availablePropellant.totalEnginesDemand)));

                    if (!MaximizePersistentPower && currentPropellantAmount < bufferSize)
                        storageModifier *= currentPropellantAmount / bufferSize;
                }

                var demandOut = IsInfinite(pp.propellant) ? demandIn : RequestResource(pp, demandIn * storageModifier, true);

                var propellantFoundRatio = demandOut >= demandIn ? 1 : demandIn > 0 ? demandOut / demandIn : 0;

                if (propellantFoundRatio < overallPropellantReqMet)
                    overallPropellantReqMet = propellantFoundRatio;

                if (pp.propellant.resourceDef.density > 0)
                {
                    // reset stabilize Queue when out of mass propellant
                    if (propellantFoundRatio < 1)
                        propellantReqMetFactorQueue.Clear();
                }
                else if (propellantFoundRatio == 0)
                {
                    // reset stabilize Queue when out power for too long
                    if (missingPowerCountdown <= 0)
                        propellantReqMetFactorQueue.Clear();
                    missingPowerCountdown--;
                }
                else
                    missingPowerCountdown = missingPowerCountdownSize;
            }

            // attempt to stabilize thrust output with First In Last Out Queue 
            propellantReqMetFactorQueue.Enqueue((float)overallPropellantReqMet);
            if (propellantReqMetFactorQueue.Count() > propellantReqMetFactorQueueSize)
                propellantReqMetFactorQueue.Dequeue();
            var averagePropellantReqMetFactor = propellantReqMetFactorQueue.Average();

            if (averagePropellantReqMetFactor < minimumPropellantReqMetFactor)
                autoMaximizePersistentIsp = true;

            finalPropellantReqMetFactor = (!vessel.packed || MaximizePersistentIsp || autoMaximizePersistentIsp) ? averagePropellantReqMetFactor : Mathf.Pow(averagePropellantReqMetFactor, fudgeExponent);

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

            if (useKerbalismInFlight)
            {
                availablePartResources.TryGetValue(propellant.definition.name, out var currentAmount);

                var available = Math.Min(currentAmount, demand);
                var updateAmount = Math.Max(0, currentAmount - demand);

                if (simulate)
                    availablePartResources[propellant.definition.name] = updateAmount;
                else
                {
                    var demandPerSecond = demand / TimeWarp.fixedDeltaTime;

                    kerbalismResourceChangeRequest.TryGetValue(propellant.definition.name, out var currentDemand);
                    kerbalismResourceChangeRequest[propellant.definition.name] = currentDemand - demandPerSecond;
                }

                return available;
            }
            else
                return part.RequestResource(propellant.definition.id, demand, propellant.propellant.GetFlowMode(), simulate);
        }

        public double UpdateBuffer(PersistentPropellant propellant, double baseSize)
        {
            var requiredBufferSize = useDynamicBuffer ? Math.Max(baseSize / TimeWarp.fixedDeltaTime * 10 * bufferSizeMult, baseSize * bufferSizeMult) : Math.Max(0, propellant.maxamount - baseSize);

            if (previousFixedDeltaTime == TimeWarp.fixedDeltaTime)
                return requiredBufferSize;

            var amountRatio = propellant.maxamount > 0 ? Math.Min(1, propellant.amount / propellant.maxamount) : 0;

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
        public virtual Vector3d CalculateDeltaVVector(double vesselMass, double deltaTime, double thrust, float isp, Vector3d thrustUV, out double demandMass)
        {
            // Mass flow rate
            var massFlowRate = isp > 0 ? thrust / (isp * PhysicsGlobals.GravitationalAcceleration) : 0;
            // Change in mass over time interval dT
            var deltaMass = massFlowRate * deltaTime;
            // Resource demand from propellants with mass
            demandMass = densityAverage > 0 ? deltaMass / densityAverage : 0;
            //// Resource demand from propellants with mass
            var remainingMass = vesselMass - deltaMass;
            // deltaV amount
            var deltaV = isp * PhysicsGlobals.GravitationalAcceleration * Math.Log(remainingMass > 0 ? vesselMass / remainingMass : 1);
            // Return deltaV vector
            return deltaV * thrustUV;
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
                propellantReqMetFactorQueue.Enqueue(engineHasAnyMassLessPropellants
                    ? Mathf.Pow(propellantReqMetFactor, 1 / fudgeExponent)
                    : propellantReqMetFactor);

                if (propellantReqMetFactorQueue.Count > propellantReqMetFactorQueueSize)
                    propellantReqMetFactorQueue.Dequeue();
            }
            else
                propellantReqMetFactorQueue.Clear();
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

            kerbalismResourceChangeRequest.Clear();

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

            availablePartResources = availableResources;

            resourceChangeRequest.Clear();

            foreach (var resourceRequest in kerbalismResourceChangeRequest)
            {
                resourceChangeRequest.Add(new KeyValuePair<string, double>(resourceRequest.Key, resourceRequest.Value));
            }

            return part.partInfo.title;
        }

        #endregion
    }
}
