using UnityEngine;
using UnityEngine.UI;

namespace PersistentThrust.UI
{
    public class Style : MonoBehaviour
    {
        public enum ElementTypes
        {
            None,
            Window,
            Box,
            Button,
            Toggle,
            Slider,
            Scrollbar,
            Scrollview
        }

        [SerializeField]
        private ElementTypes m_ElementType = ElementTypes.None;

        public ElementTypes ElementType
        {
            get { return m_ElementType; }
        }

        private void SetSelectable(Sprite normal, Sprite highlight, Sprite active, Sprite inactive)
        {
            Selectable select = GetComponent<Selectable>();

            if (select == null)
                return;

            select.image.sprite = normal;
            select.image.type = Image.Type.Sliced;
            select.transition = Selectable.Transition.SpriteSwap;

            SpriteState spriteState = select.spriteState;
            spriteState.highlightedSprite = highlight;
            spriteState.pressedSprite = active;
            spriteState.disabledSprite = inactive;
            select.spriteState = spriteState;
        }

        public void SetImage(Sprite sprite, Image.Type type)
        {
            Image image = GetComponent<Image>();

            if (image == null)
                return;

            image.sprite = sprite;
            image.type = type;
        }

        public void SetButton(Sprite normal, Sprite highlight, Sprite active, Sprite inactive)
        {
            SetSelectable(normal, highlight, active, inactive);
        }

        public void SetToggle(Sprite normal, Sprite highlight, Sprite active, Sprite inactive)
        {
            SetSelectable(normal, highlight, active, inactive);

            Toggle toggle = GetComponent<Toggle>();

            if (toggle == null)
                return;

            //The "checkmark" sprite is replaced with the "active" sprite; this is only displayed when the toggle is in the true state
            Image toggleImage = toggle.graphic as Image;

            if (toggleImage == null)
                return;

            toggleImage.sprite = active;
            toggleImage.type = Image.Type.Sliced;
        }

        public void SetSlider(Sprite background, Sprite thumbNormal, Sprite thumbHighlight, Sprite thumbActive, Sprite thumbInactive)
        {
            //The slider thumb is the selectable component
            SetSelectable(thumbNormal, thumbHighlight, thumbActive, thumbInactive);

            if (background == null)
                return;

            Slider slider = GetComponent<Slider>();

            if (slider == null)
                return;

            Image back = slider.GetComponentInChildren<Image>();

            if (back == null)
                return;

            back.sprite = background;
            back.type = Image.Type.Sliced;
        }

        public void SetScrollbar(Sprite background, Sprite handleNormal, Sprite handleHighlight, Sprite handleActive, Sprite handleInactive)
        {
            //The scrollbar handle is the selectable component
            SetSelectable(handleNormal, handleHighlight, handleActive, handleInactive);

            if (background == null)
                return;

            Scrollbar scrollbar = GetComponent<Scrollbar>();

            if (scrollbar == null)
                return;

            Image back = scrollbar.GetComponent<Image>();

            if (back == null)
                return;

            back.sprite = background;
            back.type = Image.Type.Sliced;
        }
    }
}
