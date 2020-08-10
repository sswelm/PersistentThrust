using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PersistentThrust.UI.Interface;

namespace PersistentThrust.UI
{
    public class InfoWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField]
        private GameObject m_ModulePrefab = null;
        [SerializeField]
        private Transform m_ModuleTransform = null;
        [SerializeField]
        public Text m_vesselName = null;
        [SerializeField]
        public Toggle m_deltaVToggle = null;
        [SerializeField]
        public Toggle m_situationToggle = null;
        [SerializeField]
        public Toggle m_throttleToggle = null;
        [SerializeField]
        public Slider m_throttleSlider = null;
        [SerializeField]
        public InputField m_throttleInput = null;

        [SerializeField]
        public Text m_situationInfo = null;

        private bool dragging = false;
        private bool throttleWasChanged = false;

        private Vector2 mouseStart;
        private Vector3 windowStart;
        private RectTransform rect;

        private IInfoWindow infoWindowInterface;
        private List<InfoModule> Modules = new List<InfoModule>();

        private void Awake()
        {
            m_throttleInput.text = "0";
            rect = GetComponent<RectTransform>();
            //m_throttleInput.onValidateInput += delegate (string input, int charIndex, char addedChar) { return ValidateThrottleInputChar(addedChar); };
            m_throttleInput.onEndEdit.AddListener(delegate
            {
                ValidateThrottle(m_throttleInput.text);
                infoWindowInterface.SetInputLock(false);
                m_throttleInput.DeactivateInputField();
                m_throttleSlider.value = float.Parse(m_throttleInput.text);
            });
            m_throttleSlider.onValueChanged.AddListener(delegate
            {
                if(m_throttleSlider.value != float.Parse(m_throttleInput.text))
                    UpdateInputField(m_throttleSlider.value);

                UpdatePersistentThrottle(m_throttleSlider.value);
            });
        }

        public void SetInitialState(IInfoWindow info)
        {
            if (info == null)
                return;

            infoWindowInterface = info;

            if (m_vesselName != null)
                m_vesselName.text = info.VesselName;

            if (m_throttleInput != null)
                m_throttleInput.GetComponent<ThrottleInputField>().SetInterface(info);

            SetPosition(info.Position);

            transform.localScale *= info.Scale;

            m_deltaVToggle.isOn = info.DeltaVVisible;
            m_situationToggle.isOn = info.SituationVisible;
            m_throttleToggle.isOn = info.ThrottleVisible;

            CreateSituationPanel(info.Modules);
            UpdateInfo();
        }

        public void Update()
        {
            if (infoWindowInterface is null || !infoWindowInterface.IsVisible) return;

            UpdateInfo();
        }

        private void UpdateInfo()
        {
            if(infoWindowInterface.ThrottleVisible && !throttleWasChanged)
            {
                m_throttleSlider.value = infoWindowInterface.Throttle;
                m_throttleInput.text = infoWindowInterface.Throttle.ToString("G4");
            }

            if (throttleWasChanged && m_throttleSlider.value == infoWindowInterface.Throttle)
                throttleWasChanged = false;

            if (infoWindowInterface.SituationVisible)
            {
                m_situationInfo.text = infoWindowInterface.SituationTextString;
                for (int i = Modules.Count - 1; i >= 0; i--)
                {
                    InfoModule mod = Modules[i];

                    if (mod == null)
                        continue;

                    if (!mod.IsVisible)
                    {
                        if (mod.gameObject.activeSelf)
                            mod.gameObject.SetActive(false);

                        continue;
                    }

                    if (mod.IsActive)
                    {
                        if (!mod.gameObject.activeSelf)
                            mod.gameObject.SetActive(true);

                        mod.UpdateModule();
                    }
                    else if (mod.gameObject.activeSelf)
                        mod.gameObject.SetActive(false);
                }
            }

            if (infoWindowInterface.DeltaVVisible)
            {

            }
        }

        private char ValidateThrottleInputChar(char charToValidate)
        {
            if (!Char.IsDigit(charToValidate) && charToValidate != '.')
            {
                if (charToValidate == ',')
                    return '.';
                else
                    charToValidate = '\0';
            }
            return charToValidate;
        }

        /// <summary>
		/// Creates the individual readout modules for the situation panel.
		/// </summary>
		/// <param name="modules">The list of available readout modules</param>
        private void CreateSituationPanel(List<IInfoModule> modules)
        {
            if (modules == null)
                return;

            if (infoWindowInterface == null)
                return;

            if (m_ModulePrefab == null || m_ModuleTransform == null)
                return;

            for (int i = modules.Count - 1; i >= 0; i--)
            {
                IInfoModule module = modules[i];

                if (module == null)
                    continue;

                CreateModule(module);
            }
        }

        /// <summary>
		/// Creates the individual readout module using the Situation Module prefab.
		/// </summary>
		/// <param name="module">The readout module interface</param>
		private void CreateModule(IInfoModule module)
        {
            GameObject mod = Instantiate(m_ModulePrefab);

            if (mod == null)
                return;

            mod.transform.SetParent(m_ModuleTransform, false);

            InfoModule bMod = mod.GetComponent<InfoModule>();

            if (bMod == null)
                return;

            bMod.SetModule(module);

            bMod.gameObject.SetActive(module.IsVisible);

            Modules.Add(bMod);
        }

        private void ValidateThrottle(string input)
        {
            if (string.IsNullOrEmpty(input))
                m_throttleInput.text = m_throttleSlider.value.ToString();

            float throttleNum = float.Parse(input);

            Mathf.Clamp(throttleNum, 0, 1);

            m_throttleInput.text = throttleNum.ToString();
        }

        private void UpdateInputField(float input)
        {
            m_throttleInput.text = input.ToString("G4");
        }

        private void UpdatePersistentThrottle(float value)
        {
            throttleWasChanged = true;
            infoWindowInterface.UpdatePersistentThrottle(value);
        }

        /// <summary>
		/// Interface method to begin drag operation
		/// </summary>
		/// <param name="eventData"></param>
		public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = true;

            if (rect == null)
                return;

            mouseStart = eventData.position;
            windowStart = rect.position;
        }

        /// <summary>
        /// Interface method to update the panel position on drag
        /// </summary>
        /// <param name="eventData"></param>
        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging)
                return;

            if (rect == null)
                return;

            if (infoWindowInterface == null)
                return;

            rect.position = windowStart + (Vector3)(eventData.position - mouseStart);
        }

        /// <summary>
        /// Interface method to end drag operation and clamp the panel to the screen
        /// </summary>
        /// <param name="eventData"></param>
        public void OnEndDrag(PointerEventData eventData)
        {
            dragging = false;

            if (rect == null)
                return;

            if (infoWindowInterface == null)
                return;

            infoWindowInterface.ClampToScreen(rect);

            infoWindowInterface.Position = new Vector2(rect.anchoredPosition.x, rect.anchoredPosition.y);
        }

        /// <summary>
		/// Sets the panel position
		/// </summary>
		/// <param name="v">The x and y coordinates of the panel, measured from the top-left</param>
		private void SetPosition(Vector2 v)
        {
            if (rect == null)
                return;

            rect.anchoredPosition = new Vector3(v.x, v.y > 0 ? v.y * -1 : v.y, 0);
        }
    }
}
