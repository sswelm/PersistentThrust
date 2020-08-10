using System;
using UnityEngine;

namespace PersistentThrust.UI.Interface
{
    public enum AutoPilotModeEnum
    {
        StabilityAssist = 0,
        Prograde = 1,
        Retrograde = 2,
        Normal = 3,
        Antinormal = 4,
        RadialIn = 5,
        RadialOut = 6,
        Target = 7,
        AntiTarget = 8,
        Maneuver = 9
    }

    public interface IVesselElement
    {
        GameObject GameObj { get; set; }

        Guid VesselId { get; set; }

        string VesselName { get; set; }

        bool IsActiveVessel { get; }

        bool HasPersistentThrustActive { get; set; }

        bool HasInfoWindowActive { get; set; }

        Sprite VesselIcon { get; set; }

        AutoPilotModeEnum VesselAutopilotMode { get; set; }

        bool VesselAutopilotActive { get; set; }

        void GoToVessel();

        void ChangeHasPersistentThrustState(bool isOn);

        void ChangeAutopilotMode();

        bool CheckAutopilotModeAvailable(AutoPilotModeEnum apMode);

        void OpenModeUnavailableDialog(AutoPilotModeEnum apMode);

        void OpenInfoWindow();

        void CloseInfoWindow();
    }
}
