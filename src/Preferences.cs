using System;
using System.Reflection;
using KSP.IO;
using KSP.Localization;

namespace PersistentThrust
{
	public class PTSettings : GameParameters.CustomParameterNode
	{
        public override string Title { get { return "#LOC_PT_Settings"; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "PersistentThrust"; } }
        public override string DisplaySection { get { return "Persistent Thrust"; } }
        public override int SectionOrder { get { return 1; } }
        public override bool HasPresets { get { return false; } }


        // Config Settings
        [GameParameters.CustomParameterUI("#LOC_PT_Settings_ReturnToRealTime", toolTip = "#LOC_PT_Settings_ReturnToRealTimeToolTip")]
        public bool returnToRealtimeAfterKeyPressed = false;

    }

    public class PTDevSettings : GameParameters.CustomParameterNode
    {
        public override string Title { get { return "#LOC_PT_SettingsDev"; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "PersistentThrust"; } }
        public override string DisplaySection { get { return "Persistent Thrust"; } }
        public override int SectionOrder { get { return 2; } }
        public override bool HasPresets { get { return false; } }

        // Config settings
        [GameParameters.CustomIntParameterUI("#LOT_PT_SettingsDev_QueueLength", minValue = 2, maxValue = 10, toolTip = "#LOT_PT_SettingsDev_QueueLengthToolTip")]
        public int queueLength = 2;
    }
}
