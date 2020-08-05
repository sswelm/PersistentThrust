using System.Collections.Generic;
using UnityEngine;

namespace PersistentThrust.UI.Interface
{
    public interface IMainWindow
    {
        string Version { get; }

        bool IsVisible { get; }

        List<IVesselElement> Vessels { get; }

        Vector2 Position { get; set; }

        float Scale { get; }

        void ClampToScreen(RectTransform rect);
    }
}
