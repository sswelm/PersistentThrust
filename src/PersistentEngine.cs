using KSP.Localization;
using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace PersistentThrust
{
    public class PersistentEngine : PartModule
    {
        // GUI
        [KSPField(guiFormat = "F1", guiActive = true, guiName = "#autoLOC_6001378", guiUnits = "#autoLOC_7001400")]
        public float realIsp;
        [KSPField(guiFormat = "F6", guiActive = true, guiName = "#autoLOC_6001377", guiUnits = "#autoLOC_7001408")]
        public float finalThrust;
        [KSPField(guiFormat = "F2", guiActive = true, guiName = "#autoLOC_6001376", guiUnits = "%")]
        public float propellantReqMet;

        // Enable/disable persistent engine features
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_PersistentThrust"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool HasPersistentThrust = true;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_PersistentHeading"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool HasPersistentHeadingEnabled = true;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_MaximizePersistentIsp"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool MaximizePersistentIsp = false;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_MaximizePersistentPower"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool MaximizePersistentPower = false;

        // Config Settings
        [KSPField]
        public bool useDynamicBuffer = false;
        [KSPField]
        public bool processMasslessSeperately = true;
        [KSPField]
        public int queueLength = 2;
        [KSPField]
        public double fudgeExponent = 0.27;
        [KSPField]
        public int missingPowerCountdownSize = 10;
        [KSPField]
        public int buffersizeMult = 50;
        [KSPField]
        public double minimumPropellantReqMetFactor = 0.2;
        [KSPField]
        public float headingTolerance = 0.001f;
        [KSPField]
        public bool RequestPropMassless = true;             // Flag whether to request massless resources
        [KSPField]
        public bool RequestPropMass = true;                 // Flag whether to request resources with mass

        public string powerEffectName;
        public string runningEffectName;

        public double ratioHeadingVersusRequest;
        public double demandMass;
        public double dynamicBufferSize;

        // Engine module on the same part
        public ModuleEngines engine;
        public ModuleEnginesFX engineFX;

        public float ThrottlePersistent = 0;                // Persistent values to use during timewarp
        public float IspPersistent = 0;

        public float propellantReqMetFactor;
        
        public bool IsPersistentEngine = false;             // Flag if using PersistentEngine features        
        public bool warpToReal = false;                     // Are we transitioning from timewarp to reatime?
        public bool engineHasAnyMassLessPropellants;

        public bool autoMaximizePersistentIsp;
        public bool useKerbalismInFlight = false;

        public int vesselChangedSOICountdown = 0;
        public int missingPowerCountdown = 0;
        public int updateFuelCounter = 0;

        public double[] fuelDemands = new double[0];

        // Propellant data
        public List<PersistentPropellant> pplist = new List<PersistentPropellant>();

        // Average density of propellants
        public double densityAverage;
        public double buffersize;

        private float previousfixedDeltaTime;

        public double consumedPower;

        private Queue<double> propellantReqMetFactorQueue = new Queue<double>(100);

        private Queue<float> throttleQueue = new Queue<float>();
        private Queue<float> ispQueue = new Queue<float>();

        private Dictionary<string, double> availableResources = new Dictionary<string, double>();
        private Dictionary<string, double> kerbalismResourceChangeRequest = new Dictionary<string, double>();

        private List<PersistentEngine> persistentEngines;

        public override void OnStart(PartModule.StartState state)
        {
            if (state == StartState.Editor) return;

            persistentEngines = vessel.FindPartModulesImplementing<PersistentEngine>();
        }

        public void VesselChangedSOI()
        {
            vesselChangedSOICountdown = 10;
        }

        // Make "engine" and "engineFX" fields refer to the ModuleEngines and ModuleEnginesFX modules in part.Modules
        void FindModuleEngines()
        {
            foreach (PartModule pm in part.Modules)
            {
                if (pm is ModuleEngines)
                {
                    engine = pm as ModuleEngines;
                    IsPersistentEngine = true;
                }

                if (pm is ModuleEnginesFX)
                    engineFX = pm as ModuleEnginesFX;
            }

            if (engineFX != null)
            {
                if (string.IsNullOrEmpty(powerEffectName))
                    powerEffectName = engineFX.powerEffectName;

                engineFX.powerEffectName = "";

                if (string.IsNullOrEmpty(runningEffectName))
                    runningEffectName = engineFX.runningEffectName;

                engineFX.runningEffectName = "";
            }
        }

        // Update is called durring refresh frame, which can be less frequent than FixedUpdate which is called every processing frame
        public override void OnUpdate()
        {
            if (engine == null) return;

            if (processMasslessSeperately && engine.currentThrottle == 0 && engineHasAnyMassLessPropellants)
                RemoveMasslessPropellantsFromEngine(pplist);

            // hide stock thrust
            engine.Fields["finalThrust"].guiActive = false;
            engine.Fields["realIsp"].guiActive = false;
            engine.Fields["propellantReqMet"].guiActive = false;

            propellantReqMet = propellantReqMetFactor * 100;
            realIsp = !this.vessel.packed && !engine.propellants.Any(m => m.resourceDef.density == 0)
                ? engine.realIsp 
                : (this.vessel.packed && (MaximizePersistentIsp || autoMaximizePersistentIsp)) || ThrottlePersistent == 0 
                        ? IspPersistent 
                        : IspPersistent * propellantReqMetFactor;

            if (!IsPersistentEngine || !HasPersistentThrust) return;

            // When transitioning from timewarp to real update throttle
            if (warpToReal)
            {
                vessel.ctrlState.mainThrottle = ThrottlePersistent;
                warpToReal = false;
            }

            if (vessel.packed)
            {
                // stop engines when X pressed
                if (Input.GetKeyDown(KeyCode.X))
                    SetThrotleAndReturnToRealtime(0);
                // full throtle when Z pressed
                else if (Input.GetKeyDown(KeyCode.Z))
                    SetThrotleAndReturnToRealtime(1);
                // increase throtle when Shift pressed
                else if (Input.GetKeyDown(KeyCode.LeftShift))
                    SetThrotleAndReturnToRealtime(Mathf.Min(1, ThrottlePersistent + 0.01f));
                // decrease throtle when Ctrl pressed
                else if (Input.GetKeyDown(KeyCode.LeftControl))
                    SetThrotleAndReturnToRealtime(Mathf.Max(0, ThrottlePersistent - 0.01f));
            }
            else
                TimeWarp.GThreshold = 12f;
        }

        private void SetThrotleAndReturnToRealtime(float newsetting)
        {
            vessel.ctrlState.mainThrottle = newsetting;

            // Return to realtime
            TimeWarp.SetRate(0, true);
        }

        // Initialization
        public override void OnLoad(ConfigNode node)
        {
            // Run base OnLoad method
            base.OnLoad(node);

            // Populate engine and engineFX fields
            FindModuleEngines();

            if (IsPersistentEngine)
            {
                // Initialize PersistentPropellant list
                pplist = PersistentPropellant.MakeList(engine.propellants);

                // Initialize density of propellant used in deltaV and mass calculations
                densityAverage = pplist.AverageDensity();
            }
        }

        //[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "UpdateFuel")]
        //public void UpdateFuelEvent()
        //{
        //    RemoveMasslessPropellantsFromEngine(pplist);
        //}

        private void RemoveMasslessPropellantsFromEngine(List<PersistentPropellant> pplist)
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
            if (demandMass > 0)
            {
                // Per propellant demand
                for (var i = 0; i < pplist.Count; i++)
                {
                    demands[i] = pplist[i].CalculateDemand(demandMass);
                }
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
                // calculate total demand
                pp.totalEnginesDemand = persistentEngines.Sum(m => m.pplist.Where(l => l.definition.id == pp.definition.id).Sum(l => l.normalizedDemand));
            }
            else
                activePropellant = firstProcessedEngine.pplist.First(m => m.definition.id == pp.definition.id);

            return activePropellant;
        }

        // Apply demanded resources & return results
        // Updated depleted boolean flag if resource request failed
        public virtual double[] ApplyDemands(double[] demands, ref float finalPropellantReqMetFactor)
        {
            double overalPropellantReqMet = 1;

            autoMaximizePersistentIsp = false;

            var demandsOut = new double[pplist.Count];

            // first do a simulation run to determine the propellant availability so we don't over consume
            for (var i = 0; i < pplist.Count; i++)
            {
                var pp = pplist[i];
                // Request resources if:
                // - resource has mass & request mass flag true
                // - resource massless & request massless flag true
                if ((pp.density > 0 && RequestPropMass) || (pp.density == 0 && RequestPropMassless))
                {
                    var demandIn = demands[i];
                    var storageModifier = 1.0;

                    if (pp.density == 0)
                    {
                        // find initial resource amount for propellant
                        var availablePropellant = LoadPropellantAvailability(pp);

                        double kerbalismAmount = 0;
                        availableResources.TryGetValue(pp.definition.name, out kerbalismAmount);

                        var currentPropellantAmount = useKerbalismInFlight ? kerbalismAmount : availablePropellant.amount;

                        // update power buffer
                        buffersize = UpdateBuffer(availablePropellant, demandIn);

                        var bufferedTotalEnginesDemand = Math.Min(availablePropellant.maxamount, availablePropellant.totalEnginesDemand * 50);

                        if (bufferedTotalEnginesDemand > currentPropellantAmount)
                            storageModifier = Math.Min(1, (demandIn / availablePropellant.totalEnginesDemand) + ((currentPropellantAmount / bufferedTotalEnginesDemand) * (demandIn / availablePropellant.totalEnginesDemand)));

                        if (!MaximizePersistentPower && currentPropellantAmount < buffersize)
                            storageModifier *= currentPropellantAmount / buffersize;
                    }

                    var demandOut = IsInfinite(pp.propellant) ? demandIn : RequestResource(pp, demandIn * storageModifier, true);

                    var propellantFoundRatio = demandOut >= demandIn ? 1 : demandIn > 0 ? demandOut / demandIn : 0;

                    if (propellantFoundRatio < overalPropellantReqMet)
                        overalPropellantReqMet = propellantFoundRatio;

                    if (pp.propellant.resourceDef.density > 0)
                    {
                        // reset stabilize Queue when out of mass propellant
                        if (propellantFoundRatio < 1)
                            propellantReqMetFactorQueue.Clear();
                    }
                    else
                    {
                        if (propellantFoundRatio == 0)
                        {
                            // reset stabilize Queue when out power for too long
                            if (missingPowerCountdown <= 0)
                                propellantReqMetFactorQueue.Clear();
                            missingPowerCountdown--;
                        }
                        else
                            missingPowerCountdown = missingPowerCountdownSize;
                    }
                }
            }

            // attempt to stabilize thrust output with First In Last Out Queue 
            propellantReqMetFactorQueue.Enqueue(overalPropellantReqMet);
            if (propellantReqMetFactorQueue.Count() > 100)
                propellantReqMetFactorQueue.Dequeue();
            var averagePropellantReqMetFactor = propellantReqMetFactorQueue.Average();

            if (averagePropellantReqMetFactor < minimumPropellantReqMetFactor)
                autoMaximizePersistentIsp = true;

            finalPropellantReqMetFactor = (!this.vessel.packed || MaximizePersistentIsp || autoMaximizePersistentIsp) ? (float)averagePropellantReqMetFactor : (float)Math.Pow(averagePropellantReqMetFactor, fudgeExponent);

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
                        : overalPropellantReqMet * demands[i];

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
            if (propellant.density > 0 && !this.vessel.packed)
                return demand;

            if (useKerbalismInFlight)
            {
                double currentAmount;
                availableResources.TryGetValue(propellant.definition.name, out currentAmount);

                double available = Math.Min(currentAmount, demand);

                double updateAmount = Math.Max(0, currentAmount - demand);

                if (simulate)
                    availableResources[propellant.definition.name] = updateAmount;
                else
                {
                    var demandPerSecond = demand / TimeWarp.fixedDeltaTime;

                    double currentDemand;
                    kerbalismResourceChangeRequest.TryGetValue(propellant.definition.name, out currentDemand);
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

            var partresource = part.Resources[propellant.definition.name];
            if (partresource == null)
            {
                var node = new ConfigNode("RESOURCE");
                node.AddValue("name", propellant.definition.name);
                node.AddValue("maxAmount", 0);
                node.AddValue("amount", 0);
                this.part.AddResource(node);

                partresource = part.Resources[propellant.definition.name];
            }

            partresource.maxAmount = dynamicBufferSize;
            partresource.amount = dynamicBufferSize * amountRatio;

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

        public virtual void UpdateFX(double currentThust)
        {
            if (!engine.getIgnitionState)
                currentThust = 0;

            var exhaustRatio = (float)(engine.maxThrust > 0 ? currentThust / engine.maxThrust : 0);

            if (!String.IsNullOrEmpty(powerEffectName))
                part.Effect(powerEffectName, exhaustRatio);

            if (!String.IsNullOrEmpty(runningEffectName))
                part.Effect(runningEffectName, exhaustRatio);
        }

        private void UpdatePropellantReqMetFactorQueue()
        {
            if (propellantReqMetFactor > 0 && engine.currentThrottle > 0)
            {
                if (engineHasAnyMassLessPropellants)
                    propellantReqMetFactorQueue.Enqueue(Math.Pow(propellantReqMetFactor, 1 / fudgeExponent));
                else
                    propellantReqMetFactorQueue.Enqueue(propellantReqMetFactor);

                if (propellantReqMetFactorQueue.Count > 100)
                    propellantReqMetFactorQueue.Dequeue();
            }
            else
                propellantReqMetFactorQueue.Clear();
        }

        // Physics update
        public void FixedUpdate() // FixedUpdate is also called while not staged
        {
            if (this.vessel is null || !isEnabled) return;

            kerbalismResourceChangeRequest.Clear();

            if (vesselChangedSOICountdown > 0)
                vesselChangedSOICountdown--;

            // Realtime mode
            if (!this.vessel.packed)
            {
                engineHasAnyMassLessPropellants = engine.propellants.Any(m => m.resourceDef.density == 0);

                // Update persistent thrust parameters if NOT transitioning from warp to realtime
                if (!warpToReal)
                    UpdatePersistentParameters();

                ratioHeadingVersusRequest = 0;

                if (!engineHasAnyMassLessPropellants)
                {
                    // Mass flow rate
                    var massFlowRate = IspPersistent > 0 ?  (engine.requestedThrottle * engine.maxThrust) / (IspPersistent * PhysicsGlobals.GravitationalAcceleration): 0;
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
                    if (maxFuelFlow > 0)
                        engine.maxFuelFlow = (float)(maxFuelFlow * propellantReqMetFactor);
                    // update displayed thrust and fx
                    finalThrust = engine.currentThrottle * engine.maxThrust * propellantReqMetFactor;
                }
                else
                {
                    missingPowerCountdown = missingPowerCountdownSize;

                    propellantReqMetFactor = engine.propellantReqMet * 0.01f;

                    finalThrust = engine.GetCurrentThrust();
                }

                UpdatePropellantReqMetFactorQueue();

                UpdateFX(finalThrust);

                UpdateBuffers(fuelDemands);
            }
            else
            {
                if (ThrottlePersistent > 0 && IspPersistent > 0 && IsPersistentEngine && HasPersistentThrust)
                {
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
                    var thrustUV = this.part.transform.up; // Thrust direction unit vector
                    // Calculate deltaV vector & resource demand from propellants with mass
                    var deltaVV = CalculateDeltaVV(this.vessel.totalMass, TimeWarp.fixedDeltaTime, requestedThrust, IspPersistent, thrustUV, out demandMass);
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
                }
                else
                {
                    finalThrust = 0;
                    if (vessel.IsControllable && HasPersistentHeadingEnabled)
                        ratioHeadingVersusRequest = engine.PersistHeading(TimeWarp.fixedDeltaTime, headingTolerance, vesselChangedSOICountdown > 0);
                    UpdateFX(0);

                    UpdateBuffers(fuelDemands);
                }
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
