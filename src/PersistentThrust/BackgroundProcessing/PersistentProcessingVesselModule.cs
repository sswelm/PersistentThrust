using System.Collections.Generic;
using UnityEngine;

namespace PersistentThrust.BackgroundProcessing
{
    public class PersistentProcessingVesselModule : VesselModule
    {
        // KSP doesn't save Vessel AutopilotMode so we have to do it ourselves
        [KSPField(isPersistant = true)]
        public VesselAutopilot.AutopilotMode persistentAutopilotMode;

        public GameScenes linkedScene;

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
                    if (vesselData.hasBeenLoaded)
                        persistentAutopilotMode = vesselData.PersistentAutopilotMode;
                    else
                        vesselData.PersistentAutopilotMode = persistentAutopilotMode;

                    return;
            }

            if (fixedUpdateCount++ > 60)
            {
                persistentAutopilotMode = vessel.Autopilot.Mode;

                vesselData.hasBeenLoaded = true;
                vesselData.PersistentAutopilotMode = persistentAutopilotMode;
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
