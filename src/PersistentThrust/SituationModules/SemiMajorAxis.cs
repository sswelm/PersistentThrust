using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class SemiMajorAxis : SituationModule
    {
        public SemiMajorAxis(string t, Vessel v) : base(t, v) { }

        protected override void UpdateVisible()
        {
            PTGUI_Settings.Instance.showSMA = IsVisible;
        }

        protected override string FieldUpdate()
        {
            if (vessel == null)
                return "---";

            if (vessel.orbit == null)
                return "---";

            return Result(vessel.orbit.semiMajorAxis);
        }

        private string Result(double d)
        {
            return string.Format($"{d.Distance()}");
        }
    }
}
