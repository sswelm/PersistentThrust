using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class Altitude : SituationModule
    {
        public Altitude(string t, Vessel v) : base(t, v) { }

        protected override void UpdateVisible()
        {
            PTGUI_Settings.Instance.showAltitude = IsVisible;
        }

        protected override string FieldUpdate()
        {
            if (vessel == null)
                return "---";

            if (vessel.orbit == null)
                return "---";

            return Result(vessel.orbit.altitude);
        }

        private string Result(double d)
        {
            return string.Format($"{d.Distance(1)}");
        }
    }
}
