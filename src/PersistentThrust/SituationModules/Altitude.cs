using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class Altitude : SituationModule
    {
        public Vessel Vessel { private get; set; } = null;
        public Altitude(string t) : base(t) { }

        protected override void UpdateVisible()
        {
            PTGUI_Settings.Instance.showAltitude = IsVisible;
        }

        protected override string FieldUpdate()
        {
            if (Vessel == null)
                return "---";

            if (Vessel.orbit == null)
                return "---";

            return Result(Vessel.orbit.altitude);
        }

        private string Result(double d)
        {
            return string.Format($"{d.Distance(1)}");
        }
    }
}
