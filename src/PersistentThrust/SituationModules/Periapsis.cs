using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class Periapsis : SituationModule
    {
        public Vessel Vessel { private get; set; } = null;
        public Periapsis(string t) : base(t) { }

        protected override void UpdateVisible()
        {
            PTGUI_Settings.Instance.showPeriapsis = IsVisible;
        }

        protected override string FieldUpdate()
        {
            if (Vessel == null)
                return "---";

            if (Vessel.orbit == null)
                return "---";

            if (Vessel.orbit.eccentricity >= 1 && Vessel.orbit.timeToPe < 0)
                return "---";

            return Result(Vessel.orbit.PeA, Vessel.orbit.timeToPe);
        }

        private string Result(double d, double t)
        {
            return string.Format($"{d.Distance()} in {t.Time(3)}");
        }
    }
}
