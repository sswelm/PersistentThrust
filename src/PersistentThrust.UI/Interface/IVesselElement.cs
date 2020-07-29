using UnityEngine;

namespace PersistentThrust.UI.Interface
{
    public interface IVesselElement
    {
        string VesselName { get; set; }

        bool HasPersistentThrustActive { get; set; }

        bool HasInfoWindowActive { get; set; }

        Sprite VesselIcon { get; set; }
    }
}
