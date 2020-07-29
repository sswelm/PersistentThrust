using System.Collections.Generic;

namespace PersistentThrust.UI.Interface
{
    public interface IMainWindow
    {
        string Version { get; }

        bool IsVisible { get; }

        IList<IVesselElement> GetVessels { get; }
        /*
        float Scale { get; }

        Vector2 Position { get; set; }

        void ClampToScreen(RectTransform rect);
        */
    }
}
