using System;
using System.Reflection;
using KSP.IO;
using KSP.Localization;

namespace PersistentThrust
{
	public class PTSettings : GameParameters.CustomParameterNode
	{
        public override string Title { get { return "Settings"; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "PersistentThrust"; } }
        public override string DisplaySection { get { return "Persistent Thrust"; } }
        public override int SectionOrder { get { return 1; } }
        public override bool HasPresets { get { return false; } }


        // Config Settings
        [GameParameters.CustomParameterUI("Return to real time after key pressed",
            toolTip = "Return to real time after any throttle adjust (ctrl, shift or z) key is pressed?")]
        public bool returnToRealtimeAfterKeyPressed = false;

    }

    public class PTDevSettings : GameParameters.CustomParameterNode
    {
        public override string Title { get { return "Development Settings"; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "PersistentThrust"; } }
        public override string DisplaySection { get { return "Persistent Thrust"; } }
        public override int SectionOrder { get { return 2; } }
        public override bool HasPresets { get { return false; } }

        // Config settings
        [GameParameters.CustomFloatParameterUI("Queue length", minValue = 2, maxValue = 10, displayFormat = "N1",
            toolTip = "Throttle and Isp queue length (in frames). Try raising it if you have issues with thrust not being persisted when dropping from timewarp.")]
        public int queueLength = 2;

        // Other Settings
        public int missingPowerCountdownSize = 10;
        public int propellantReqMetFactorQueueSize = 100;
        public double minimumPropellantReqMetFactor = 0.2;
        public float headingTolerance = 0.002f;
    }
}
