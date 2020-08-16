using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class Velocity : SituationModule
    {
        public Velocity(string t, Vessel v) : base(t, v) { }

        protected override void UpdateVisible()
        {
            PTGUI_Settings.Instance.showVelocity = IsVisible;
        }

        protected override string FieldUpdate()
        {
            if (vessel == null)
                return "---";

            if (vessel.orbit == null)
                return "---";

            return Result(vessel.orbit.orbitalSpeed);
        }

        private string Result(double v)
        {
            return string.Format($"{v.Speed(1, 1)}");
        }
    }
}
