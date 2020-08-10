using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class Eccentricity : SituationModule
    {
        public Vessel Vessel { private get; set; } = null;
        public Eccentricity(string t) : base(t) { }

        protected override void UpdateVisible()
        {
            PTGUI_Settings.Instance.showEccentricity = IsVisible;
        }

        protected override string FieldUpdate()
        {
            if (Vessel == null)
                return "---";

            if (Vessel.orbit == null)
                return "---";

            return Result(Vessel.orbit.eccentricity);
        }

        private string Result(double e)
        {
            return string.Format($"{e:F4}");
        }
    }
}
