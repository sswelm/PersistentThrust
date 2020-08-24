using System;
using System.Collections.Generic;
using UnityEngine;

namespace PersistentThrust.BackgroundProcessing
{
    public class PersistentProcessingVesselModule : VesselModule
    {
        // KSP doesn't save Vessel AutopilotMode so we have to do it ourselves
        [KSPField(isPersistant = true)]
        public VesselAutopilot.AutopilotMode persistentAutopilotMode;
        [KSPField(isPersistant = true)]
        public string persistentVesselTargetBodyName;
        [KSPField(isPersistant = true)]
        public string persistentVesselTargetId = Guid.Empty.ToString();
        [KSPField(isPersistant = true)]
        public double persistentManeuverUT;
        [KSPField(isPersistant = true)]
        public string persistentManeuverNextPatch;
        [KSPField(isPersistant = true)]
        public string persistentManeuverPatch;

        public GameScenes linkedScene;

        public float headingTolerance = 0.002f;

        //List of scenes where we shouldn't run the mod. I toyed with runOnce, but couldn't get it working
        private static readonly List<GameScenes> forbiddenScenes = new List<GameScenes> { GameScenes.LOADING, GameScenes.LOADINGBUFFER, GameScenes.CREDITS, GameScenes.MAINMENU, GameScenes.SETTINGS };

        private static bool _initialized;
        private static int fixedUpdateCount = 0;

        //protected override void OnAwake()
        //{
        //    base.OnAwake();
        //}

        ////Fired when the mod loads each scene
        //public void Awake()
        //{
        //    //If we're in the MainMenu, don't do anything
        //    if (forbiddenScenes.Contains(HighLogic.LoadedScene))
        //        return;
        //}

        //Fired when the mod loads each scene
        protected override void OnStart()
        {
            base.OnStart();

            linkedScene = HighLogic.LoadedScene;

            //If we're in the MainMenu, don't do anything
            if (forbiddenScenes.Contains(HighLogic.LoadedScene))
                return;

            if (!_initialized)
            {
                GameEvents.onVesselSOIChanged.Add(OmVesselSOIChanged);
                //GameEvents.onGameStateLoad.Add(onGameStateLoadEvent);
                //GameEvents.onGameSceneLoadRequested.Add(GameSceneLoadEvent);
                //GameEvents.onVesselWillDestroy.Add(VesselDestroyEvent);

                _initialized = true;
            }

            fixedUpdateCount = 0;
        }

        //protected override void OnLoad(ConfigNode node)
        //{
        //    base.OnLoad(node);
        //}

        //protected override void OnSave(ConfigNode node)
        //{
        //    base.OnSave(node);
        //}

        ////When the scene changes and the mod is destroyed
        //public void OnDestroy()
        //{
        //    //If we're in the MainMenu, don't do anything
        //    if (forbiddenScenes.Contains(HighLogic.LoadedScene))
        //        return;
        //}

        //public override void OnLoadVessel()
        //{
        //    base.OnLoadVessel();
        //}

        //public override void OnUnloadVessel()
        //{
        //    base.OnUnloadVessel();
        //}

        //public override void OnGoOnRails()
        //{
        //    base.OnGoOnRails();
        //}

        //public override void OnGoOffRails()
        //{
        //    base.OnGoOffRails();
        //}

        //public override bool ShouldBeActive()
        //{
        //    return base.ShouldBeActive();
        //}

        /// <summary>
        /// Is called on every frame
        /// </summary>
        public void FixedUpdate()
        {
            PersistentScenarioModule.VesselDataDict.TryGetValue(vessel.id, out VesselData vesselData);

            if (vesselData == null)
                return;

            if (vessel.loaded == false)
            {
                vesselData.PersistentAutopilotMode = persistentAutopilotMode;
                vesselData.persistentVesselTargetId = persistentVesselTargetId;
                vesselData.persistentVesselTargetBodyName = persistentVesselTargetBodyName;
                vesselData.persistentManeuverUT = persistentManeuverUT;
                vesselData.persistentManeuverNextPatch = persistentManeuverNextPatch;
                vesselData.persistentManeuverPatch = persistentManeuverPatch;

                return;
            }

            if (fixedUpdateCount++ > 100)
            {
                persistentAutopilotMode = vessel.Autopilot.Mode;

                if (vessel.targetObject != null)
                {
                    var orbitDriver = vessel.targetObject.GetOrbitDriver();
                    if (orbitDriver.vessel != null)
                    {
                        persistentVesselTargetId = orbitDriver.vessel.id.ToString();
                        persistentVesselTargetBodyName = string.Empty;
                    }
                    else if (orbitDriver.celestialBody != null)
                    {
                        persistentVesselTargetId = Guid.Empty.ToString();
                        persistentVesselTargetBodyName = orbitDriver.celestialBody.bodyName;
                    }
                }
                else
                {
                    persistentVesselTargetId = Guid.Empty.ToString();
                    persistentVesselTargetBodyName = string.Empty;
                }

                if (vessel.patchedConicSolver.maneuverNodes.Count > 0)
                {
                    var maneuverNode = vessel.patchedConicSolver.maneuverNodes[0];

                    persistentManeuverUT = maneuverNode.UT;
                    persistentManeuverNextPatch = maneuverNode.patch.Serialize();
                    persistentManeuverPatch = maneuverNode.patch.Serialize();
                }
            }
            else
            {
                vessel.Autopilot.SetMode(persistentAutopilotMode);
                vessel.PersistHeading(TimeWarp.fixedDeltaTime, headingTolerance, true);
            }
        }

        void OmVesselSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> gameEvent)
        {
            Debug.Log("[PersistentThrust]: GameEventSubscriber - detected OmVesselSOIChanged");
            gameEvent.host.FindPartModulesImplementing<PersistentEngine>().ForEach(e => e.VesselChangedSOI());
        }

        //public void onGameStateLoadEvent(ConfigNode newScene)
        //{

        //}

        //public void GameSceneLoadEvent(GameScenes newScene)
        //{

        //}

        ////The main show. The VesselDestroyEvent is activated whenever KSP destroys a vessel. We only care about it in a specific set of circumstances
        //private void VesselDestroyEvent(Vessel v)
        //{

        //}
    }
}
