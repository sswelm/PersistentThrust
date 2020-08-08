using PersistentThrust.UI.Interface;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PersistentThrust.UI
{
    public class InfoWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
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

        private bool dragging = false;

        private Vector2 mouseStart;
        private Vector3 windowStart;
        private RectTransform rect;

        private IInfoWindow infoWindowInterface;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
        }

        public void SetInitialState(IInfoWindow info)
        {
            if (info == null)
                return;

            infoWindowInterface = info;

            if (m_vesselName != null)
                m_vesselName.text = info.VesselName;

            SetPosition(info.Position);

            transform.localScale *= info.Scale;
            UpdateInfo();
        }

        public void Update()
        {
            if (infoWindowInterface is null || !infoWindowInterface.IsVisible) return;

            UpdateInfo();
        }

        private void UpdateInfo()
        {
            if(infoWindowInterface.DeltaVVisible)
                m_throttleSlider.value = infoWindowInterface.Throttle;
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
