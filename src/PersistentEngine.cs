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
        // GUI
        [KSPField(guiFormat = "F1", guiActive = true, guiName = "#autoLOC_6001378", guiUnits = "#autoLOC_7001400")]
        public float realIsp;
        [KSPField(guiFormat = "F6", guiActive = true, guiName = "#autoLOC_6001377")]
        public string thrust_d;
        [KSPField(guiFormat = "F2", guiActive = true, guiName = "#autoLOC_6001376", guiUnits = "%")]
        public float propellantReqMet;
        public float finalThrust;

        // Enable/disable persistent engine features
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_PersistentThrust"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool HasPersistentThrust = true;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_PersistentHeading"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool HasPersistentHeadingEnabled = true;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_MaximizePersistentIsp"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool MaximizePersistentIsp = true;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_MaximizePersistentPower"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool MaximizePersistentPower = false;

        // Config Settings
        [KSPField]
        public string throttleAnimationName;
        [KSPField]
        public bool returnToRealtimeAfterKeyPressed = true;
        [KSPField]
        public bool useDynamicBuffer = false;
        [KSPField]
        public bool processMasslessSeperately = true;
        [KSPField]
        public int queueLength = 2;
        [KSPField]
        public float fudgeExponent = 0.27f;
        [KSPField]
        public int missingPowerCountdownSize = 10;
        [KSPField]
        public int buffersizeMult = 50;
        [KSPField]
        public int propellantReqMetFactorQueueSize = 100;
        [KSPField]
        public double minimumPropellantReqMetFactor = 0.2;
        [KSPField]
        public float headingTolerance = 0.001f;
        [KSPField]
        public bool RequestPropMassless = true;             // Flag whether to request massless resources
        [KSPField]
        public bool RequestPropMass = true;                 // Flag whether to request resources with mass

        [KSPField(guiActive = true)]
        public string powerEffectName;
        [KSPField(guiActive = true)]
        public string runningEffectName;

        public string[] powerEffectNameList = {"", ""};
        public string[] runningEffectNameList = { "", ""};

        public double ratioHeadingVersusRequest;
        public double demandMass;
        public double dynamicBufferSize;

        // Engine module on the same part
        public ModuleEngines[] engines = new ModuleEngines[2];
        public ModuleEngines engine;
        public ModuleEnginesFX engineFX;
        public AnimationState[] throttleAnimationState;
        public MultiModeEngine multiMode;

        public float ThrottlePersistent;                // Persistent values to use during timewarp
        public float IspPersistent;

        public float propellantReqMetFactor;
        
        public bool IsPersistentEngine;             // Flag if using PersistentEngine features        
        public bool warpToReal;                     // Are we transitioning from timewarp to reatime?
        public bool engineHasAnyMassLessPropellants;
        public bool isMultiMode;

        public bool autoMaximizePersistentIsp;
        public bool useKerbalismInFlight;

        public int vesselChangedSOICountdown;
        public int missingPowerCountdown;

        public double[] fuelDemands = new double[0];

        // Propellant data
        public List<PersistentPropellant> pplist = new List<PersistentPropellant>();
        public List<PersistentPropellant>[] pplistList = new List<PersistentPropellant>[2];

        // Average density of propellants
        public double densityAverage;
        public double buffersize;

        private float previousfixedDeltaTime;

        public double consumedPower;

        private Queue<float> propellantReqMetFactorQueue = new Queue<float>();

        private Queue<float> throttleQueue = new Queue<float>();
        private Queue<float> ispQueue = new Queue<float>();

        private Dictionary<string, double> availableResources = new Dictionary<string, double>();
        private Dictionary<string, double> kerbalismResourceChangeRequest = new Dictionary<string, double>();
        private static Assembly RealFuelsAssembly = null;

        private List<PersistentEngine> persistentEngines;

        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor) return;

            if (!string.IsNullOrEmpty(throttleAnimationName))
            {
                throttleAnimationState = SetUpAnimation(throttleAnimationName, this.part);
            }

            persistentEngines = vessel.FindPartModulesImplementing<PersistentEngine>();
        }

        public void VesselChangedSOI()
        {
            vesselChangedSOICountdown = 10;
        }

        // Make "engine" and "engineFX" fields refer to the ModuleEngines and ModuleEnginesFX modules in part.Modules
        void FindModuleEngines()
        {
            var moduleEnginesFXCount = 0;

            foreach (var pm in part.Modules)
            {
                if (pm is ModuleEngines)
                {
                    engine = pm as ModuleEngines;
                    engines[moduleEnginesFXCount] = engine;
                    IsPersistentEngine = true;
                }

                if (pm is MultiModeEngine)
                {
                    multiMode = pm as MultiModeEngine;

                    isMultiMode = true;
                }

                if (pm is ModuleEnginesFX)
                {
                    engineFX = pm as ModuleEnginesFX;
                    engines[moduleEnginesFXCount] = engineFX;
                    IsPersistentEngine = true;

                    if (!string.IsNullOrEmpty(engineFX.powerEffectName))
                    {
                        powerEffectName = engineFX.powerEffectName;
                        part.Effect(powerEffectName, 0);
                        powerEffectNameList[moduleEnginesFXCount] = engineFX.powerEffectName;
                        engineFX.powerEffectName = "";
                    }

                    if (!string.IsNullOrEmpty(engineFX.runningEffectName))
                    {
                        runningEffectName = engineFX.runningEffectName;
                        part.Effect(runningEffectName, 0);
                        runningEffectNameList[moduleEnginesFXCount] = engineFX.runningEffectName;
                        engineFX.runningEffectName = "";
                    }

                    moduleEnginesFXCount++;
                }
            }

            if (!IsPersistentEngine)
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

        // Finds the active engine module from the MultiModeEngine partmodule
        private void FetchActiveMode()
        {
            if (!isMultiMode)
                return;

            engine = multiMode.runningPrimary ? multiMode.PrimaryEngine : multiMode.SecondaryEngine;
            engineFX = multiMode.runningPrimary ? multiMode.PrimaryEngine : multiMode.SecondaryEngine;

            if (isMultiMode)
            {
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
                pplist = pplistList[index];

                // Initialize density of propellant used in deltaV and mass calculations
                densityAverage = pplist.AverageDensity();
            }
        }

        private void ApplyEffect(string effect, float ratio)
        {
            if (string.IsNullOrEmpty(name))
                return;

            part.Effect(effect, ratio, -1);
        }

        // Update is called durring refresh frame, which can be less frequent than FixedUpdate which is called every processing frame
        public override void OnUpdate()
        {
            if (engine == null) return;

            // hide stock thrust
            engine.Fields["finalThrust"].guiActive = false;
            engine.Fields["realIsp"].guiActive = false;
            engine.Fields["propellantReqMet"].guiActive = false;

            propellantReqMet = propellantReqMetFactor * 100;
            realIsp = !vessel.packed && !engine.propellants.Any(m => m.resourceDef.density == 0)
                ? engine.realIsp 
                : (vessel.packed && (MaximizePersistentIsp || autoMaximizePersistentIsp)) || ThrottlePersistent == 0 
                        ? IspPersistent 
                        : IspPersistent * propellantReqMetFactor;

            if (!IsPersistentEngine || !HasPersistentThrust) return;

            // When transitioning from timewarp to real update throttle
            if (warpToReal)
            {
                SetThrottle(ThrottlePersistent, true);
                warpToReal = false;
            }

            if (vessel.packed)
            {
                // stop engines when X pressed
                if (Input.GetKeyDown(KeyCode.X))
                    SetThrottle(0, returnToRealtimeAfterKeyPressed);
                // full throtle when Z pressed
                else if (Input.GetKeyDown(KeyCode.Z))
                    SetThrottle(1, returnToRealtimeAfterKeyPressed);
                // increase throtle when Shift pressed
                else if (Input.GetKeyDown(KeyCode.LeftShift))
                    SetThrottle(Mathf.Min(1, ThrottlePersistent + 0.01f), returnToRealtimeAfterKeyPressed);
                // decrease throtle when Ctrl pressed
                else if (Input.GetKeyDown(KeyCode.LeftControl))
                    SetThrottle(Mathf.Max(0, ThrottlePersistent - 0.01f), returnToRealtimeAfterKeyPressed);
            }
            else
                TimeWarp.GThreshold = 12f;
        }

        private void SetThrottle(float newsetting, bool returnToRealTime)
        {
            vessel.ctrlState.mainThrottle = newsetting;
            engine.requestedThrottle = newsetting;
            engine.currentThrottle = newsetting;

            if (!returnToRealTime) return;

            // Return to realtime
            TimeWarp.SetRate(0, true);

            if (RealFuelsAssembly == null || !(newsetting > 0)) return;

            FieldInfo ignitedInfo = engine.GetType().GetField("ignited",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (ignitedInfo == null)
                return;

            ignitedInfo.SetValue(engine, true);
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

            // Populate engine and engineFX fields
            FindModuleEngines();

            // Initialize PersistentPropellant list
            if (isMultiMode)
            {
                pplistList[0] = PersistentPropellant.MakeList(engines[0].propellants);
                pplistList[1] = PersistentPropellant.MakeList(engines[1].propellants);
            }
            else
            {
                pplistList[0] = PersistentPropellant.MakeList(engine.propellants);
                pplistList[1] = new List<PersistentPropellant>();
            }

            // select active propellant list
            pplist = pplistList[0];

            // Initialize density of propellant used in deltaV and mass calculations
            densityAverage = pplist.AverageDensity();
        }

        private void ReloadPropellantsWithoutMasslessPropellants(List<PersistentPropellant> pplist)
        {
            var akPropellants = new ConfigNode();

            //Get the Ignition state, i.e. is the engine shutdown or activated
            var ignitionState = engine.getIgnitionState;

            engine.Shutdown();

            foreach (var propellant in pplist)
            {
                if (propellant.density == 0)
                    continue;

                var propellantConfig = LoadPropellant(propellant.propellant.name, propellant.propellant.ratio);
                akPropellants.AddNode(propellantConfig);
            }

            engine.Load(akPropellants);

            if (ignitionState)
                engine.Activate();
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
            ThrottlePersistent = throttleQueue.Max();

            ispQueue.Enqueue(engine.realIsp);
            if (ispQueue.Count > queueLength)
                ispQueue.Dequeue();
            IspPersistent = ispQueue.Max();
        }

        // Calculate demands of each resource
        public virtual double[] CalculateDemands(double demandMass)
        {
            var demands = new double[pplist.Count];
            if (!(demandMass > 0)) return demands;

            // Per propellant demand
            for (var i = 0; i < pplist.Count; i++)
            {
                demands[i] = pplist[i].CalculateDemand(demandMass);
            }
            return demands;
        }

        public void UpdateBuffers(double[] demands)
        {
            for (var i = 0; i < pplist.Count; i++)
            {
                var pp = pplist[i];

                if (pp.density == 0 && i < demands.Count())
                {
                    // find initial resource amount for propellant
                    var availablePropellant = LoadPropellantAvailability(pp);

                    // update power buffer
                    buffersize = UpdateBuffer(availablePropellant, demands[i]);

                    // update request
                    RequestResource(pp, 0);
                }
            }
        }

        private PersistentPropellant LoadPropellantAvailability(PersistentPropellant pp)
        {
            var activePropellant = pp;
            var firstProcessedEngine = persistentEngines.FirstOrDefault(m => m.pplist.Any(l => l.missionTime == vessel.missionTime && l.definition.id == pp.definition.id));
            if (firstProcessedEngine == null)
            {
                // store mission time to prevent other engines doing unnesisary work
                pp.missionTime = vessel.missionTime;
                // determine amount and maxamount at start of PersistenEngine testing
                part.GetConnectedResourceTotals(pp.definition.id, pp.propellant.GetFlowMode(), out pp.amount, out pp.maxamount, true);
                // calculate total demand on operational engines
                pp.totalEnginesDemand = persistentEngines
                    .Where(e => e.engine.getIgnitionState)
                    .Sum(m => m.pplist
                        .Where(l => l.definition.id == pp.definition.id)
                        .Sum(l => l.normalizedDemand));
            }
            else
                activePropellant = firstProcessedEngine.pplist.First(m => m.definition.id == pp.definition.id);

            return activePropellant;
        }

        // Apply demanded resources & return results
        // Updated depleted boolean flag if resource request failed
        public virtual double[] ApplyDemands(double[] demands, ref float finalPropellantReqMetFactor)
        {
            double overallPropellantReqMet = 1;

            autoMaximizePersistentIsp = false;

            var demandsOut = new double[pplist.Count];

            // first do a simulation run to determine the propellant availability so we don't over consume
            for (var i = 0; i < pplist.Count; i++)
            {
                var pp = pplist[i];
                // Request resources if:
                // - resource has mass & request mass flag true
                // - resource massless & request massless flag true
                if ((!(pp.density > 0) || !RequestPropMass) && (pp.density != 0 || !RequestPropMassless)) continue;

                var demandIn = demands[i];
                var storageModifier = 1.0;

                if (pp.density == 0)
                {
                    // find initial resource amount for propellant
                    var availablePropellant = LoadPropellantAvailability(pp);

                    availableResources.TryGetValue(pp.definition.name, out double kerbalismAmount);

                    var currentPropellantAmount = useKerbalismInFlight ? kerbalismAmount : availablePropellant.amount;

                    // update power buffer
                    buffersize = UpdateBuffer(availablePropellant, demandIn);

                    var bufferedTotalEnginesDemand = Math.Min(availablePropellant.maxamount, availablePropellant.totalEnginesDemand * buffersizeMult);

                    if (bufferedTotalEnginesDemand > currentPropellantAmount && availablePropellant.totalEnginesDemand > 0)
                        storageModifier = Math.Min(1, (demandIn / availablePropellant.totalEnginesDemand) + ((currentPropellantAmount / bufferedTotalEnginesDemand) * (demandIn / availablePropellant.totalEnginesDemand)));

                    if (!MaximizePersistentPower && currentPropellantAmount < buffersize)
                        storageModifier *= currentPropellantAmount / buffersize;
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
            for (var i = 0; i < pplist.Count; i++)
            {
                var pp = pplist[i];
                // Request resources if:
                // - resource has mass & request mass flag true
                // - resource massless & request massless flag true
                if ((pp.density > 0 && RequestPropMass) || (pp.density == 0 && RequestPropMassless))
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
                availableResources.TryGetValue(propellant.definition.name, out double currentAmount);

                double available = Math.Min(currentAmount, demand);
                double updateAmount = Math.Max(0, currentAmount - demand);

                if (simulate)
                    availableResources[propellant.definition.name] = updateAmount;
                else
                {
                    var demandPerSecond = demand / TimeWarp.fixedDeltaTime;

                    kerbalismResourceChangeRequest.TryGetValue(propellant.definition.name, out double currentDemand);
                    kerbalismResourceChangeRequest[propellant.definition.name] = currentDemand - demandPerSecond;
                }

                return available;
            }
            else
                return part.RequestResource(propellant.definition.id, demand, propellant.propellant.GetFlowMode(), simulate);
        }

        public double UpdateBuffer(PersistentPropellant propellant, double baseSize)
        {
            var requiredBufferSize = useDynamicBuffer ? Math.Max(baseSize / TimeWarp.fixedDeltaTime * 10 * buffersizeMult, baseSize * buffersizeMult) : Math.Max(0, propellant.maxamount - baseSize);

            if (previousfixedDeltaTime == TimeWarp.fixedDeltaTime)
                return requiredBufferSize;

            var amountRatio = propellant.maxamount > 0 ? Math.Min(1, propellant.amount / propellant.maxamount) : 0;

            dynamicBufferSize = useDynamicBuffer ? requiredBufferSize : 0;
            if (dynamicBufferSize > 0)
            {
                var partresource = part.Resources[propellant.definition.name];
                if (partresource == null)
                {
                    var node = new ConfigNode("RESOURCE");
                    node.AddValue("name", propellant.definition.name);
                    node.AddValue("maxAmount", 0);
                    node.AddValue("amount", 0);
                    part.AddResource(node);

                    partresource = part.Resources[propellant.definition.name];
                }

                partresource.maxAmount = dynamicBufferSize;
                partresource.amount = dynamicBufferSize * amountRatio;
            }

            previousfixedDeltaTime = TimeWarp.fixedDeltaTime;

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
        public virtual Vector3d CalculateDeltaVV(double vesselMass, double deltaTime, double thrust, float isp, Vector3d thrustUV, out double demandMass)
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
            if (!engine.getIgnitionState)
                currentThrust = 0;

            var exhaustRatio = (float)(engine.maxThrust > 0 ? currentThrust / engine.maxThrust : 0);

            ApplyEffect(powerEffectName, exhaustRatio);
            ApplyEffect(runningEffectName, exhaustRatio);
        }

        private void UpdatePropellantReqMetFactorQueue()
        {
            if (propellantReqMetFactor > 0 && ThrottlePersistent > 0)
            {
                if (engineHasAnyMassLessPropellants)
                    propellantReqMetFactorQueue.Enqueue(Mathf.Pow(propellantReqMetFactor, 1 / fudgeExponent));
                else
                    propellantReqMetFactorQueue.Enqueue(propellantReqMetFactor);

                if (propellantReqMetFactorQueue.Count > propellantReqMetFactorQueueSize)
                    propellantReqMetFactorQueue.Dequeue();
            }
            else
                propellantReqMetFactorQueue.Clear();
        }

        // Physics update
        public void FixedUpdate() // FixedUpdate is also called while not staged
        {
            if (this.vessel is null || engine is null || !isEnabled) return;

            kerbalismResourceChangeRequest.Clear();

            if (vesselChangedSOICountdown > 0)
                vesselChangedSOICountdown--;

            // Realtime mode
            if (!vessel.packed)
            {
                // Checks if engine mode wasn't switched
                FetchActiveMode();

                engineHasAnyMassLessPropellants = engine.propellants.Any(m => m.resourceDef.density == 0);

                if (processMasslessSeperately && engineHasAnyMassLessPropellants)
                    ReloadPropellantsWithoutMasslessPropellants(pplist);

                // Update persistent thrust parameters if NOT transitioning from warp to realtime
                if (!warpToReal)
                    UpdatePersistentParameters();

                ratioHeadingVersusRequest = 0;

                if (!engineHasAnyMassLessPropellants && engine.propellantReqMet > 0)
                {
                    // Mass flow rate
                    var massFlowRate = IspPersistent > 0 ?  (engine.currentThrottle * engine.maxThrust) / (IspPersistent * PhysicsGlobals.GravitationalAcceleration): 0;
                    // Change in mass over time interval dT
                    var deltaMass = massFlowRate * TimeWarp.fixedDeltaTime;
                    // Resource demand from propellants with mass
                    demandMass = densityAverage > 0 ? deltaMass / densityAverage : 0;

                    // Calculate resource demands
                    fuelDemands = CalculateDemands(demandMass);
                    // Apply resource demands & test for resource depletion
                    ApplyDemands(fuelDemands, ref propellantReqMetFactor);

                    // calculate maximum flow
                    var maxFuelFlow = IspPersistent > 0 ? engine.maxThrust / (IspPersistent * PhysicsGlobals.GravitationalAcceleration) : 0;
                    
                    // adjust fuel flow 
                    if (maxFuelFlow > 0 && propellantReqMetFactor > 0)
                        engine.maxFuelFlow = (float)(maxFuelFlow * propellantReqMetFactor);
                    else
                        vessel.ctrlState.mainThrottle = 0;

                    // update displayed thrust and fx
                    finalThrust = engine.currentThrottle * engine.maxThrust * Math.Min(propellantReqMetFactor, engine.propellantReqMet * 0.01f);
                }
                else
                {
                    propellantReqMetFactor = engine.propellantReqMet * 0.01f;

                    finalThrust = engine.GetCurrentThrust();
                }

                UpdatePropellantReqMetFactorQueue();

                UpdateFX(finalThrust);

                thrust_d = Utils.FormatThrust(finalThrust);

                SetAnimationRatio(engine.maxThrust > 0 ? finalThrust / engine.maxThrust: 0, throttleAnimationState);

                UpdateBuffers(fuelDemands);
            }
            else
            {
                if (ThrottlePersistent > 0 && IspPersistent > 0 && IsPersistentEngine && HasPersistentThrust)
                {
                    SetAnimationRatio(ThrottlePersistent, throttleAnimationState);
                    if(TimeWarp.CurrentRateIndex == 0)
                        warpToReal = true; // Set to true for transition to realtime

                    if (vessel.IsControllable && HasPersistentHeadingEnabled)
                    {
                        ratioHeadingVersusRequest = engine.PersistHeading(TimeWarp.fixedDeltaTime, headingTolerance, vesselChangedSOICountdown > 0, ratioHeadingVersusRequest == 1);
                        if (ratioHeadingVersusRequest != 1)
                        {
                            finalThrust = 0;
                            return;
                        }
                    }
                    // Calculated requested thrust
                    var requestedThrust = engine.thrustPercentage * 0.01f * ThrottlePersistent * engine.maxThrust;
                    var UT = Planetarium.GetUniversalTime(); // Universal time
                    var thrustUV = part.transform.up; // Thrust direction unit vector
                    // Calculate deltaV vector & resource demand from propellants with mass
                    var deltaVV = CalculateDeltaVV(vessel.totalMass, TimeWarp.fixedDeltaTime, requestedThrust, IspPersistent, thrustUV, out demandMass);
                    // Calculate resource demands
                    fuelDemands = CalculateDemands(demandMass);
                    // Apply resource demands & test for resource depletion
                    ApplyDemands(fuelDemands, ref propellantReqMetFactor);

                    // Apply deltaV vector at UT & dT to orbit if resources not depleted
                    if (propellantReqMetFactor > 0)
                    {
                        finalThrust = requestedThrust * propellantReqMetFactor;
                        vessel.orbit.Perturb(deltaVV * propellantReqMetFactor, UT);
                    }

                    // Otherwise log warning and drop out of timewarp if throttle on & depleted
                    else if (ThrottlePersistent > 0)
                    {
                        finalThrust = 0;
                        Debug.Log("[PersistentThrust]: Thrust warp stopped - propellant depleted");
                        ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_PT_StoppedDepleted"), 5.0f, ScreenMessageStyle.UPPER_CENTER);
                        // Return to realtime
                        TimeWarp.SetRate(0, true);
                        if (!vessel.IsControllable)
                        {
                            ThrottlePersistent = 0;
                            vessel.ctrlState.mainThrottle = 0;
                        }
                    }
                    else
                        finalThrust = 0;

                    UpdateFX(finalThrust);
                    thrust_d = Utils.FormatThrust(finalThrust);
                }
                else
                {
                    SetAnimationRatio(0, throttleAnimationState);

                    finalThrust = 0;
                    if (vessel.IsControllable && HasPersistentHeadingEnabled)
                        ratioHeadingVersusRequest = engine.PersistHeading(TimeWarp.fixedDeltaTime, headingTolerance, vesselChangedSOICountdown > 0);
                    UpdateFX(0);

                    UpdateBuffers(fuelDemands);
                }
            }
        }

        public static AnimationState[] SetUpAnimation(string animationName, Part part)
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

        public static void SetAnimationRatio(float ratio, AnimationState[] animationState)
        {
            if (animationState == null) return;

            foreach (var anim in animationState)
            {
                anim.normalizedTime = ratio;
            }
        }

        #region Kerbalism

        public virtual string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
        {
            useKerbalismInFlight = true;

            this.availableResources = availableResources;

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
