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
        public readonly Queue<float> propellantReqMetFactorQueue = new Queue<float>();
        public readonly Queue<float> ispQueue = new Queue<float>();
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
        public double[] fuelDemands = new double[0];
        public ModuleEngines engine;
        public List<PersistentPropellant> propellants;
    }

    public class PersistentEngine : PartModule
    {

        #region Fields

        // Persistant
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_PersistentThrust"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool HasPersistentThrust = true;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_PersistentHeading"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool HasPersistentHeadingEnabled = true;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_MaximizePersistentIsp"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool MaximizePersistentIsp = true;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "#LOC_PT_MaximizePersistentPower"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889", affectSymCounterparts = UI_Scene.All)]
        public bool MaximizePersistentPower = false;
        [KSPField(isPersistant = true, guiActiveEditor = false, guiActive = true, guiName = "#LOC_PT_ManeuverTolerance", guiUnits = " %"), UI_FloatRange(stepIncrement = 1, maxValue = 90, minValue = 0, requireFullControl = false, affectSymCounterparts = UI_Scene.All)]//Beamed Power Throttle
        public float maneuverToleranceInDegree = 90;

        // Persistent values to use during TimeWarp and offline processing
        [KSPField(isPersistant = true)]
        public VesselAutopilot.AutopilotMode persistentAutopilotMode;
        [KSPField(isPersistant = true)]
        public double persistentThrust;
        [KSPField(isPersistant = true)]
        public float persistentIsp;
        [KSPField(isPersistant = true)]
        public float persistentThrottle;
        [KSPField(isPersistant = true)]
        public double persistentAverageDensity;
        [KSPField(isPersistant = true)]
        public double vesselAlignmentWithAutopilotMode;
        [KSPField(isPersistant = true)]
        public string persistentResourceChange;
        [KSPField(isPersistant = true)]
        public float vesselDryMass;
        [KSPField(isPersistant = true)]
        public Guid persistentVesselTargetId;
        [KSPField(isPersistant = true)]
        public string persistentVesselTargetBodyName;
        [KSPField(isPersistant = true)]
        public string persistentManeuverPatch;
        [KSPField(isPersistant = true)]
        public string persistentManeuverNextPatch;
        [KSPField(isPersistant = true)]
        public double persistentManeuverUT;

        // GUI
        [KSPField(guiFormat = "F1", guiActive = true, guiName = "#autoLOC_6001378", guiUnits = "#autoLOC_7001400")]
        public float realIsp;
        [KSPField(guiFormat = "F6", guiActive = true, guiName = "#autoLOC_6001377")]
        public string thrustTxt;
        [KSPField(guiFormat = "F2", guiActive = true, guiName = "#autoLOC_6001376", guiUnits = "%")]
        public float propellantReqMet;
        [KSPField(guiFormat = "F3", guiUnits = " U/s")]
        public double masslessUsage;
        [KSPField(guiFormat = "F3", guiActive = true, guiName = "#LOC_PT_HeadingVersusManeuver", guiUnits = " deg")]
        public double vesselHeadingVersusManeuverInDegree;

        // Config Settings
        [KSPField]
        public string throttleAnimationName;
        [KSPField]
        public bool useDynamicBuffer = false;
        [KSPField]
        public bool processMasslessSeparately = true;
        [KSPField]
        public float fudgeExponent = 0.27f;
        [KSPField]
        public int bufferSizeMult = 50;
        [KSPField]
        public bool requestPropMassless = true;             // Flag whether to request massless resources
        [KSPField]
        public bool requestPropMass = true;                 // Flag whether to request resources with mass

        public List<string> powerEffectNameList = new List<string>();
        public List<string> runningEffectNameList = new List<string>();

        public PersistentEngineModule currentEngine;
        public PersistentEngineModule[] moduleEngines = new PersistentEngineModule[0];
        public MultiModeEngine multiModeEngine;
        public PartModule GTI_MultiModeEngineFX;
        public AnimationState[] throttleAnimationState;
        public BaseField masslessUsageField;
        public FieldInfo currentModuleEngineFieldInfo;

        public double bufferSize;
        public double vesselHeadingVersusManeuver;
        public float previousFixedDeltaTime;
        public bool isPersistentEngine;             // Flag if using PersistentEngine features
        public bool warpToReal;                     // Are we transitioning from TimeWarp to realtime?
        public int warpToRealCountDown;
        public bool isMultiMode;

        public int vesselChangedSoiCountdown = 10;
        public int fixedUpdateCount;

        private readonly Dictionary<string, double> _kerbalismResourceChangeRequest = new Dictionary<string, double>();
        private readonly Queue<float> _mainThrottleQueue = new Queue<float>();
        private Dictionary<string, double> _availablePartResources = new Dictionary<string, double>();

        private List<PersistentEngine> _persistentEngines;

        #endregion


        #region Events

        /// <summary>
        /// Uses the OnLoad event to initialize the PersistentEngine module.
        /// </summary>
        /// <param name="node"></param>
        public override void OnLoad(ConfigNode node)
        {
            // Run base OnLoad method
            base.OnLoad(node);

            if (part == null || part.partInfo == null)
                return;

            Debug.Log("[PersistentThrust]: OnLoad called for " + part.partInfo.title + " " + part.persistentId);

            // Populate moduleEngine and moduleEngineFx fields
            FindModuleEngines();

            // Initialize PersistentPropellant list
            foreach (var engine in moduleEngines)
            {
                engine.propellants = PersistentPropellant.MakeList(engine.engine.propellants);
                engine.averageDensity = engine.propellants.AverageDensity();
            }
        }

        /// <summary>
        /// Uses the to store data required for background processing 
        /// </summary>
        /// <param name="node"></param>
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;

            persistentAverageDensity = isMultiMode
                ? currentEngine.averageDensity
                : persistentThrust > 0 
                    ? moduleEngines.Sum(m => m.finalThrust * m.averageDensity) / moduleEngines.Sum(m => m.engine.maxThrust) 
                    : persistentThrust;

            vesselDryMass = vessel.GetDryMass();

            // serialize resource request
            if (_kerbalismResourceChangeRequest.Any())
                persistentResourceChange = string.Join(";", _kerbalismResourceChangeRequest.Select(x => x.Key + "=" + x.Value).ToArray());

            if (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Target || vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.AntiTarget)
            {
                var orbitDriver = vessel.targetObject.GetOrbitDriver();
                if (orbitDriver.vessel != null)
                {
                    persistentVesselTargetId = orbitDriver.vessel.id;
                    persistentVesselTargetBodyName = string.Empty;
                }
                else if (orbitDriver.celestialBody != null)
                {
                    persistentVesselTargetId = Guid.Empty;
                    persistentVesselTargetBodyName = orbitDriver.celestialBody.bodyName;
                }
                else
                {
                    persistentVesselTargetId = Guid.Empty;
                    persistentVesselTargetBodyName = string.Empty;
                }

                node.SetValue(nameof(persistentVesselTargetId), persistentVesselTargetId.ToString(), true);
                node.SetValue(nameof(persistentVesselTargetBodyName), persistentVesselTargetBodyName, true);
            }
            else if (vessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.Maneuver && vessel.patchedConicSolver.maneuverNodes.Count > 0)
            {
                var maneuverNode = vessel.patchedConicSolver.maneuverNodes[0];

                persistentManeuverUT = maneuverNode.UT;
                persistentManeuverNextPatch = maneuverNode.nextPatch.Serialize();
                persistentManeuverPatch = maneuverNode.patch.Serialize();

                node.SetValue(nameof(persistentManeuverUT), persistentManeuverUT, true);
                node.SetValue(nameof(persistentManeuverNextPatch), persistentManeuverNextPatch, true);
                node.SetValue(nameof(persistentManeuverPatch), persistentManeuverPatch, true);
            }
        }



        /// <summary>
        /// Called when the part starts.
        /// </summary>
        /// <param name="state"> gives an indication of where in flight you are </param>
        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor) return;

            if (!string.IsNullOrEmpty(throttleAnimationName))
                throttleAnimationState = SetUpAnimation(throttleAnimationName, part);

            _persistentEngines = vessel.FindPartModulesImplementing<PersistentEngine>();

            masslessUsageField = Fields[nameof(this.masslessUsage)];
        }



        /// <summary>
        /// Called by the part every refresh frame where it is active, which can be less frequent than FixedUpdate which is called every processing frame
        /// </summary>
        public override void OnUpdate()
        {
            thrustTxt = Utils.FormatThrust(persistentThrust);

            PersistentEngineModule[] processedEngines = isMultiMode ? new[] { currentEngine } : moduleEngines;

            for (var i = 0; i < processedEngines.Length; i++)
            {
                currentEngine = processedEngines[i];

                if (currentEngine.engine == null) continue;

                // hide stock fields
                currentEngine.engine.Fields[nameof(currentEngine.engine.finalThrust)].guiActive = false;
                currentEngine.engine.Fields[nameof(currentEngine.engine.realIsp)].guiActive = false;
                currentEngine.engine.Fields[nameof(currentEngine.engine.propellantReqMet)].guiActive = false;
            }

            float averagePropellantReqMetFactor = isMultiMode
                ? currentEngine.propellantReqMetFactor
                : moduleEngines.Average(m => m.propellantReqMetFactor);

            propellantReqMet = averagePropellantReqMetFactor * 100;

            bool anyMasslessPropellants = isMultiMode
                ? currentEngine.engine.propellants.Any(m => m.resourceDef.density == 0)
                : moduleEngines.SelectMany(m => m.engine.propellants.Where(p => p.resourceDef.density == 0)).Any();

            bool anyAutoMaximizePersistentIsp = isMultiMode
                ? currentEngine.autoMaximizePersistentIsp
                : moduleEngines.Any(m => m.autoMaximizePersistentIsp);

            realIsp = !vessel.packed && !anyMasslessPropellants
                ? persistentIsp
                : vessel.packed && (MaximizePersistentIsp || anyAutoMaximizePersistentIsp) || persistentThrottle == 0
                    ? persistentIsp
                    : persistentIsp * averagePropellantReqMetFactor;

            UpdateMasslessPropellant();

            if (!isPersistentEngine || !HasPersistentThrust) return;

            // When transitioning from TimeWarp to real update throttle
            if (warpToReal)
            {
                SetThrottle(persistentThrottle, true);

                if (warpToRealCountDown-- <= 0)
                    warpToReal = false;
            }

            if (vessel.packed)
            {
                // maintain thrust setting during TimeWarp
                vessel.ctrlState.mainThrottle = persistentThrottle;

                // stop engines when X pressed
                if (Input.GetKeyDown(KeyCode.X))
                    SetThrottleAfterKey(0, true);
                // full throttle when Z pressed
                else if (Input.GetKeyDown(KeyCode.Z))
                    SetThrottleAfterKey(1, HighLogic.CurrentGame.Parameters.CustomParams<PTSettings>().returnToRealtimeAfterKeyPressed);
                // increase throttle when Shift pressed
                else if (Input.GetKeyDown(KeyCode.LeftShift))
                    SetThrottleAfterKey(Mathf.Min(1, persistentThrottle + 0.01f), HighLogic.CurrentGame.Parameters.CustomParams<PTSettings>().returnToRealtimeAfterKeyPressed);
                // decrease throttle when Ctrl pressed
                else if (Input.GetKeyDown(KeyCode.LeftControl))
                    SetThrottleAfterKey(Mathf.Max(0, persistentThrottle - 0.01f), HighLogic.CurrentGame.Parameters.CustomParams<PTSettings>().returnToRealtimeAfterKeyPressed);
            }
            else
                TimeWarp.GThreshold = 12;
        }



        /// <summary>
        /// [Unity] Update is called every frame, if the MonoBehaviour is enabled.
        /// </summary>
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



        /// <summary>
        /// [Unity] This function is called every fixed framerate frame (KSP default: 0.02s), if the MonoBehaviour is enabled.
        /// </summary>
        public void FixedUpdate() // FixedUpdate is also called while not staged
        {
            if (vessel is null || currentEngine?.engine is null || !isEnabled) return;

            vesselHeadingVersusManeuver = vessel.GetVesselOrbitHeadingVersusManeuverVector();
            vesselHeadingVersusManeuverInDegree = Math.Acos(Math.Max(-1, Math.Min(1, vesselHeadingVersusManeuver))) * Orbit.Rad2Deg;

            RestoreHeadingAtLoad();

            _kerbalismResourceChangeRequest.Clear();

            if (vesselChangedSoiCountdown > 0)
                vesselChangedSoiCountdown--;

            // Checks if moduleEngine mode wasn't switched
            FetchActiveEngineModule();

            ResetMonitoringVariables();

            PersistentEngineModule[] processedEngines = isMultiMode ? new[] { currentEngine } : moduleEngines;

            // Realtime mode
            if (!vessel.packed)
            {
                vesselAlignmentWithAutopilotMode = vessel.HeadingVersusAutopilotVector(Planetarium.GetUniversalTime());

                // Update persistent thrust throttle if NOT transitioning from warp to realtime
                if (!warpToReal)
                    UpdatePersistentThrottle();

                for (var i = 0; i < processedEngines.Length; i++)
                {
                    currentEngine = processedEngines[i];

                    // Update persistent thrust isp if NOT transitioning from warp to realtime
                    if (!warpToReal)
                        UpdateCurrentEnginePersistentIsp();

                    currentEngine.engineHasAnyMassLessPropellants = currentEngine.engine.propellants.Any(m => m.resourceDef.density == 0);

                    if (processMasslessSeparately && currentEngine.engineHasAnyMassLessPropellants)
                        ReloadPropellantsWithoutMasslessPropellants();

                    if (vesselHeadingVersusManeuverInDegree > maneuverToleranceInDegree + (persistentThrust > 0 ? 1 : 0))
                    {
                        //currentEngine.engine.maxFuelFlow = 1e-10f;
                        currentEngine.engine.multFlow = 0;
                        currentEngine.finalThrust = 0;

                        UpdateBuffers();
                    }
                    else if (!currentEngine.engine.getIgnitionState)
                    {
                        currentEngine.finalThrust = 0;

                        // restore maximum flow
                        RestoreMaxFuelFlow();

                        UpdateBuffers();
                    }
                    else if (!currentEngine.engineHasAnyMassLessPropellants && currentEngine.engine.propellantReqMet > 0)
                    {
                        // Mass flow rate
                        double massFlowRate = currentEngine.persistentIsp > 0
                            ? currentEngine.engine.currentThrottle * currentEngine.engine.maxThrust / (currentEngine.persistentIsp * PhysicsGlobals.GravitationalAcceleration)
                            : 0;
                        // Resource demand from propellants with mass
                        currentEngine.demandMass = currentEngine.averageDensity > 0 ? massFlowRate * TimeWarp.fixedDeltaTime / currentEngine.averageDensity : 0;
                        // Calculate resource demands
                        currentEngine.fuelDemands = CalculateDemands(currentEngine.demandMass);
                        // Apply resource demands & test for resource depletion
                        ApplyDemands(currentEngine.fuelDemands, ref currentEngine.propellantReqMetFactor);

                        // calculate maximum flow
                        double maxFuelFlow = currentEngine.persistentIsp > 0 ? currentEngine.engine.maxThrust / (currentEngine.persistentIsp * PhysicsGlobals.GravitationalAcceleration) : 0;

                        // adjust fuel flow
                        //currentEngine.engine.maxFuelFlow = maxFuelFlow > 0 && currentEngine.propellantReqMetFactor > 0 ? (float)(maxFuelFlow * currentEngine.propellantReqMetFactor) : 1e-10f;
                        currentEngine.engine.multFlow = maxFuelFlow > 0 && currentEngine.propellantReqMetFactor > 0 ? currentEngine.propellantReqMetFactor : 0;

                        // update displayed thrust and fx
                        currentEngine.finalThrust = currentEngine.engine.currentThrottle * currentEngine.engine.maxThrust * Math.Min(currentEngine.propellantReqMetFactor, currentEngine.engine.propellantReqMet * 0.01f);
                    }
                    else
                    {
                        // restore maximum flow
                        RestoreMaxFuelFlow();

                        currentEngine.propellantReqMetFactor = currentEngine.engine.propellantReqMet * 0.01f;

                        currentEngine.finalThrust = currentEngine.engine.GetCurrentThrust();

                        UpdateBuffers();
                    }

                    UpdatePropellantReqMetFactorQueue();

                    UpdateFX();

                    SetThrottleAnimation();
                }
            }
            else
            {
                if (TimeWarp.CurrentRateIndex == 0)
                {
                    if (!warpToReal)
                        warpToRealCountDown = 2;

                    warpToReal = true; // Set to true for transition to realtime
                }

                for (var i = 0; i < processedEngines.Length; i++)
                {
                    currentEngine = processedEngines[i];

                    // restore maximum flow
                    RestoreMaxFuelFlow();

                    if (persistentThrottle > 0 && currentEngine.persistentIsp > 0 && isPersistentEngine && HasPersistentThrust)
                    {
                        // Calculated requested thrust
                        var requestedThrust = vesselHeadingVersusManeuverInDegree <= (maneuverToleranceInDegree + (persistentThrust > 0 ? 1 : 0))
                            ? currentEngine.engine.thrustPercentage * 0.01f * persistentThrottle * currentEngine.engine.maxThrust
                            : 0;

                        // Calculate deltaV vector & resource demand from propellants with mass
                        Vector3d deltaVVector = Utils.CalculateDeltaVVector(currentEngine.averageDensity, vessel.GetTotalMass(), TimeWarp.fixedDeltaTime, requestedThrust, currentEngine.persistentIsp, part.transform.up, out currentEngine.demandMass);

                        //Debug.Log("[PersistentThrust]: For vessel with mass " + vessel.totalMass + " applied Perturb for " + deltaVVector.magnitude.ToString("F5") +
                        //          " m/s resulting in speed " + vessel.obt_velocity.magnitude);

                        // Calculate resource demands
                        currentEngine.fuelDemands = CalculateDemands(currentEngine.demandMass);
                        // Apply resource demands & test for resource depletion
                        ApplyDemands(currentEngine.fuelDemands, ref currentEngine.propellantReqMetFactor);

                        // Apply deltaV vector at UT & dT to orbit if resources not depleted
                        if (currentEngine.propellantReqMetFactor > 0)
                        {
                            currentEngine.finalThrust = requestedThrust * currentEngine.propellantReqMetFactor;
                            vessel.orbit.Perturb(deltaVVector * currentEngine.propellantReqMetFactor, Planetarium.GetUniversalTime());
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
                    }
                    else
                    {
                        currentEngine.finalThrust = 0;

                        UpdateBuffers();
                    }

                    SetThrottleAnimation();

                    UpdateFX();
                }

                if (vessel.IsControllable && HasPersistentHeadingEnabled)
                    vesselAlignmentWithAutopilotMode = vessel.PersistHeading(TimeWarp.fixedDeltaTime, HighLogic.CurrentGame.Parameters.CustomParams<PTDevSettings>().headingTolerance, vesselChangedSoiCountdown > 0, vesselAlignmentWithAutopilotMode == 1);
            }

            CollectStatistics();
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Updates the required buffer size for correct resource consumption at high warp rates.
        /// </summary>
        public double UpdateBuffer(PersistentPropellant propellant, double baseSize)
        {
            double requiredBufferSize = useDynamicBuffer
                ? Math.Max(baseSize / TimeWarp.fixedDeltaTime * 10 * bufferSizeMult, baseSize * bufferSizeMult)
                : Math.Max(0, propellant.maxAmount - baseSize);

            if (previousFixedDeltaTime == TimeWarp.fixedDeltaTime)
                return requiredBufferSize;

            double amountRatio = propellant.maxAmount > 0 ? Math.Min(1, propellant.amount / propellant.maxAmount) : 0;

            double dynamicBufferSize = useDynamicBuffer ? requiredBufferSize : 0;

            if (dynamicBufferSize <= 0)
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



        /// <summary>
        /// Calls the UpdateBuffer method for all the suitable propellants in the current engine.
        /// </summary>
        public void UpdateBuffers()
        {
            for (var i = 0; i < currentEngine.propellants.Count; i++)
            {
                var currentPropellant = currentEngine.propellants[i];

                if (currentPropellant.density != 0 || i >= currentEngine.fuelDemands.Count()) continue;

                // find initial resource amount for propellant
                PersistentPropellant availablePropellant = LoadPropellantAvailability(currentPropellant);

                // update power buffer
                bufferSize = UpdateBuffer(availablePropellant, currentEngine.fuelDemands[i]);
            }
        }



        /// <summary>
        /// Calculates demands of each resource from a total mass input.
        /// </summary>
        public double[] CalculateDemands(double mass)
        {
            var demands = new double[currentEngine.propellants.Count];
            if (mass <= 0) return demands;

            // Per propellant demand
            for (var i = 0; i < currentEngine.propellants.Count; i++)
            {
                demands[i] = currentEngine.propellants[i].CalculateDemand(mass);
            }
            return demands;
        }



        /// <summary>
        /// Applies demanded resources & returns results.
        /// Updates depleted boolean flag if resource request failed.
        /// </summary>
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
                if ((persistentPropellant.density <= 0 || !requestPropMass) && (persistentPropellant.density != 0 || !requestPropMassless)) continue;

                persistentPropellant.demandIn = demands[i];
                var storageModifier = 1.0;

                // Process massless propellants like ElectricCharge separately
                if (persistentPropellant.density == 0)
                {
                    // find initial resource amount for propellant
                    PersistentPropellant availablePropellant = LoadPropellantAvailability(persistentPropellant);

                    _availablePartResources.TryGetValue(persistentPropellant.definition.name, out var kerbalismAmount);

                    double currentPropellantAmount = DetectKerbalism.Found() ? kerbalismAmount : availablePropellant.amount;

                    // update power buffer
                    bufferSize = UpdateBuffer(availablePropellant, persistentPropellant.demandIn);

                    var bufferedTotalEnginesDemand = Math.Min(availablePropellant.maxAmount, availablePropellant.totalEnginesDemand * bufferSizeMult);

                    if (bufferedTotalEnginesDemand > currentPropellantAmount && availablePropellant.totalEnginesDemand > 0)
                        storageModifier = Math.Min(1, (persistentPropellant.demandIn / availablePropellant.totalEnginesDemand) + currentPropellantAmount / bufferedTotalEnginesDemand * (persistentPropellant.demandIn / availablePropellant.totalEnginesDemand));

                    if (!MaximizePersistentPower && currentPropellantAmount < bufferSize)
                        storageModifier *= currentPropellantAmount / bufferSize;
                }

                persistentPropellant.demandOut = PersistentPropellant.IsInfinite(persistentPropellant.propellant)
                    ? persistentPropellant.demandIn
                    : RequestResource(persistentPropellant, persistentPropellant.demandIn * storageModifier, true);

                double propellantFoundRatio = persistentPropellant.demandOut >= persistentPropellant.demandIn
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
                    currentEngine.missingPowerCountdown = HighLogic.CurrentGame.Parameters.CustomParams<PTDevSettings>().missingPowerCountdownSize;
            }

            // attempt to stabilize thrust output with First In Last Out Queue
            currentEngine.propellantReqMetFactorQueue.Enqueue((float)overallPropellantReqMet);
            if (currentEngine.propellantReqMetFactorQueue.Count() > HighLogic.CurrentGame.Parameters.CustomParams<PTDevSettings>().propellantReqMetFactorQueueSize)
                currentEngine.propellantReqMetFactorQueue.Dequeue();
            float averagePropellantReqMetFactor = currentEngine.propellantReqMetFactorQueue.Average();

            if (averagePropellantReqMetFactor < HighLogic.CurrentGame.Parameters.CustomParams<PTDevSettings>().minimumPropellantReqMetFactor)
                currentEngine.autoMaximizePersistentIsp = true;

            finalPropellantReqMetFactor = !vessel.packed || MaximizePersistentIsp || currentEngine.autoMaximizePersistentIsp
                ? averagePropellantReqMetFactor
                : Mathf.Pow(averagePropellantReqMetFactor, fudgeExponent);

            // secondly we can consume the resource based on propellant availability
            for (var i = 0; i < currentEngine.propellants.Count; i++)
            {
                PersistentPropellant pp = currentEngine.propellants[i];
                // Request resources if:
                // - resource has mass & request mass flag true
                // - resource massless & request massless flag true
                if ((pp.density > 0 && requestPropMass) || (pp.density == 0 && requestPropMassless))
                {
                    double demandIn = pp.density > 0
                        ? MaximizePersistentIsp || currentEngine.autoMaximizePersistentIsp
                            ? averagePropellantReqMetFactor * demands[i]
                            : demands[i]
                        : overallPropellantReqMet * demands[i];

                    demandsOut[i] = PersistentPropellant.IsInfinite(pp.propellant) ? demandIn : RequestResource(pp, demandIn);
                }
                // Otherwise demand is 0
                else
                    demandsOut[i] = 0;
            }
            // Return demand outputs
            return demandsOut;
        }



        /// <summary>
        /// Updates the engine visual FX based on the ratio between current thrust and total thrust.
        /// </summary>
        public virtual void UpdateFX()
        {
            var exhaustRatio = currentEngine.engine.maxThrust > 0 ? currentEngine.finalThrust / currentEngine.engine.maxThrust : 0;

            ApplyEffect(currentEngine.powerEffectName, exhaustRatio);
            ApplyEffect(currentEngine.runningEffectName, exhaustRatio);
        }



        /// <summary>
        /// Resets vessel changed SOI countdown.
        /// </summary>
        public void VesselChangedSOI()
        {
            vesselChangedSoiCountdown = 10;
        }

        #endregion

        #region  Private Methods

        private static AnimationState[] SetUpAnimation(string animationName, Part part)
        {
            var states = new List<AnimationState>();
            foreach (var animation in part.FindModelAnimators(animationName))
            {
                AnimationState animationState = animation[animationName];
                animationState.speed = 0;
                animationState.enabled = true;
                animationState.wrapMode = WrapMode.ClampForever;
                animation.Blend(animationName);
                states.Add(animationState);
            }
            return states.ToArray();
        }



        /// <summary>
        /// Sets a ratio in an animation.
        /// </summary>
        private static void SetAnimationRatio(float ratio, AnimationState[] animationState)
        {
            if (animationState == null) return;

            foreach (var anim in animationState)
            {
                anim.normalizedTime = ratio;
            }
        }



        /// <summary>
        /// Applies an effect to the current part from an input ratio.
        /// </summary>
        private void ApplyEffect(string effect, float ratio)
        {
            if (string.IsNullOrEmpty(effect))
                return;

            part.Effect(effect, ratio, -1);
        }



        /// <summary>
        /// Sets throttle animation state and ratio for the active part.
        /// </summary>
        private void SetThrottleAnimation()
        {
            SetAnimationRatio(currentEngine.engine.maxThrust > 0 ? currentEngine.finalThrust / currentEngine.engine.maxThrust : 0, throttleAnimationState);
        }



        /// <summary>
        /// Adjusts the vessel's throttle and eventually returns to real time.
        /// </summary>
        private void SetThrottle(float newSetting, bool returnToRealTime = false, bool reset = false)
        {
            vessel.ctrlState.mainThrottle = newSetting;

            PersistentEngineModule[] processedEngines = isMultiMode ? new[] { currentEngine } : moduleEngines;
            for (var i = 0; i < processedEngines.Length; i++)
            {
                currentEngine = processedEngines[i];

                currentEngine.engine.requestedThrottle = newSetting;
                currentEngine.engine.currentThrottle = newSetting;

                if (reset)
                {
                    currentEngine.engine.Shutdown();
                    currentEngine.engine.Activate();
                }

                if (!returnToRealTime) continue;

                if (i == 0)
                {
                    // Return to realtime
                    TimeWarp.SetRate(0, true);
                }

                // adjust ignited information if RF is installed to prevent engine shutdown in the warp to realtime transition
                if (!DetectRealFuels.Found() || newSetting <= 0) continue;

                FieldInfo ignitedInfo = currentEngine.engine.GetType().GetField("ignited", BindingFlags.NonPublic | BindingFlags.Instance);

                if (ignitedInfo == null)
                    continue;

                ignitedInfo.SetValue(currentEngine.engine, true);
            }
        }



        /// <summary>
        /// Uses SetThrottle to adjust the vessel's throttle, but also updates warpToRealCountDown.
        /// Used when our own keybinds are used to drop from warp.
        /// </summary>
        private void SetThrottleAfterKey(float newSetting, bool returnToRealTime)
        {
            SetThrottle(newSetting, returnToRealTime);

            if (!returnToRealTime) return;

            warpToRealCountDown = 2;
            warpToReal = true;
        }



        /// <summary>
        /// Finds all the suitable engine modules in the active part and logs the results.
        /// </summary>
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
                    currentEngine = new PersistentEngineModule { engine = engine };

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
                else if (partModule.ClassName == "GTI_MultiModeEngineFX")
                {
                    Debug.Log("[PersistentThrust]: found GTI_MultiModeEngineFX on " + part.partInfo.title + " " + part.persistentId);
                    GTI_MultiModeEngineFX = partModule;
                    currentModuleEngineFieldInfo = GTI_MultiModeEngineFX.GetType().GetField("currentModuleEngine", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (currentModuleEngineFieldInfo == null)
                        Debug.LogError("[PersistentThrust]: failed to find currentModuleEngine on GTI_MultiModeEngineFX");
                    else
                        Debug.Log("[PersistentThrust]: found currentModuleEngine on GTI_MultiModeEngineFX");
                }
            }

            moduleEngines = moduleEnginesList.ToArray();


            var partIdentity = part.partInfo.title + " " + part.persistentId;
            if (moduleEnginesCount == 1 && multiModeEngine == null)
            {
                Debug.Log("[PersistentThrust]: enabled for " + partIdentity);
                isPersistentEngine = true;
            }
            else if (GTI_MultiModeEngineFX != null && moduleEnginesCount > 1)
            {
                Debug.Log("[PersistentThrust]: enabled GTI MultiMode for " + partIdentity);
                isPersistentEngine = true;
                isMultiMode = true;
            }
            else if (moduleEnginesCount == 0)
            {
                Debug.LogError("[PersistentThrust]: found no compatible engines, disabling PersistentThrust for " + partIdentity);
                isPersistentEngine = false;
            }
            else if (moduleEnginesCount == 1 && multiModeEngine != null)
            {
                Debug.LogWarning("[PersistentThrust]: found Insufficient engines for MultiMode, using single engine mode PersistentThrust for " + partIdentity);
                isPersistentEngine = false;
            }
            else if (moduleEnginesCount > 1 && multiModeEngine == null)
            {
                Debug.LogWarning("[PersistentThrust]: found multiple engines but no MultiMode PartModule, enabled multi engine PersistentThrust for " + partIdentity);
                isPersistentEngine = true;
            }
            else if (multiModeEngine != null && moduleEnginesCount == 2)
            {
                Debug.Log("[PersistentThrust]: enabled MultiMode for " + partIdentity);
                isPersistentEngine = true;
                isMultiMode = true;
            }
            else
            {
                Debug.LogError("[PersistentThrust]: failed to initialize for " + partIdentity);
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



        /// <summary>
        /// Finds the active moduleEngine module from the MultiModeEngine partModule
        /// </summary>
        private void FetchActiveEngineModule()
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

            ModuleEnginesFX currentModuleEngineFx = null;

            if (multiModeEngine != null)
                currentModuleEngineFx = multiModeEngine.runningPrimary ? multiModeEngine.PrimaryEngine : multiModeEngine.SecondaryEngine;
            else if (GTI_MultiModeEngineFX != null)
                currentModuleEngineFx = (ModuleEnginesFX)currentModuleEngineFieldInfo?.GetValue(GTI_MultiModeEngineFX);

            if (currentModuleEngineFx != null)
                currentEngine = moduleEngines.FirstOrDefault(m => m.engine.engineID == currentModuleEngineFx.engineID);
        }



        /// <summary>
        /// Updates the persisted throttle value with the current vessel throttle.
        /// Queued to avoid the field being zeroed in the realtime -> warp transition frame.
        /// </summary>
        private void UpdatePersistentThrottle()
        {
            _mainThrottleQueue.Enqueue(vessel.ctrlState.mainThrottle);
            if (_mainThrottleQueue.Count > HighLogic.CurrentGame.Parameters.CustomParams<PTDevSettings>().queueLength)
                _mainThrottleQueue.Dequeue();
            persistentThrottle = _mainThrottleQueue.Max();
        }



        /// <summary>
        /// Updates the persisted Isp value with the current (active) engine Isp.
        /// Queued to avoid the field being zeroed in the realtime -> warp transition frame.
        /// </summary>
        private void UpdateCurrentEnginePersistentIsp()
        {
            currentEngine.ispQueue.Enqueue(currentEngine.engine.realIsp);
            if (currentEngine.ispQueue.Count > HighLogic.CurrentGame.Parameters.CustomParams<PTDevSettings>().queueLength)
                currentEngine.ispQueue.Dequeue();
            currentEngine.persistentIsp = currentEngine.ispQueue.Max();
        }



        private void UpdatePropellantReqMetFactorQueue()
        {
            if (currentEngine.propellantReqMetFactor > 0 && persistentThrottle > 0)
            {
                currentEngine.propellantReqMetFactorQueue.Enqueue(currentEngine.engineHasAnyMassLessPropellants
                    ? Mathf.Pow(currentEngine.propellantReqMetFactor, 1 / fudgeExponent)
                    : currentEngine.propellantReqMetFactor);

                if (currentEngine.propellantReqMetFactorQueue.Count > HighLogic.CurrentGame.Parameters.CustomParams<PTDevSettings>().propellantReqMetFactorQueueSize)
                    currentEngine.propellantReqMetFactorQueue.Dequeue();
            }
            else
                currentEngine.propellantReqMetFactorQueue.Clear();
        }



        private PersistentPropellant LoadPropellantAvailability(PersistentPropellant currentPersistentPropellant)
        {
            var activePropellant = currentPersistentPropellant;

            List<PersistentPropellant> activePropellants = _persistentEngines.SelectMany(pe => pe.moduleEngines.SelectMany(pl =>
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



        private void ReloadPropellantsWithoutMasslessPropellants()
        {
            var akPropellants = new ConfigNode();

            //Get the Ignition state, i.e. is the moduleEngine shutdown or activated
            bool ignitionState = currentEngine.engine.getIgnitionState;

            currentEngine.engine.Shutdown();

            foreach (var propellant in currentEngine.propellants)
            {
                if (propellant.density == 0)
                    continue;

                akPropellants.AddNode(PersistentPropellant.LoadPropellant(propellant.propellant.name, propellant.propellant.ratio));
            }

            currentEngine.engine.Load(akPropellants);

            if (ignitionState)
                currentEngine.engine.Activate();
        }



        private void UpdateMasslessPropellant()
        {
            PersistentPropellant masslessPropellant = isMultiMode
                ? currentEngine.propellants.FirstOrDefault(m => m.density == 0)
                : moduleEngines.SelectMany(m => m.propellants.Where(p => p.density == 0)).FirstOrDefault();

            if (masslessPropellant != null)
            {
                masslessUsageField.guiActive = true;
                masslessUsageField.guiName = masslessPropellant.definition.displayName;

                masslessUsage = (isMultiMode
                    ? currentEngine.propellants.Sum(m => m.demandOut)
                    : moduleEngines.Sum(m => m.propellants.Sum(l => l.demandOut))) / TimeWarp.fixedDeltaTime;
            }
            else
                masslessUsageField.guiActive = false;
        }



        /// <summary>
        /// Consumes (or simulates consuming) an amount of propellant resource based on calculated demand.
        /// </summary>
        /// <returns> The amount of resource that was consumed. </returns>
        private double RequestResource(PersistentPropellant propellant, double demand, bool simulate = false)
        {
            if (!DetectKerbalism.Found())
                return part.RequestResource(propellant.definition.id, demand, propellant.propellant.GetFlowMode(), simulate || (propellant.density > 0 && !vessel.packed));

            _availablePartResources.TryGetValue(propellant.definition.name, out double availableAmount);

            if (simulate)
                _availablePartResources[propellant.definition.name] = Math.Max(0, availableAmount - demand);
            else
            {
                _kerbalismResourceChangeRequest.TryGetValue(propellant.definition.name, out double currentDemand);
                _kerbalismResourceChangeRequest[propellant.definition.name] = currentDemand - (demand / TimeWarp.fixedDeltaTime);
            }

            return Math.Min(availableAmount, demand);
        }

        private void RestoreMaxFuelFlow()
        {
            //currentEngine.engine.maxFuelFlow = (float)(currentEngine.persistentIsp > 0 ? currentEngine.engine.maxThrust / (currentEngine.persistentIsp * PhysicsGlobals.GravitationalAcceleration) : 1e-10f);
            currentEngine.engine.multFlow = 1;
        }

        private void RestoreHeadingAtLoad()
        {
            // restore heading at load
            if (HasPersistentHeadingEnabled && fixedUpdateCount++ <= 60 && vesselAlignmentWithAutopilotMode > 0.995)
            {
                vessel.Autopilot.SetMode(persistentAutopilotMode);
                vessel.PersistHeading(TimeWarp.fixedDeltaTime, HighLogic.CurrentGame.Parameters.CustomParams<PTDevSettings>().headingTolerance, vesselChangedSoiCountdown > 0);
            }
            else
                persistentAutopilotMode = vessel.Autopilot.Mode;
        }

        private void ResetMonitoringVariables()
        {
            // reset monitoring variables
            foreach (var persistentEngineModule in moduleEngines)
            {
                persistentEngineModule.propellants.ForEach(m =>
                {
                    m.demandIn = 0;
                    m.demandOut = 0;
                });
                persistentEngineModule.finalThrust = 0;
            }
        }

        private void CollectStatistics()
        {
            // display final thrust in a user friendly way
            persistentThrust = isMultiMode ? currentEngine.finalThrust : moduleEngines.Sum(m => m.finalThrust);

            persistentIsp = isMultiMode
                ? currentEngine.persistentIsp
                : persistentThrust > 0 
                    ? (float)(moduleEngines.Sum(m => m.finalThrust * m.persistentIsp) / persistentThrust) 
                    : persistentIsp;

            // store current fixedDeltaTime for comparison
            previousFixedDeltaTime = TimeWarp.fixedDeltaTime;
        }

        #endregion


        #region Kerbalism

        /// <summary>
        /// Called by Kerbalism for all part modules of all unloaded vessels.
        /// </summary>
        /// <param name="vessel">the vessel (unloaded)</param>
        /// <param name="part_snapshot">proto part snapshot (contains all non-persistant KSPFields)</param>
        /// <param name="module_snapshot">proto part module snapshot (contains all non-persistant KSPFields)</param>
        /// <param name="proto_part_module">proto part module snapshot (contains all non-persistant KSPFields)</param>
        /// <param name="proto_part">proto part snapshot (contains all non-persistant KSPFields)</param>
        /// <param name="availableResources">key-value pair containing all available resources and their currently available amount on the vessel. if the resource is not in there, it's not available</param>
        /// <param name="resourceChangeRequest">key-value pair that contains the resource names and the units per second that you want to produce/consume (produce: positive, consume: negative)</param>
        /// <param name="elapsed_s">how much time elapsed since the last time. note this can be very long, minutes and hours depending on warp speed</param>
        /// <returns>the title to be displayed in the resource tooltip</returns>
        public static string BackgroundUpdate(
            Vessel vessel,
            ProtoPartSnapshot part_snapshot,
            ProtoPartModuleSnapshot module_snapshot,
            PartModule proto_part_module,
            Part proto_part,
            Dictionary<string, double> availableResources,
            List<KeyValuePair<string, double>> resourceChangeRequest,
            double elapsed_s)
        {
            double persistentThrust = 0;
            if (!module_snapshot.moduleValues.TryGetValue(nameof(persistentThrust), ref persistentThrust))
                return proto_part.partInfo.title;

            // ignore background update when no thrust generated
            if (persistentThrust <= 0)
                return proto_part.partInfo.title;

            double vesselAlignmentWithAutopilotMode = 0;
            if (!module_snapshot.moduleValues.TryGetValue(nameof(vesselAlignmentWithAutopilotMode), ref vesselAlignmentWithAutopilotMode))
                return proto_part.partInfo.title;

            // ignore background update when not aligned with autopilot mode
            if (vesselAlignmentWithAutopilotMode < 0.995)
                return proto_part.partInfo.title;

            string persistentResourceChange = module_snapshot.moduleValues.GetValue(nameof(persistentResourceChange));

            if (string.IsNullOrEmpty(persistentResourceChange))
                return proto_part.partInfo.title;

            Dictionary<string, double> resourceChanges = persistentResourceChange
                .Split(';').Select(s => s.Trim().Split('='))
                .ToDictionary(a => a[0], a => double.Parse(a[1]));

            if (resourceChanges.Any(m => m.Value == 0))
                return proto_part.partInfo.title;

            VesselAutopilot.AutopilotMode persistentAutopilotMode = (VesselAutopilot.AutopilotMode) Enum.Parse(
                typeof(VesselAutopilot.AutopilotMode), module_snapshot.moduleValues.GetValue(nameof(persistentAutopilotMode)));

            Orbit orbit = vessel.GetOrbit();
            double UT = Planetarium.GetUniversalTime();
            Vector3d orbitalVelocityAtUt = orbit.getOrbitalVelocityAtUT(UT).xzy;

            Vector3d thrustVector = Vector3d.zero;
            switch (persistentAutopilotMode)
            {
                case VesselAutopilot.AutopilotMode.Prograde:
                    thrustVector = orbitalVelocityAtUt;
                    break;
                case VesselAutopilot.AutopilotMode.Retrograde:
                    thrustVector = -orbitalVelocityAtUt;
                    break;
                case VesselAutopilot.AutopilotMode.Normal:
                    thrustVector = Vector3.Cross(orbitalVelocityAtUt, (orbit.getPositionAtUT(UT) - vessel.mainBody.getPositionAtUT(UT) ));
                    break;
                case VesselAutopilot.AutopilotMode.Antinormal:
                    thrustVector = -Vector3.Cross(orbitalVelocityAtUt, (orbit.getPositionAtUT(UT) - vessel.mainBody.getPositionAtUT(UT)));
                    break;
                case VesselAutopilot.AutopilotMode.RadialIn:
                    thrustVector = -Vector3.Cross(orbitalVelocityAtUt, Vector3.Cross(orbitalVelocityAtUt, vessel.orbit.getPositionAtUT(UT) - orbit.referenceBody.position));
                    break;
                case VesselAutopilot.AutopilotMode.RadialOut:
                    thrustVector = Vector3.Cross(orbitalVelocityAtUt, Vector3.Cross(orbitalVelocityAtUt, vessel.orbit.getPositionAtUT(UT) - orbit.referenceBody.position));
                    break;
                case VesselAutopilot.AutopilotMode.Target:
                    thrustVector = GetThrustVectorToTarget(vessel, module_snapshot, UT);
                    break;
                case VesselAutopilot.AutopilotMode.AntiTarget:
                    thrustVector = -GetThrustVectorToTarget(vessel, module_snapshot, UT);
                    break;
                //case VesselAutopilot.AutopilotMode.Maneuver:
                //    thrustVector = orbit.GetThrustVectorToManeuver(module_snapshot);
                //    break;
            }

            if (thrustVector == Vector3d.zero)
                return proto_part.partInfo.title;

            double fuelRequirementMet = 1;
            foreach (var resourceChange in resourceChanges)
            {
                var fixedRequirement = -resourceChange.Value * elapsed_s;

                if (availableResources.TryGetValue(resourceChange.Key, out double availableAmount))
                    fuelRequirementMet = availableAmount > 0 && fixedRequirement > 0 ? Math.Min(fuelRequirementMet, availableAmount / fixedRequirement) : 0;
                else
                    fuelRequirementMet = 0;
            }

            if (fuelRequirementMet > 0)
            {
                foreach (var resourceChange in resourceChanges)
                {
                    resourceChangeRequest.Add(new KeyValuePair<string, double>(resourceChange.Key, resourceChange.Value * fuelRequirementMet));
                }

                double vesselDryMass = 0;
                if (!module_snapshot.moduleValues.TryGetValue(nameof(vesselDryMass), ref vesselDryMass))
                    return proto_part.partInfo.title;

                double persistentAverageDensity = 0;
                if (!module_snapshot.moduleValues.TryGetValue(nameof(persistentAverageDensity), ref persistentAverageDensity))
                    return proto_part.partInfo.title;

                float persistentIsp = 0;
                if (!module_snapshot.moduleValues.TryGetValue(nameof(persistentIsp), ref persistentIsp))
                    return proto_part.partInfo.title;

                var vesselMass = vesselDryMass += Utils.GetResourceMass(availableResources);

                Vector3d deltaVVector = Utils.CalculateDeltaVVector(persistentAverageDensity, vesselMass, elapsed_s, persistentThrust * fuelRequirementMet, persistentIsp, thrustVector.normalized);

                orbit.Perturb(deltaVVector, UT);
            }

            return proto_part.partInfo.title;
        }

        private static Vector3d GetThrustVectorToTarget(Vessel vessel, ProtoPartModuleSnapshot module_snapshot, double UT)
        {
            string persistentVesselTargetBodyName = string.Empty;
            module_snapshot.moduleValues.TryGetValue(nameof(persistentVesselTargetBodyName), ref persistentVesselTargetBodyName);

            string persistentVesselTargetId = Guid.Empty.ToString();
            module_snapshot.moduleValues.TryGetValue(nameof(persistentVesselTargetId), ref persistentVesselTargetId);

            Guid persistentVesselTargetGuid = new Guid(persistentVesselTargetId);

            Orbit targetOrbit = null;
            if (persistentVesselTargetGuid != Guid.Empty)
            {
                Vessel targetVessel = FlightGlobals.Vessels.SingleOrDefault(m => m.id == persistentVesselTargetGuid);
                if (targetVessel != null)
                    targetOrbit = targetVessel.GetOrbit();
            }
            else if (!string.IsNullOrEmpty(persistentVesselTargetBodyName))
            {
                CelestialBody body = FlightGlobals.Bodies.SingleOrDefault(m => m.bodyName == persistentVesselTargetBodyName);
                if (body != null)
                    targetOrbit = body.GetOrbit();
            }

            return targetOrbit != null ? targetOrbit.getPositionAtUT(UT) - vessel.orbit.getPositionAtUT(UT) : Vector3d.zero;
        }

        /// <summary>
        /// Called by Kerbalism every frame. Uses their resource system when Kerbalism is installed.
        /// </summary>
        public virtual string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
        {
            _availablePartResources = availableResources;

            resourceChangeRequest.Clear();

            foreach (var resourceRequest in _kerbalismResourceChangeRequest)
            {
                var definition = PartResourceLibrary.Instance.GetDefinition(resourceRequest.Key);
                if (definition == null || definition.density > 0 && !vessel.packed)
                    continue;

                resourceChangeRequest.Add(new KeyValuePair<string, double>(resourceRequest.Key, resourceRequest.Value));
            }

            return part.partInfo.title;
        }

        #endregion
    }
}
