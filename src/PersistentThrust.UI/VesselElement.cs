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


        public void UpdateVesselName(string newName)
        {
            if (m_vesselName != null)
                m_vesselName.text = newName;
        }

        public void SetPersistentThrustEnabledToggle(bool state)
        {
            if (m_persistentThrustToggle != null)
                m_persistentThrustToggle.isOn = state;
        }

        public void SetVesselInfoToggle(bool state)
        {
            if (m_vesselInfoToggle != null)
                m_vesselInfoToggle.isOn = state;

            //Turn on Vessel info window
        }

        public void SetVesselIcon(Sprite icon)
        {
            if (m_vesselIcon != null)
                m_vesselIcon.sprite = icon;

            //Turn on Vessel info window
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
        }

        // Listeners
        /*
        public void PersistentThrustToggle(bool isOn)
        {
            if (!loaded)
                return;

            if (m_persistentThrustToggle == null)
                return;

            //Turn on Persistent Thrust
        }
        */

        public void myButtonListener()
        {
            //Methods with no arguments can be added to any button or to any other element if the argument does not need to be specified
        }
    }
}
