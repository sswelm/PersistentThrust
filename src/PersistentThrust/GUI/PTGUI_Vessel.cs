using PersistentThrust.BackgroundProcessing;
using PersistentThrust.UI.Interface;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PersistentThrust
{
    public class PTGUI_Vessel : MonoBehaviour, IVesselElement
    {
        public GameObject GameObj { get; set; }
        public Guid VesselId { get; set; }
        public bool IsActiveVessel { get; set; }
        public bool IsVesselCommandable { get; set; }
        public string VesselName { get; set; }
        public bool HasPersistentThrustActive { get; set; }
        public bool HasInfoWindowActive { get; set; }
        public Sprite VesselIcon { get; set; }
        public AutoPilotModeEnum VesselAutopilotMode { get; set; }
        public bool VesselAutopilotActive { get; set; }

        private Vessel vessel;
        private VesselType currentVesselType;

        [KSPField(isPersistant = true)]
        private List<bool> hasPersistentThrustEnabled;
        [KSPField(isPersistant = false)]
        private bool didCheckPTEnabled = false;

        /// <summary>
        /// Adds a PTGUI_Vessel Monobehaviour component to a GameObject (or creates a new one).
        /// Initializes the script with values from the vessel.
        /// <para /> FIXME: Definitely not optimal. Creates 2 GameObjects for each vessel (one VesselElement, one PTGUI_Vessel).
        /// </summary>
        /// <param name="v"> The vessel whose parameters are used to initialize the script. </param>
        /// <param name="gameObject"> The GameObject to which the component will be added. </param>
        /// <returns> Initialized PTGUI_Vessel component </returns>
        public static PTGUI_Vessel Create(Vessel v, GameObject gameObject = null)
        {
            if (gameObject == null)
                gameObject = new GameObject();

            PTGUI_Vessel x = gameObject.AddComponent<PTGUI_Vessel>();

            x.GameObj = gameObject;
            x.vessel = v;
            x.VesselId = v.id;
            x.IsActiveVessel = v.isActiveVessel;
            x.IsVesselCommandable = v.IsVesselSituationValid();
            x.VesselName = v.vesselName;
            x.VesselIcon = GetVesselIcon(v.vesselType);
            x.HasInfoWindowActive = false;
            x.currentVesselType = v.vesselType;
            x.VesselAutopilotMode = v.GetAutopilotMode().ToPTIEnum();

            return x;
        }

        public void ChangeHasPersistentThrustState(bool isOn)
        {
            HasPersistentThrustActive = isOn;
            SetVesselWidePersistentThrust(isOn);
        }

        public void ChangeAutopilotMode()
        {
            if (vessel.loaded)
            {
                vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, VesselAutopilotActive);
                vessel.Autopilot.Enabled = VesselAutopilotActive;
                vessel.Autopilot.SetMode(VesselAutopilotMode.ToKSPEnum());
            }
            else
            {
                PersistentScenarioModule.VesselDataDict[vessel.id].PersistentAutopilotMode = VesselAutopilotMode.ToKSPEnum();
            }
        }

        public bool CheckAutopilotModeAvailable(AutoPilotModeEnum apMode)
        {
            if (!vessel.isCommandable)
                return false;

            if (vessel.loaded)
                return true;

            var vesselData = PersistentScenarioModule.VesselDataDict[vessel.id];

            switch (apMode.ToKSPEnum())
            {
                case VesselAutopilot.AutopilotMode.Target:
                case VesselAutopilot.AutopilotMode.AntiTarget:
                {
                    Guid persistentVesselTargetGuid = new Guid(vesselData.persistentVesselTargetId);

                    if (persistentVesselTargetGuid != Guid.Empty || !string.IsNullOrEmpty(vesselData.persistentVesselTargetBodyName))
                        return true;

                    break;
                }
                case VesselAutopilot.AutopilotMode.Maneuver:
                    {
                        //if (vessel.patchedConicSolver is null)
                        //    return false;

                        //else if (vessel.patchedConicSolver.maneuverNodes.Count > 0)
                        //    return true;

                        if (vessel.orbit.GetThrustVectorToManeuver(vesselData.persistentManeuverNextPatch, vesselData.persistentManeuverPatch, vesselData.persistentManeuverUT, vesselData.maneuverToleranceInDegree) != Vector3d.zero)
                            return true;

                        if (!string.IsNullOrEmpty(vesselData.persistentManeuverNextPatch))
                            return true;

                        break;
                    }
                default:
                    return true;
            }

            return false;
        }

        public void GoToVessel()
        {
            if (FlightGlobals.ActiveVessel != vessel)
            {
                var title = "Warning!";
                var msg = "Do you really want go to ";
                msg += VesselName.ToString() + "?";
                DialogGUIButton[] buttons;

                if (HighLogic.LoadedSceneIsFlight)
                {
                    buttons = new DialogGUIButton[] {
                        new DialogGUIButton("Go", () => { Utils.JumpToVessel(vessel); }),
                        new DialogGUIButton("Target", () => { Utils.SetVesselAsTarget(vessel); }),
                        new DialogGUIButton("Stay", () => { })
                    };
                }
                else
                {
                    buttons = new DialogGUIButton[] {
                        new DialogGUIButton("Go", () => { Utils.JumpToVessel(vessel); }),
                        new DialogGUIButton("Stay", () => { })
                    };
                }

                PopupDialog.SpawnPopupDialog
                    (
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new MultiOptionDialog(title, msg, title, HighLogic.UISkin, buttons),
                        false,
                        HighLogic.UISkin,
                        true,
                        string.Empty
                    );
            }
        }

        /// <summary>
        /// Called by Unity every frame that the MonoBehaviour is active. Updates the vessel with data from the GUI and vice-versa.
        /// </summary>
        private void LateUpdate()
        {
            UpdateVesselInfo();
        }

        private void OnDestroy()
        {
            SetVesselWidePersistentThrust(HasPersistentThrustActive);
        }

        /// <summary>
        /// Tries to find an icon for the input vessel type. Logs an exception if the type is unknonw.
        /// </summary>
        /// <param name="type"></param>
        /// <returns> The vessel Sprite </returns>
        public static Sprite GetVesselIcon(VesselType type)
        {
            try
            {
                return PTGUI_Loader.vesselSprites[type];
            }
            catch (Exception ex)
            {
                Debug.LogError("[PersistentThrust]: Failed to find vessel icon");
                Debug.LogException(ex);

                return PTGUI_Loader.vesselSprites[VesselType.Unknown];
            }
        }

        /// <summary>
        ///
        /// </summary>
        private void UpdateVesselInfo()
        {
            if (VesselName != vessel.vesselName)
                VesselName = vessel.vesselName;

            if (currentVesselType != vessel.vesselType)
            {
                currentVesselType = vessel.vesselType;
                VesselIcon = GetVesselIcon(vessel.vesselType);
            }

            if (vessel.loaded)
            {
                IsActiveVessel = vessel.isActiveVessel;

                if (VesselAutopilotActive != vessel.Autopilot.Enabled)
                {
                    VesselAutopilotActive = vessel.Autopilot.Enabled;
                }

                if (VesselAutopilotMode != vessel.Autopilot.Mode.ToPTIEnum())
                {
                    VesselAutopilotMode = vessel.Autopilot.Mode.ToPTIEnum();
                }
            }
        }

        public void UpdateVesselThrustInfo(bool? isOn = null)
        {
            if (isOn != null)
                HasPersistentThrustActive = (bool)isOn;
            else
                isOn = PersistentThrustEnabled(vessel).Contains(true);

            HasPersistentThrustActive = (bool)isOn;
        }

        public List<bool> PersistentThrustEnabled(Vessel vessel)
        {
            if (didCheckPTEnabled)
                return hasPersistentThrustEnabled;

            if (hasPersistentThrustEnabled is null)
                hasPersistentThrustEnabled = new List<bool>();

            if (vessel.loaded)
            {
                List<PersistentEngine> PEmoduleList = vessel.FindPartModulesImplementing<PersistentEngine>();
                foreach (var PE in PEmoduleList)
                {
                    hasPersistentThrustEnabled.Add(PE.HasPersistentThrust);
                }
            }
            else
            {
                foreach (var x in vessel.FindPersistentEngineModuleSnapshots())
                {
                    bool _enabled = true;
                    x.moduleValues.TryGetValue(nameof(PersistentEngine.HasPersistentThrust), ref _enabled);
                    hasPersistentThrustEnabled.Add(_enabled);
                }

            }

            didCheckPTEnabled = true;

            return hasPersistentThrustEnabled;
        }

        public void ResetPTEnabledCheck()
        {
            didCheckPTEnabled = false;
        }

        public void SetVesselWidePersistentThrust(bool isOn)
        {
            if (vessel.loaded)
            {
                var PEmoduleList = vessel.FindPartModulesImplementing<PersistentEngine>();

                foreach (var module in PEmoduleList)
                {
                    module.HasPersistentThrust = isOn;
                }
            }
            else
            {
                foreach (var x in vessel.FindPersistentEngineModuleSnapshots())
                    x.moduleValues.values.SetValue(nameof(PersistentEngine.HasPersistentThrust), isOn.ToString());
            }

            ResetPTEnabledCheck();
        }

        public void OpenModeUnavailableDialog(AutoPilotModeEnum mode)
        {
            string title = "Warning!";
            string msg;
            DialogGUIButton[] buttons;

            switch (mode)
            {
                case AutoPilotModeEnum.AntiTarget:
                case AutoPilotModeEnum.Target:
                    {
                        msg = $"Vessel {VesselName} doesn't have a valid target.";
                        buttons = new DialogGUIButton[] {
                        new DialogGUIButton($"Jump to vessel to set a Target", () => { Utils.JumpToVessel(vessel); }),
                        new DialogGUIButton("Ok", () => { })
                        };
                        break;
                    }
                case AutoPilotModeEnum.Maneuver:
                    {
                        msg = $"Vessel {VesselName} has no active maneuver nodes.";
                        buttons = new DialogGUIButton[] {
                        new DialogGUIButton($"Jump to vessel to plot a maneuver", () => { Utils.JumpToVessel(vessel); }),
                        new DialogGUIButton("Ok", () => { })
                        };
                        break;
                    }
                default:
                    {
                        msg = $"Invalid option";
                        buttons = new DialogGUIButton[] {
                        new DialogGUIButton("Ok", () => { })
                        };
                        break;
                    }
            }

            PopupDialog.SpawnPopupDialog
            (
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new MultiOptionDialog(title, msg, title, HighLogic.UISkin, buttons),
                false,
                HighLogic.UISkin,
                true,
                string.Empty
            );
        }

        public void OpenInfoWindow()
        {
            PTGUI.Instance.OpenInfoWindow(vessel);
        }

        public void CloseInfoWindow()
        {
            PTGUI.Instance.CloseInfoWindow();
        }
    }

    public static class AutopilotEnumExtensions
    {
        public static AutoPilotModeEnum ToPTIEnum(this VesselAutopilot.AutopilotMode mode)
        {
            switch (mode)
            {
                case VesselAutopilot.AutopilotMode.StabilityAssist:
                    return AutoPilotModeEnum.StabilityAssist;
                case VesselAutopilot.AutopilotMode.Prograde:
                    return AutoPilotModeEnum.Prograde;
                case VesselAutopilot.AutopilotMode.Retrograde:
                    return AutoPilotModeEnum.Retrograde;
                case VesselAutopilot.AutopilotMode.Normal:
                    return AutoPilotModeEnum.Normal;
                case VesselAutopilot.AutopilotMode.Antinormal:
                    return AutoPilotModeEnum.Antinormal;
                case VesselAutopilot.AutopilotMode.RadialIn:
                    return AutoPilotModeEnum.RadialIn;
                case VesselAutopilot.AutopilotMode.RadialOut:
                    return AutoPilotModeEnum.RadialOut;
                case VesselAutopilot.AutopilotMode.Target:
                    return AutoPilotModeEnum.Target;
                case VesselAutopilot.AutopilotMode.AntiTarget:
                    return AutoPilotModeEnum.AntiTarget;
                case VesselAutopilot.AutopilotMode.Maneuver:
                    return AutoPilotModeEnum.Maneuver;
                default:
                    return AutoPilotModeEnum.StabilityAssist;
            }
        }

        public static VesselAutopilot.AutopilotMode ToKSPEnum(this AutoPilotModeEnum apMode)
        {
            switch (apMode)
            {
                case AutoPilotModeEnum.StabilityAssist:
                    return VesselAutopilot.AutopilotMode.StabilityAssist;
                case AutoPilotModeEnum.Prograde:
                    return VesselAutopilot.AutopilotMode.Prograde;
                case AutoPilotModeEnum.Retrograde:
                    return VesselAutopilot.AutopilotMode.Retrograde;
                case AutoPilotModeEnum.Normal:
                    return VesselAutopilot.AutopilotMode.Normal;
                case AutoPilotModeEnum.Antinormal:
                    return VesselAutopilot.AutopilotMode.Antinormal;
                case AutoPilotModeEnum.RadialIn:
                    return VesselAutopilot.AutopilotMode.RadialIn;
                case AutoPilotModeEnum.RadialOut:
                    return VesselAutopilot.AutopilotMode.RadialOut;
                case AutoPilotModeEnum.Target:
                    return VesselAutopilot.AutopilotMode.Target;
                case AutoPilotModeEnum.AntiTarget:
                    return VesselAutopilot.AutopilotMode.AntiTarget;
                case AutoPilotModeEnum.Maneuver:
                    return VesselAutopilot.AutopilotMode.Maneuver;
                default:
                    return VesselAutopilot.AutopilotMode.StabilityAssist;
            }
        }
    }
}
