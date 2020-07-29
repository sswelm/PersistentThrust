using PersistentThrust.UI.Interface;
using System;
using UnityEngine;

namespace PersistentThrust
{
    public class PTGUI_Vessel : MonoBehaviour, IVesselElement
    {
        public static PTGUI_Vessel Instance { get; set; }
        public string VesselName { get; set; }
        public bool HasPersistentThrustActive { get; set; }
        public bool HasInfoWindowActive { get; set; }
        public Sprite VesselIcon { get; set; }
        public Vessel vessel { get; set; }

        public void Update()
        {
            vessel.SetVesselWidePersistentThurst(HasPersistentThrustActive);
        }

        public static Sprite GetVesselTypeIcon(VesselType type)
        {
            try
            {
                return PTGUI_Loader.vesselSprites[type];
            }
            catch (Exception ex)
            {
                Debug.LogError("[PersistentThrust]: Failed to find vessel icon");
                Debug.LogException(ex);

                return PTGUI_Loader.vesselSprites[VesselType.Unknown];
            }
        }
    }
}
