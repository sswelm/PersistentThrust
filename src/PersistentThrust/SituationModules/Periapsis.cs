using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class Periapsis : SituationModule
    {
        public Periapsis(string t, Vessel v) : base(t, v) { }

        protected override void UpdateVisible()
        {
            PTGUI_Settings.Instance.showPeriapsis = IsVisible;
        }

        protected override string FieldUpdate()
        {
            if (vessel == null)
                return "---";

            if (vessel.orbit == null)
                return "---";

            if (vessel.orbit.eccentricity >= 1 && vessel.orbit.timeToPe < 0)
                return "---";

            return Result(vessel.orbit.PeA, vessel.orbit.timeToPe);
        }

        private string Result(double d, double t)
        {
            return string.Format($"{d.Distance()} in {t.Time(3)}");
        }
    }
}
