using PersistentThrust.BackgroundProcessing;
using PersistentThrust.UI.SituationModules;

namespace PersistentThrust.SituationModules
{
    public class Acceleration : SituationModule
    {
        private readonly VesselData _vesselData;

        public Acceleration(string t, Vessel v) : base(t, v)
        {
            PersistentScenarioModule.VesselDataDict.TryGetValue(vessel.id, out _vesselData);
        }

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

            if (!vessel.HasPersistentEngineModules())
                return Result(0);
            else
            {
                if (vessel == FlightGlobals.ActiveVessel && !vessel.packed)
                    return Result(vessel.geeForce_immediate * PhysicsGlobals.GravitationalAcceleration);

                else if (_vesselData != null)
                    return Result(_vesselData.AccelerationVector.magnitude);
                
                else
                    return "---";
            }
        }

        private string Result(double a)
        {
            return string.Format($"{a.Acceleration()}");
        }
    }
}
