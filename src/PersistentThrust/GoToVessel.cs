using System;
using System.Linq;
using UnityEngine;

namespace PersistentThrust
{
    public static class GotoVessel
    {
        public static void JumpToVessel(Vessel v, bool skipKerbalism = false)
        {
            string _saveGame = GamePersistence.SaveGame("PT_Goto_backup", HighLogic.SaveFolder, SaveMode.OVERWRITE);

            if (HighLogic.LoadedSceneIsFlight)
            {
                FlightGlobals.SetActiveVessel(v);
            }
            else
            {
                int _idx = HighLogic.CurrentGame.flightState.protoVessels.FindLastIndex(pv => pv.vesselID == v.id);

                if (_idx != -1)
                {
                    FlightDriver.StartAndFocusVessel(_saveGame, _idx);
                }
                else
                {
                    Debug.Log("Invalid vessel Id:" + _idx);
                }
            }
        }

        public static void SetVesselAsTarget(Vessel v)
        {
            if (v != FlightGlobals.ActiveVessel) FlightGlobals.fetch.SetVesselTarget(v);
        }
    }
}
