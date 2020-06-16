using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PersistentThrust
{
    public class PersistentEngine : PartModule
    {
        // GUI
        [KSPField(guiActive = true, guiName = "#autoLOC_6001377", guiUnits = "#autoLOC_7001408", guiFormat = "F6")]
        public double thrust_d;
        // Enable/disable persistent engine features
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_PT_PersistentThrust"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889")]
        public bool HasPersistentThrust = true;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_PT_PersistentHeading"), UI_Toggle(disabledText = "#autoLOC_900890", enabledText = "#autoLOC_900889")]
        public bool HasPersistentHeadingEnabled = true;


        [KSPField]
        int queueLength = 2;

        // Flag if using PersistentEngine features
        public bool IsPersistentEngine = false;
        // Flag whether to request massless resources
        public bool RequestPropMassless = true;
        // Flag whether to request resources with mass
        public bool RequestPropMass = true;

        public string powerEffectName;
        public string runningEffectName;

        [KSPField(guiActive = false, guiFormat = "F6")]
        public double propellantReqMet;
        [KSPField(guiActive = false, guiFormat = "F6")]
        public double fudgedPropellantReqMet;

        public double ratioHeadingVersusRequest;

        // Engine module on the same part
        public ModuleEngines engine;
        public ModuleEnginesFX engineFX;

        // Persistent values to use during timewarp
        public double ThrustPersistent = 0;
        public float ThrottlePersistent = 0;
        public float IspPersistent = 0;

        // Are we transitioning from timewarp to reatime?
        public bool warpToReal = false;

        public int vesselChangedSOICountdown = 0;
        public int missingPowerCountdown = 0;

        // Propellant data
        public List<PersistentPropellant> pplist;
        // Average density of propellants
        public double densityAverage;

        private Queue<double> propellantReqMetQueue = new Queue<double>(1000);

        private Queue<float> throttleQueue = new Queue<float>();
        private Queue<float> thrustQueue = new Queue<float>();
        private Queue<float> ispQueue = new Queue<float>();

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
                if (String.IsNullOrEmpty(powerEffectName))
                    powerEffectName = engineFX.powerEffectName;

                engineFX.powerEffectName = "";

                if (String.IsNullOrEmpty(runningEffectName))
                    runningEffectName = engineFX.runningEffectName;

                engineFX.runningEffectName = "";
            }
        }

        // Update
        public override void OnUpdate()
        {
            if (!IsPersistentEngine || !HasPersistentThrust) return;

            TimeWarp.GThreshold = 12f;

            // stop engines and drop out of timewarp when X pressed
            if (vessel.packed && ThrottlePersistent > 0 && Input.GetKeyDown(KeyCode.X))
            {
                // Return to realtime
                TimeWarp.SetRate(0, true);

                ThrottlePersistent = 0;
                vessel.ctrlState.mainThrottle = ThrottlePersistent;
                Debug.Log("[PersistentThrust]: PersistentThrust canceled after " + KeyCode.X + " pressed" );
            }

            // When transitioning from timewarp to real update throttle
            if (warpToReal)
            {
                vessel.ctrlState.mainThrottle = ThrottlePersistent;
                warpToReal = false;
            }

            // hide stock thrust
            engine.Fields["finalThrust"].guiActive = false;
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

        void UpdatePersistentParameters()
        {
            throttleQueue.Enqueue(vessel.ctrlState.mainThrottle);
            if (throttleQueue.Count > queueLength)
                throttleQueue.Dequeue();
            ThrottlePersistent = throttleQueue.Max();

            thrustQueue.Enqueue(engine.getIgnitionState ? engine.finalThrust : 0);
            if (thrustQueue.Count > queueLength)
                thrustQueue.Dequeue();
            ThrustPersistent = thrustQueue.Max();

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
                    demands[i] = pplist[i].Demand(demandMass);
                }
            }
            return demands;
        }

        // Apply demanded resources & return results
        // Updated depleted boolean flag if resource request failed
        public virtual double[] ApplyDemands(double[] demands, ref double foundRatio)
        {
            double propellantReqMet = 1;

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

                    var demandOut = IsInfinite(pp.propellant) ? demandIn : part.RequestResource(pp.propellant.id, demandIn, pp.propellant.GetFlowMode(), true);

                    // Test if resource depleted
                    // TODO test if resource partially depleted: demandOut < demands[i]
                    // For the moment, just let the full deltaV for time segment dT be applied
                    if (demandOut < demandIn && demandIn > 0)
                    {
                        var propellantFoundRatio = demandOut / demandIn;
                        if (propellantFoundRatio < propellantReqMet)
                            propellantReqMet = propellantFoundRatio;

                        if (pp.propellant.resourceDef.density > 0)
                        {
                            // reset stabilize Queue when out of mass propellant
                            if (propellantFoundRatio < 0.1)
                                propellantReqMetQueue.Clear();
                        }
                        else
                        {
                            if (propellantFoundRatio == 0)
                            {
                                // reset stabilize Queue when out power for too long
                                if (missingPowerCountdown <= 0)
                                    propellantReqMetQueue.Clear();
                                missingPowerCountdown--;
                            }
                            else
                                missingPowerCountdown = 10;
                        }
                    }
                }
            }

            // attempt to stabilize thrust output with First In Last Out Queue 
            propellantReqMetQueue.Enqueue(propellantReqMet);
            if (propellantReqMetQueue.Count() > 1000)
                propellantReqMetQueue.Dequeue();
            foundRatio = propellantReqMetQueue.Average();

            // secondly we can consume the resource based on propellant availability
            for (var i = 0; i < pplist.Count; i++)
            {
                var pp = pplist[i];
                // Request resources if:
                // - resource has mass & request mass flag true
                // - resource massless & request massless flag true
                if ((pp.density > 0 && RequestPropMass) || (pp.density == 0 && RequestPropMassless))
                {
                    var demandIn = demands[i];
                    var demandOut = IsInfinite(pp.propellant) ? demandIn : part.RequestResource(pp.propellant.id, propellantReqMet * demandIn, pp.propellant.GetFlowMode(), false);
                    demandsOut[i] = demandOut;
                }
                // Otherwise demand is 0
                else
                    demandsOut[i] = 0;
            }
            // Return demand outputs
            return demandsOut;
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

        // Physics update
        public void FixedUpdate() // FixedUpdate is also called while not staged
        {
            if (this.vessel is null || !isEnabled) return;

            if (vesselChangedSOICountdown > 0)
                vesselChangedSOICountdown--;

            // Realtime mode
            if (!this.vessel.packed)
            {
                UpdateFX(engine.GetCurrentThrust());

                // Update persistent thrust parameters if NOT transitioning from warp to realtime
                if (!warpToReal)
                    UpdatePersistentParameters();

                ratioHeadingVersusRequest = 0;

                if (engine.propellantReqMet > 0)
                {
                    missingPowerCountdown = 10;
                    propellantReqMetQueue.Enqueue(Math.Pow(engine.propellantReqMet * 0.01, 1/0.267));
                    if (propellantReqMetQueue.Count > 1000)
                        propellantReqMetQueue.Dequeue();
                    propellantReqMet = propellantReqMetQueue.Average();
                }
                else
                    propellantReqMetQueue.Clear();

                thrust_d = ThrustPersistent;
            }
            else
            {
                if (ThrottlePersistent > 0 && IsPersistentEngine && HasPersistentThrust)
                {
                    warpToReal = true; // Set to true for transition to realtime

                    if (HasPersistentHeadingEnabled)
                    {
                        ratioHeadingVersusRequest = engine.PersistHeading(vesselChangedSOICountdown > 0, ratioHeadingVersusRequest == 1);
                        if (ratioHeadingVersusRequest != 1)
                        {
                            thrust_d = 0;
                            return;
                        }
                    }

                    var UT = Planetarium.GetUniversalTime(); // Universal time
                    var thrustUV = this.part.transform.up; // Thrust direction unit vector
                    // Calculate deltaV vector & resource demand from propellants with mass
                    double demandMass;
                    // Calculate deltaV vector & resource demand from propellants with mass
                    var deltaVV = CalculateDeltaVV(this.vessel.totalMass, TimeWarp.fixedDeltaTime, ThrottlePersistent * engine.maxThrust, IspPersistent, thrustUV, out demandMass);
                    // Calculate resource demands
                    var fuelDemands = CalculateDemands(demandMass);
                    // Apply resource demands & test for resource depletion
                    var demandsOut = ApplyDemands(fuelDemands, ref propellantReqMet);

                    // normalize thrust similary to stock
                    fudgedPropellantReqMet = propellantReqMet > 0 ?  Math.Pow(propellantReqMet, 0.267) : 0;

                    // Apply deltaV vector at UT & dT to orbit if resources not depleted
                    if (fudgedPropellantReqMet > 0)
                    {
                        thrust_d = engine.maxThrust * ThrottlePersistent * fudgedPropellantReqMet;
                        vessel.orbit.Perturb(deltaVV * fudgedPropellantReqMet, UT);
                    }

                    // Otherwise log warning and drop out of timewarp if throttle on & depleted
                    else if (ThrottlePersistent > 0)
                    {
                        thrust_d = 0;
                        Debug.Log("[PersistentThrust]: Thrust warp stopped - propellant depleted");
                        ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_PT_StoppedDepleted"), 5.0f, ScreenMessageStyle.UPPER_CENTER);
                        // Return to realtime
                        TimeWarp.SetRate(0, true);
                    }
                    else
                        thrust_d = 0;

                    UpdateFX(ThrustPersistent * propellantReqMet);
                }
                else
                {
                    if (HasPersistentHeadingEnabled)
                        ratioHeadingVersusRequest = engine.PersistHeading(vesselChangedSOICountdown > 0);
                    UpdateFX(0);
                }
            }
        }
    }
}
