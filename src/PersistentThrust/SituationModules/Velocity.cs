using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class Velocity : SituationModule
    {
        public Vessel Vessel { private get; set; } = null;
        public Velocity(string t) : base(t) { }

        protected override void UpdateVisible()
        {
            PTGUI_Settings.Instance.showVelocity = IsVisible;
        }

        protected override string FieldUpdate()
        {
            if (Vessel == null)
                return "---";

            if (Vessel.orbit == null)
                return "---";

            return Result(Vessel.orbit.orbitalSpeed);
        }

        private string Result(double v)
        {
            return string.Format($"{v.Speed(1, 1)}");
        }
    }
}
