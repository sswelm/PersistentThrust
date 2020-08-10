using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class SemiMajorAxis : SituationModule
    {
        public Vessel Vessel { private get; set; } = null;
        public SemiMajorAxis(string t) : base(t) { }

        protected override void UpdateVisible()
        {
            PTGUI_Settings.Instance.showSMA = IsVisible;
        }

        protected override string FieldUpdate()
        {
            if (Vessel == null)
                return "---";

            if (Vessel.orbit == null)
                return "---";

            return Result(Vessel.orbit.semiMajorAxis);
        }

        private string Result(double d)
        {
            return string.Format($"{d.Distance()}");
        }
    }
}
