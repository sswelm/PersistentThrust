using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class Inclination : SituationModule
    {
        public Vessel Vessel { private get; set; } = null;
        public Inclination(string t) : base(t) { }

        protected override void UpdateVisible()
        {
            PTGUI_Settings.Instance.showInclination = IsVisible;
        }

        protected override string FieldUpdate()
        {
            if (Vessel == null)
                return "---";

            if (Vessel.orbit == null)
                return "---";

            return Result(Vessel.orbit.inclination);
        }

        private string Result(double i)
        {
            return string.Format($"{i:F3}");
        }
    }
}
