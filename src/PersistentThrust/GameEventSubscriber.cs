using UnityEngine;

namespace PersistentThrust
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class GameEventSubscriber : MonoBehaviour
    {
        void Start()
        {
            GameEvents.onVesselSOIChanged.Add(OmVesselSOIChanged);
        }

        void OnDestroy()
        {
            GameEvents.onVesselSOIChanged.Remove(OmVesselSOIChanged);
        }

        void OmVesselSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> gameEvent)
        {
            Debug.Log("[Persistent Thrust]: GameEventSubscriber - detected OmVesselSOIChanged");
            gameEvent.host.FindPartModulesImplementing<PersistentEngine>().ForEach(e => e.VesselChangedSOI());
        }
    }
}
