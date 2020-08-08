using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        bool ThrottleVisible { get; set; }

        Vector2 Position { get; set; }

        float Scale { get; }

        void ClampToScreen(RectTransform rect);
    }
}
