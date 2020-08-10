using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class Acceleration : SituationModule
    {
        public Vessel Vessel { private get; set; } = null;
        public Acceleration(string t) : base(t) { }

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

            if (!Vessel.HasPersistentEngineModules())
                return Result(0);
            else
            {
                if (Vessel == FlightGlobals.ActiveVessel && !Vessel.packed)
                    return Result(Vessel.geeForce_immediate * PhysicsGlobals.GravitationalAcceleration);

                else if (Vessel == FlightGlobals.ActiveVessel)
                {
                    double acc = 0;

                    foreach (var pm in Vessel.FindPartModulesImplementing<PersistentEngine>())
                    {
                        acc += pm.persistentAcceleration;
                    }

                    return Result(acc);
                }
                else
                {
                    double acc = 0;

                    foreach (var peModSnaphot in Vessel.FindPersistentEngineModuleSnapshots())
                    {
                        acc += double.Parse(peModSnaphot.moduleValues.GetValue(nameof(PersistentEngine.persistentAcceleration)));
                    }

                    return Result(acc);
                }
            }
        }

        private string Result(double a)
        {
            return string.Format($"{a.Acceleration()}");
        }
    }
}
