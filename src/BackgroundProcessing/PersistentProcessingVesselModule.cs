using System.Collections.Generic;

namespace PersistentThrust
{
    public class PersistentProcessingVesselModule : VesselModule
    {
        // KSP doesn't save Vessel AutopilotMode so we have to do it ourselves
        [KSPField(isPersistant = true)]
        public VesselAutopilot.AutopilotMode persistentAutopilotMode;

        //List of scenes where we shouldn't run the mod. I toyed with runOnce, but couldn't get it working
        private static List<GameScenes> forbiddenScenes = new List<GameScenes> { GameScenes.LOADING, GameScenes.LOADINGBUFFER, GameScenes.CREDITS, GameScenes.MAINMENU, GameScenes.SETTINGS };

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

        //protected override void OnStart()
        //{
        //    base.OnStart();
        //}

        ////Fired when the mod loads each scene
        //public void Start()
        //{
        //    //If we're in the MainMenu, don't do anything
        //    if (forbiddenScenes.Contains(HighLogic.LoadedScene))
        //        return;
        //}

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
        public  void FixedUpdate()
        {
            if (vessel.loaded == false)
                return;

            persistentAutopilotMode = vessel.Autopilot.Mode;
        }
    }
}
