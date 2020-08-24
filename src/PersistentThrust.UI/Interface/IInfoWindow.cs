using System.Collections.Generic;
using UnityEngine;

namespace PersistentThrust.UI.Interface
{
    public interface IInfoWindow
    {
        string VesselName { get; }

        float Throttle { get; set; }

        bool IsVisible { get; }

        bool DeltaVVisible { get; set; }

        bool SituationVisible { get; set; }

        string SituationTextString { get; }

        GameObject SituationModulePrefab { get; }

        List<IInfoModule> Modules { get; }

        bool ThrottleVisible { get; set; }

        void UpdatePersistentThrottle(float value);

        void SetInputLock(bool on);

        Vector2 Position { get; set; }

        float Scale { get; }

        void ClampToScreen(RectTransform rect);
    }
}
