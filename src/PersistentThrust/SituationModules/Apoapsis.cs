using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class Apoapsis : SituationModule
    {
        public Apoapsis(string t, Vessel v) : base(t, v) { }

        protected override void UpdateVisible()
        {
            PTGUI_Settings.Instance.showApoapsis = IsVisible;
        }

        protected override string FieldUpdate()
        {
            if (vessel == null)
                return "---";

            if (vessel.orbit == null)
                return "---";

            if (vessel.orbit.eccentricity >= 1)
                return "---";

            return Result(vessel.orbit.ApA, vessel.orbit.timeToAp);
        }

        private string Result(double d, double t)
        {
            return string.Format($"{d.Distance()} in {t.Time(3)}");
        }
    }
}
