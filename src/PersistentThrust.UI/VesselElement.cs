using PersistentThrust.UI.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace PersistentThrust.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class VesselElement : MonoBehaviour
    {
        [SerializeField]
        private Text m_vesselName = null;
        [SerializeField]
        private Toggle m_persistentThrustToggle = null;
        [SerializeField]
        private Toggle m_vesselInfoToggle = null;
        [SerializeField]
        private Image m_vesselIcon = null;

        private IVesselElement vesselElementInterface;

        private void Start()
        {
            //Add listener for when the state of the Toggle changes, to take action
            m_persistentThrustToggle.onValueChanged.AddListener(delegate {
                PersistentThrustToggle(m_persistentThrustToggle.isOn);
            });
            m_vesselInfoToggle.onValueChanged.AddListener(delegate {
                VesselInfoToggle(m_vesselInfoToggle.isOn);
            });
        }

        private void Update()
        {

        }

        private void OnDestroy()
        {
            Destroy(vesselElementInterface.GameObj);
        }

        public bool GetPersistentThrustEnabledToggle()
        {
            if (m_persistentThrustToggle != null)
                return m_persistentThrustToggle.isOn;

            return false;
        }

        public void setElement(IVesselElement element)
        {
            if (element == null)
                return;

            vesselElementInterface = element;

            if (element.VesselName != null)
                m_vesselName.text = element.VesselName;

            if (element.VesselIcon != null)
                m_vesselIcon.sprite = element.VesselIcon;

            m_persistentThrustToggle.isOn = element.HasPersistentThrustActive;
            m_vesselInfoToggle.isOn = element.HasInfoWindowActive;
        }

        public void updateElement(IVesselElement element)
        {
            if (element == null)
                return;

            vesselElementInterface = element;

            if (element.VesselName != null)
                m_vesselName.text = element.VesselName;

            if (element.VesselIcon != null)
                m_vesselIcon.sprite = element.VesselIcon;

            m_vesselInfoToggle.isOn = element.HasInfoWindowActive;
            m_persistentThrustToggle.isOn = element.HasPersistentThrustActive;
        }

        // Listeners
        public void PersistentThrustToggle(bool isOn)
        {
            if (m_persistentThrustToggle == null)
                return;

            vesselElementInterface.HasPersistentThrustActive = isOn;
            vesselElementInterface.PersistentThrustWasToggled = true;
        }

        public void VesselInfoToggle(bool isOn)
        {
            if (m_vesselInfoToggle == null)
                return;
        }
    }
}
