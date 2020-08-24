using PersistentThrust.UI.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace PersistentThrust.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class VesselElement : MonoBehaviour
    {
        [SerializeField]
        private Image m_image = null;
        [SerializeField]
        private TextHandler m_vesselName = null;
        [SerializeField]
        private Toggle m_persistentThrustToggle = null;
        [SerializeField]
        private Toggle m_vesselInfoToggle = null;
        [SerializeField]
        private Image m_vesselIcon = null;
        [SerializeField]
        private ToggleGroup m_HeadingsToggleGroup = null;
        [SerializeField]
        private Toggle m_progradeToggle = null;
        [SerializeField]
        private Toggle m_retrogradeToggle = null;
        [SerializeField]
        private Toggle m_radialIntoggle = null;
        [SerializeField]
        private Toggle m_radialOutToggle = null;
        [SerializeField]
        private Toggle m_normalToggle = null;
        [SerializeField]
        private Toggle m_antinormalToggle = null;
        [SerializeField]
        private Toggle m_targetToggle = null;
        [SerializeField]
        private Toggle m_antiTargetToggle = null;
        [SerializeField]
        private Toggle m_maneuverToggle = null;
        [SerializeField]
        private Toggle m_stabilityToggle = null;
        [SerializeField]
        private Button goToButton = null;
        [SerializeField]
        public Toggle m_infoToggle = null;

        private IVesselElement vesselElementInterface;
        private AutoPilotModeEnum currentAutopilotMode;

        private void Awake()
        {
            var rect = GetComponent<RectTransform>();
            int indexNumber = rect.GetSiblingIndex();

            var tempColor = m_image.color;
            tempColor.a = (indexNumber % 2) == 0 ? 0f : 0.08f;
            m_image.color = tempColor;

            goToButton = m_vesselIcon.gameObject.GetComponent<Button>();
            //m_infoToggle = gameObject.GetComponent<Toggle>();
        }

        private void Start()
        {
            // Add listener to the GoToVessel button
            goToButton.onClick.AddListener(delegate
            {
                GoToVessel();
            });

            // Add listener to the toggles
            m_persistentThrustToggle.onValueChanged.AddListener(delegate
            {
                PersistentThrustToggle(m_persistentThrustToggle.isOn);
            });
            m_vesselInfoToggle.onValueChanged.AddListener(delegate
            {
                VesselInfoToggle(m_vesselInfoToggle.isOn);
            });
            m_antinormalToggle.onValueChanged.AddListener(delegate
            {
                HeadingToggles(m_antinormalToggle);
            });
            m_antiTargetToggle.onValueChanged.AddListener(delegate
            {
                HeadingToggles(m_antiTargetToggle);
            });
            m_maneuverToggle.onValueChanged.AddListener(delegate
            {
                HeadingToggles(m_maneuverToggle);
            });
            m_normalToggle.onValueChanged.AddListener(delegate
            {
                HeadingToggles(m_normalToggle);
            });
            m_progradeToggle.onValueChanged.AddListener(delegate
            {
                HeadingToggles(m_progradeToggle);
            });
            m_radialIntoggle.onValueChanged.AddListener(delegate
            {
                HeadingToggles(m_radialIntoggle);
            });
            m_radialOutToggle.onValueChanged.AddListener(delegate
            {
                HeadingToggles(m_radialOutToggle);
            });
            m_retrogradeToggle.onValueChanged.AddListener(delegate
            {
                HeadingToggles(m_retrogradeToggle);
            });
            m_targetToggle.onValueChanged.AddListener(delegate
            {
                HeadingToggles(m_targetToggle);
            });
            m_stabilityToggle.onValueChanged.AddListener(delegate
            {
                HeadingToggles(m_stabilityToggle);
            });

            if (m_infoToggle != null)
                m_infoToggle.onValueChanged.AddListener(delegate
                {
                    InfoWindowDelegate(m_infoToggle.isOn);
                });
        }

        private void Update()
        {

        }

        private void OnDestroy()
        {
            Destroy(vesselElementInterface.GameObj);
        }

        public void SetElement(IVesselElement element)
        {
            if (element == null)
                return;

            vesselElementInterface = element;

            if (element.VesselName != null)
                m_vesselName.OnTextUpdate.Invoke(element.VesselName);

            if (element.VesselIcon != null)
                m_vesselIcon.sprite = element.VesselIcon;

            m_persistentThrustToggle.isOn = element.HasPersistentThrustActive;
            m_vesselInfoToggle.isOn = element.HasInfoWindowActive;

            currentAutopilotMode = element.VesselAutopilotMode;

            if (goToButton != null && element.IsActiveVessel)
                goToButton.enabled = false;

            m_vesselName.OnColorUpdate.Invoke(vesselElementInterface.IsVesselCommandable ? Color.white : Color.yellow);

            UpdateElementAutopilotInfo(element.VesselAutopilotMode);
        }

        public void UpdateElement(IVesselElement element)
        {
            if (element == null)
                return;

            vesselElementInterface = element;

            m_vesselName.OnTextUpdate.Invoke(element.VesselName);

            if (element.VesselIcon != m_vesselIcon.sprite)
                m_vesselIcon.sprite = element.VesselIcon;

            m_persistentThrustToggle.isOn = element.HasPersistentThrustActive;
            m_vesselInfoToggle.isOn = element.HasInfoWindowActive;

            m_vesselName.OnColorUpdate.Invoke(vesselElementInterface.IsVesselCommandable ? Color.white : Color.yellow);

            if (currentAutopilotMode != element.VesselAutopilotMode)
            {
                UpdateElementAutopilotInfo(element.VesselAutopilotMode);
                currentAutopilotMode = element.VesselAutopilotMode;
            }
        }

        private void UpdateElementAutopilotInfo(AutoPilotModeEnum ap)
        {
            switch (ap)
            {
                case AutoPilotModeEnum.StabilityAssist:
                    {
                        m_stabilityToggle.isOn = true;
                        break;
                    }
                case AutoPilotModeEnum.Antinormal:
                    {
                        m_antinormalToggle.isOn = true;
                        break;
                    }
                case AutoPilotModeEnum.AntiTarget:
                    {
                        m_antiTargetToggle.isOn = true;
                        break;
                    }
                case AutoPilotModeEnum.Maneuver:
                    {
                        m_maneuverToggle.isOn = true;
                        break;
                    }
                case AutoPilotModeEnum.Normal:
                    {
                        m_normalToggle.isOn = true;
                        break;
                    }
                case AutoPilotModeEnum.Prograde:
                    {
                        m_progradeToggle.isOn = true;
                        break;
                    }
                case AutoPilotModeEnum.RadialIn:
                    {
                        m_radialIntoggle.isOn = true;
                        break;
                    }
                case AutoPilotModeEnum.RadialOut:
                    {
                        m_radialOutToggle.isOn = true;
                        break;
                    }
                case AutoPilotModeEnum.Retrograde:
                    {
                        m_retrogradeToggle.isOn = true;
                        break;
                    }
                case AutoPilotModeEnum.Target:
                    {
                        m_targetToggle.isOn = true;
                        break;
                    }
                default:
                    {
                        m_HeadingsToggleGroup.SetAllTogglesOff();
                        break;
                    }
            }
        }

        #region Listeners
        public void PersistentThrustToggle(bool isOn)
        {
            if (m_persistentThrustToggle == null)
                return;

            vesselElementInterface.ChangeHasPersistentThrustState(isOn);
        }

        public void VesselInfoToggle(bool isOn)
        {
            if (m_vesselInfoToggle == null)
                return;

            vesselElementInterface.HasInfoWindowActive = isOn;
        }

        public void HeadingToggles(Toggle mode)
        {

            if (mode.name == m_stabilityToggle.name)
                SetInterfaceAutopilotMode(AutoPilotModeEnum.StabilityAssist, mode.isOn);

            else if (mode.name == m_antinormalToggle.name)
                SetInterfaceAutopilotMode(AutoPilotModeEnum.Antinormal, mode.isOn);

            else if (mode.name == m_antiTargetToggle.name)
                SetInterfaceAutopilotMode(AutoPilotModeEnum.AntiTarget, mode.isOn);

            else if (mode.name == m_maneuverToggle.name)
                SetInterfaceAutopilotMode(AutoPilotModeEnum.Maneuver, mode.isOn);

            else if (mode.name == m_normalToggle.name)
                SetInterfaceAutopilotMode(AutoPilotModeEnum.Normal, mode.isOn);

            else if (mode.name == m_progradeToggle.name)
                SetInterfaceAutopilotMode(AutoPilotModeEnum.Prograde, mode.isOn);

            else if (mode.name == m_radialIntoggle.name)
                SetInterfaceAutopilotMode(AutoPilotModeEnum.RadialIn, mode.isOn);

            else if (mode.name == m_radialOutToggle.name)
                SetInterfaceAutopilotMode(AutoPilotModeEnum.RadialOut, mode.isOn);

            else if (mode.name == m_retrogradeToggle.name)
                SetInterfaceAutopilotMode(AutoPilotModeEnum.Retrograde, mode.isOn);

            else if (mode.name == m_targetToggle.name)
                SetInterfaceAutopilotMode(AutoPilotModeEnum.Target, mode.isOn);
        }

        private void SetInterfaceAutopilotMode(AutoPilotModeEnum mode, bool active = true)
        {
            currentAutopilotMode = mode;

            if (active && !vesselElementInterface.CheckAutopilotModeAvailable(mode))
            {
                vesselElementInterface.OpenModeUnavailableDialog(mode);
                return;
            }

            vesselElementInterface.VesselAutopilotActive = active;
            vesselElementInterface.VesselAutopilotMode = mode;

            vesselElementInterface.ChangeAutopilotMode();
        }

        public void GoToVessel()
        {
            vesselElementInterface.GoToVessel();
        }

        private void InfoWindowDelegate(bool isOn)
        {
            if (isOn)
                vesselElementInterface.OpenInfoWindow();
            else
                vesselElementInterface.CloseInfoWindow();
        }
        #endregion
    }
}
