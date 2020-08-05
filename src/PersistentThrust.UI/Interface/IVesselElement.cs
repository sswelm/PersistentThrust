using System;
using UnityEngine;

namespace PersistentThrust.UI.Interface
{
    public interface IVesselElement
    {
        GameObject GameObj { get; set; }

        Guid VesselId { get; set; }

        string VesselName { get; set; }

        bool HasPersistentThrustActive { get; set; }

        bool PersistentThrustWasToggled { get; set; }

        bool HasInfoWindowActive { get; set; }

        Sprite VesselIcon { get; set; }
    }
}
