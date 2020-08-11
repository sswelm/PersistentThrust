using UnityEngine;

namespace PersistentThrust
{
    [KSPAddon(KSPAddon.Startup.Flight , false)]
    public class GameEventSubscriber : MonoBehaviour
    {
        void Start()
        {
            //GameEvents.onVesselSOIChanged.Add(OmVesselSOIChanged);
            //GameEvents.onGameStateLoad.Add(onGameStateLoadEvent);
            //GameEvents.onGameSceneLoadRequested.Add(GameSceneLoadEvent);
            //GameEvents.onVesselWillDestroy.Add(VesselDestroyEvent);
        }

        void OnDestroy()
        {
            //GameEvents.onVesselSOIChanged.Remove(OmVesselSOIChanged);
        }

        //void OmVesselSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> gameEvent)
        //{
        //    Debug.Log("[PersistentThrust]: GameEventSubscriber - detected OmVesselSOIChanged");
        //    gameEvent.host.FindPartModulesImplementing<PersistentEngine>().ForEach(e => e.VesselChangedSOI());
        //}

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
