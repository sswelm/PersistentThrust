using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PersistentThrust 
{
    public class PersistentEngine : PartModule
    {
        [KSPField(guiActive = true, guiName = "#autoLOC_6001377", guiUnits = "#autoLOC_7001408", guiFormat = "F6")]
        public double thrust_d;

        // Flag to activate force if it isn't to allow overriding stage activation
        [KSPField(isPersistant = true)]
        bool IsForceActivated;
        // Flag if using PersistentEngine features
        public bool IsPersistentEngine = false;
        // Flag whether to request massless resources
        public bool RequestPropMassless = true;
        // Flag whether to request resources with mass
        public bool RequestPropMass = true;

        // GUI
        // Enable/disable persistent engine features
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Persistent"), UI_Toggle(disabledText = "Disabled", enabledText = "Enabled")]
        public bool PersistentEnabled = true;

        public string powerEffectName;
        public string runningEffectName;

        public double foundRatio;
        private double ratioHeadingVersusRequest;

        // Engine module on the same part
        public ModuleEngines engine;
        public ModuleEnginesFX engineFX;

        // Persistent values to use during timewarp
        public double ThrustPersistent = 0;
        public float ThrottlePersistent = 0;
        public float IspPersistent = 0;

        // Are we transitioning from timewarp to reatime?
        public bool warpToReal = false;

        // Keep track of number of physics ticks skipped
        public int skipCounter = 0;

        public int vesselChangedSOICountdown = 0;
        public int missingPowerCountdown = 0;

        // Propellant data
        public List<PersistentPropellant> pplist;
        // Average density of propellants
        public double densityAverage;

        public Queue<double> foundRatioQueue = new Queue<double>(100);

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
            if (!IsPersistentEngine || !PersistentEnabled) return;

            TimeWarp.GThreshold = 12f;

            // stop engines and drop out of timewarp when X pressed
            if (vessel.packed && ThrottlePersistent > 0 && Input.GetKeyDown(KeyCode.X))
            {
                // Return to realtime
                TimeWarp.SetRate(0, true);

                ThrottlePersistent = 0;
                vessel.ctrlState.mainThrottle = ThrottlePersistent;
                Debug.Log("[PersistentThrust] - PersistentThrust canceled after " + KeyCode.X + " pressed" );
            }

            // When transitioning from timewarp to real update throttle
            if (warpToReal)
            {
                vessel.ctrlState.mainThrottle = ThrottlePersistent;
                warpToReal = false;
            }

            // Activate force if engine is enabled and operational
            if (!IsForceActivated && engine.isEnabled && engine.isOperational)
            {
                IsForceActivated = true;
                part.force_activate();
                Debug.Log("[PersistentThrust] - ForceActivated");
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
            // skip some ticks
            if (skipCounter++ < 15) return;

            // we are on the 16th tick
            skipCounter = 0;
            // Update values to use during timewarp
            // Get throttle
            ThrottlePersistent = vessel.ctrlState.mainThrottle;
            // Get final thrust
            ThrustPersistent = engine.getIgnitionState ? engine.finalThrust : 0;
            IspPersistent = engine.realIsp;
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
            double currentFoundRatio = 1;

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
                        if (propellantFoundRatio < currentFoundRatio)
                            currentFoundRatio = propellantFoundRatio;

                        if (pp.propellant.resourceDef.density > 0)
                        {
                            if (propellantFoundRatio < 0.1)
                                foundRatioQueue.Clear();
                        }
                        else
                        {
                            if (propellantFoundRatio == 0)
                            {
                                if (missingPowerCountdown <= 0)
                                    foundRatioQueue.Clear();
                                missingPowerCountdown--;
                            }
                            else
                                missingPowerCountdown = 10;
                        }
                    }
                }
            }

            foundRatioQueue.Enqueue(currentFoundRatio);
            if (foundRatioQueue.Count() > 100)
                foundRatioQueue.Dequeue();
            foundRatio = foundRatioQueue.Average();

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
                    var demandOut = IsInfinite(pp.propellant) ? demandIn : part.RequestResource(pp.propellant.id, currentFoundRatio * demandIn, pp.propellant.GetFlowMode(), false);
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
                part.Effect(powerEffectName, (exhaustRatio));

            if (!String.IsNullOrEmpty(runningEffectName))
                part.Effect(runningEffectName, (exhaustRatio));
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
                    foundRatioQueue.Enqueue(engine.propellantReqMet * 0.01);
                    if (foundRatioQueue.Count > 100)
                        foundRatioQueue.Dequeue();
                    foundRatio = foundRatioQueue.Average();
                }
                else
                    foundRatioQueue.Clear();
            }
            else
            {
                ThrustPersistent = engine.getIgnitionState ? (float)(ThrottlePersistent * engine.maxFuelFlow * PhysicsGlobals.GravitationalAcceleration * IspPersistent) : 0;

                if (ThrottlePersistent > 0 && IsPersistentEngine && PersistentEnabled)
                {
                    warpToReal = true; // Set to true for transition to realtime

                    ratioHeadingVersusRequest = engine.PersistHeading(vesselChangedSOICountdown > 0, ratioHeadingVersusRequest == 1);
                    if (ratioHeadingVersusRequest != 1)
                    {
                        ThrustPersistent = 0;
                        thrust_d = 0;
                        return;
                    }

                    var UT = Planetarium.GetUniversalTime(); // Universal time
                    var thrustUV = this.part.transform.up; // Thrust direction unit vector
                    // Calculate deltaV vector & resource demand from propellants with mass
                    double demandMass;
                    // Calculate deltaV vector & resource demand from propellants with mass
                    var deltaVV = CalculateDeltaVV(this.vessel.totalMass, TimeWarp.fixedDeltaTime, ThrustPersistent, IspPersistent, thrustUV, out demandMass);
                    // Calculate resource demands
                    var fuelDemands = CalculateDemands(demandMass);
                    // Apply resource demands & test for resource depletion
                    var demandsOut = ApplyDemands(fuelDemands, ref foundRatio);

                    // normalize thrust similary to stock
                    foundRatio = foundRatio > 0 ? (foundRatio + 1d) / 2d : 0;

                    // Apply deltaV vector at UT & dT to orbit if resources not depleted
                    if (foundRatio > 0)
                    {
                        ThrustPersistent *= foundRatio;
                        vessel.orbit.Perturb(deltaVV * foundRatio, UT);
                    }

                    // Otherwise log warning and drop out of timewarp if throttle on & depleted
                    else if (ThrottlePersistent > 0)
                    {
                        ThrustPersistent = 0;
                        Debug.Log("[PersistentThrust] Thrust warp stopped - propellant depleted");
                        ScreenMessages.PostScreenMessage("Thrust warp stopped - propellant depleted", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                        // Return to realtime
                        TimeWarp.SetRate(0, true);
                    }

                    UpdateFX(ThrustPersistent * foundRatio);
                }
                else
                {
                    ratioHeadingVersusRequest = engine.PersistHeading(vesselChangedSOICountdown > 0);
                    UpdateFX(0);
                }
            }

            // Update display numbers
            thrust_d = ThrustPersistent;
        }
    }
}
