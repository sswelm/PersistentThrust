using PersistentThrust.UI.Interface;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace PersistentThrust.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class MainWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField]
        public Text m_VersionText = null;
        [SerializeField]
        public GameObject m_VesselElementPrefab = null;
        [SerializeField]
        public Transform m_VesselListTransform = null;
        private bool dragging = false;

        private Vector2 mouseStart;
        private Vector3 windowStart;
        private RectTransform rect;

        private Dictionary<Guid, VesselElement> vesselElements = new Dictionary<Guid, VesselElement>();
        private IMainWindow mainWindowInterface;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
        }

        public void setInitialState(IMainWindow main)
        {
            if (main == null)
                return;

            mainWindowInterface = main;

            m_VesselElementPrefab = main.VesselElementPrefab;

            if (m_VersionText != null)
                m_VersionText.text = main.Version;

            SetPosition(main.Position);

            transform.localScale *= main.Scale;

            CreateVesselList(main.Vessels);
        }

        public void Update()
        {
            if (mainWindowInterface is null || !mainWindowInterface.IsVisible) return;

            UpdateVesselInformation(mainWindowInterface.Vessels);
        }

        private void UpdateVesselInformation(IList<IVesselElement> vesselInterfaces)
        {
            foreach (var vInterface in vesselInterfaces)
            {
                vesselElements[vInterface.VesselId].UpdateElement(vInterface);
            }
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

            if (mainWindowInterface == null)
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

            if (mainWindowInterface == null)
                return;

            mainWindowInterface.ClampToScreen(rect);

            mainWindowInterface.Position = new Vector2(rect.anchoredPosition.x, rect.anchoredPosition.y);
        }

        private void CreateVesselList(IList<IVesselElement> elements)
        {
            if (elements == null)
                return;

            if (m_VesselElementPrefab == null || m_VesselListTransform == null)
                return;

            for (int i = elements.Count - 1; i >= 0; i--)
            {
                IVesselElement e = elements[i];

                if (e == null)
                    continue;

                AddVessel(e);
            }
        }

        private void AddVessel(IVesselElement elementInterface)
        {
            GameObject obj = Instantiate(m_VesselElementPrefab) as GameObject;

            if (obj == null)
                return;

            obj.transform.SetParent(m_VesselListTransform, false);

            VesselElement vElement = obj.GetComponent<VesselElement>();

            if (vElement == null)
                return;

            vElement.SetElement(elementInterface);

            vElement.gameObject.SetActive(mainWindowInterface.IsVisible);

            vesselElements.Add(elementInterface.VesselId, vElement);
        }

        public void RemoveVessel(IVesselElement elementInterface)
        {
            if (!vesselElements.ContainsKey(elementInterface.VesselId))
                return;

            Destroy(vesselElements[elementInterface.VesselId].gameObject);
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
