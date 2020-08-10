using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class Apoapsis : SituationModule
    {
        public Vessel Vessel { private get; set; } = null;
        public Apoapsis(string t) : base(t) { }

        protected override void UpdateVisible()
        {
            PTGUI_Settings.Instance.showApoapsis = IsVisible;
        }

        protected override string FieldUpdate()
        {
            if (Vessel == null)
                return "---";

            if (Vessel.orbit == null)
                return "---";

            if (Vessel.orbit.eccentricity >= 1)
                return "---";

            return Result(Vessel.orbit.ApA, Vessel.orbit.timeToAp);
        }

        private string Result(double d, double t)
        {
            return string.Format($"{d.Distance()} in {t.Time(3)}");
        }
    }
}
