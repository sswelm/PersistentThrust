using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class Inclination : SituationModule
    {
        public Inclination(string t, Vessel v) : base(t, v) { }

        protected override void UpdateVisible()
        {
            PTGUI_Settings.Instance.showInclination = IsVisible;
        }

        protected override string FieldUpdate()
        {
            if (vessel == null)
                return "---";

            if (vessel.orbit == null)
                return "---";

            return Result(vessel.orbit.inclination);
        }

        private string Result(double i)
        {
            return string.Format($"{i:F3}");
        }
    }
}
