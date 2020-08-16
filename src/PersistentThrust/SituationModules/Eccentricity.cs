using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class Eccentricity : SituationModule
    {
        public Eccentricity(string t, Vessel v) : base(t, v) { }

        protected override void UpdateVisible()
        {
            PTGUI_Settings.Instance.showEccentricity = IsVisible;
        }

        protected override string FieldUpdate()
        {
            if (vessel == null)
                return "---";

            if (vessel.orbit == null)
                return "---";

            return Result(vessel.orbit.eccentricity);
        }

        private string Result(double e)
        {
            return string.Format($"{e:F4}");
        }
    }
}
