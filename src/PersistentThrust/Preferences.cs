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
        [GameParameters.CustomParameterUI("#LOC_PT_DefaultHasPersistentThrust", toolTip = "#LOC_PT_DefaultHasPersistentThrustToolTip")]
        public bool defaultHasPersistentThrust = true;
        [GameParameters.CustomParameterUI("#LOC_PT_DefaultHasPersistentHeadingEnabled", toolTip = "#LOC_PT_DefaultHasPersistentHeadingEnabledToolTip")]
        public bool defaultHasPersistentHeadingEnabled = true;
        [GameParameters.CustomParameterUI("#LOC_PT_DefaultMaximizePersistentIsp", toolTip = "#LOC_PT_DefaultMaximizePersistentIspToolTip")]
        public bool defaultMaximizePersistentIsp = true;
        [GameParameters.CustomParameterUI("#LOC_PT_DefaultMaximizePersistentPower", toolTip = "#LOC_PT_DefaultMaximizePersistentPowerToolTip")]
        public bool defaultMaximizePersistentPower = false;
        [GameParameters.CustomFloatParameterUI("#LOC_PT_DefaultManeuverTolerance", minValue = 0, maxValue = 90, toolTip = "#LOC_PT_DefaultManeuverToleranceToolTip")]
        public float maneuverToleranceInDegree = 90;
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
