using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PersistentThrust.UI
{
    class TooltipText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Text m_tooltip = null;
        private Image m_background = null;
        private Button m_button = null;

        private RectTransform textRectTransform;
        private RectTransform backgroudRectTransform;

        private float textPaddingSize = 2f;
        private Vector3 offset = new Vector3(15, -15, 0);

        void Start()
        {
            m_button = gameObject.GetComponent<Button>();
            m_tooltip = gameObject.transform.Find("Tooltip").gameObject.GetComponent<Text>();
            m_background = gameObject.transform.Find("Background").gameObject.GetComponent<Image>();

            if (m_tooltip is null)
                return;

            if (m_background is null)
                return;

            backgroudRectTransform = m_background.gameObject.GetComponent<RectTransform>();
            textRectTransform = m_tooltip.gameObject.GetComponent<RectTransform>();

            HideTooltip();
        }

        private void Update()
        {
            //m_tooltip.transform.position = Input.mousePosition + offset
            //m_background.transform.position = Input.mousePosition + offset
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (m_button.enabled)
                ShowTooltip("Go to vessel");
            else
                ShowTooltip("Active vessel");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideTooltip();
        }

        private void ShowTooltip(string text)
        {
            m_tooltip.text = text;
            Vector2 backgroundSize = new Vector2(m_tooltip.preferredWidth + textPaddingSize * 2f, m_tooltip.preferredHeight);
            backgroudRectTransform.sizeDelta = backgroundSize;
            textRectTransform.sizeDelta = backgroundSize;

            m_tooltip.gameObject.SetActive(true);
            m_background.gameObject.SetActive(true);
            m_tooltip.transform.SetAsLastSibling();
            m_background.transform.SetAsLastSibling();
        }

        private void HideTooltip()
        {
            m_tooltip.gameObject.SetActive(false);
            m_background.gameObject.SetActive(false);
        }
    }
}
