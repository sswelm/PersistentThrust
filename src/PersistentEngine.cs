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
        public bool RequestPropMassless = false;
        // Flag whether to request resources with mass
        public bool RequestPropMass = true;

        // GUI
        // Enable/disable persistent engine features
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Persistent"), UI_Toggle(disabledText = "Disabled", enabledText = "Enabled")]
        public bool PersistentEnabled = true;

        public string powerEffectName;
        public string runningEffectName;

        private double ratioHeadingVersusRequest;

        // Engine module on the same part
        public ModuleEngines engine;
        public ModuleEnginesFX engineFX;

        // Persistent values to use during timewarp
        public double ThrustPersistent = 0;
        public float ThrottlePersistent = 0;

        // Are we transitioning from timewarp to reatime?
        bool warpToReal = false;

        int vesselChangedSIOCountdown = 0;

        // Propellant data
        public List<PersistentPropellant> pplist;
        // Average density of propellants
        public double densityAverage;

        public void VesselChangedSOI()
        {
            vesselChangedSIOCountdown = 10;
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
            // Update values to use during timewarp
            // Get throttle
            ThrottlePersistent = vessel.ctrlState.mainThrottle;
            // Get final thrust
            ThrustPersistent = engine.getIgnitionState ? engine.finalThrust : 0;
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
            foundRatio = 1;
            var demandsOut = new double[pplist.Count];
            for (var i = 0; i < pplist.Count; i++)
            {
                var pp = pplist[i];
                // Request resources if:
                // - resource has mass & request mass flag true
                // - resource massless & request massless flag true
                if ((pp.density > 0 && RequestPropMass) || (pp.density == 0 && RequestPropMassless))
                {
                    var demandIn = demands[i];
                    var demandOut = IsInfinite(pp.propellant) ? demandIn : part.RequestResource(pp.propellant.id, demandIn);
                    demandsOut[i] = demandOut;
                    // Test if resource depleted
                    // TODO test if resource partially depleted: demandOut < demands[i]
                    // For the moment, just let the full deltaV for time segment dT be applied
                    if (demandOut < demandIn)
                    {
                        var propellantFountRatio = demandOut / demandIn;
                        if (propellantFountRatio < foundRatio)
                            foundRatio = propellantFountRatio;
                        Debug.Log(String.Format("[PersistentThrust] - Part {0} failed to request {1} {2}", part.name, demands[i], pp.propellant.name));
                    }
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

            if (!String.IsNullOrEmpty(powerEffectName))
            {
                var powerEffectRatio = engine.maxThrust > 0 ? currentThust / engine.maxThrust : 0;
                part.Effect(powerEffectName, (float)powerEffectRatio);
            }

            if (!String.IsNullOrEmpty(runningEffectName))
            {
                var runningEffectRatio = engine.maxThrust > 0 ? currentThust / engine.maxThrust : 0;
                part.Effect(runningEffectName, (float)runningEffectRatio);
            }
        }

        // Physics update
        public void FixedUpdate() // FixedUpdate is also called while not staged
        {
            if (FlightGlobals.fetch == null || !isEnabled ) return;

            if (vesselChangedSIOCountdown > 0)
                vesselChangedSIOCountdown--;

            // Realtime mode
            if (!this.vessel.packed)
            {
                UpdateFX(engine.GetCurrentThrust());

                TimeWarp.GThreshold = 12;

                // Update persistent thrust parameters if NOT transitioning from warp to realtime
                if (!warpToReal)
                    UpdatePersistentParameters();

                ratioHeadingVersusRequest = 0;
            }
            else
            {
                ThrustPersistent = engine.getIgnitionState ? (float)(engine.requestedMassFlow * PhysicsGlobals.GravitationalAcceleration * engine.realIsp) : 0;

                if (engine.currentThrottle > 0 && IsPersistentEngine && PersistentEnabled && ThrustPersistent > 0.0000005)
                {
                    warpToReal = true; // Set to true for transition to realtime

                    ratioHeadingVersusRequest = engine.PersistHeading(vesselChangedSIOCountdown > 0, ratioHeadingVersusRequest == 1);
                    if (ratioHeadingVersusRequest != 1)
                    {
                        ThrustPersistent = 0;
                        return;
                    }

                    var UT = Planetarium.GetUniversalTime(); // Universal time
                    var thrustUV = this.part.transform.up; // Thrust direction unit vector
                    // Calculate deltaV vector & resource demand from propellants with mass
                    double demandMass;
                    // Calculate deltaV vector & resource demand from propellants with mass
                    var deltaVV = CalculateDeltaVV(this.vessel.totalMass, TimeWarp.fixedDeltaTime, ThrustPersistent, engine.realIsp, thrustUV, out demandMass);
                    // Calculate resource demands
                    var fuelDemands = CalculateDemands(demandMass);
                    // Apply resource demands & test for resource depletion
                    double foundRatio = 1;
                    var demandsOut = ApplyDemands(fuelDemands, ref foundRatio);

                    // Apply deltaV vector at UT & dT to orbit if resources not depleted
                    if (foundRatio > 0)
                        vessel.orbit.Perturb(deltaVV * foundRatio, UT);

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
                    ratioHeadingVersusRequest = engine.PersistHeading(vesselChangedSIOCountdown > 0);
                    UpdateFX(0);
                }
            }

            // Update display numbers
            thrust_d = ThrustPersistent;
        }
    }
}
