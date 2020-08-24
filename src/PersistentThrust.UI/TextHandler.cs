using UnityEngine;
using UnityEngine.Events;

namespace PersistentThrust.UI
{
    public class TextHandler : MonoBehaviour
    {
        public class OnTextEvent : UnityEvent<string> { }

        public class OnColorEvent : UnityEvent<Color> { }

        public class OnFontEvent : UnityEvent<int> { }

        private OnTextEvent _onTextUpdate = new OnTextEvent();
        private OnColorEvent _onColorUpdate = new OnColorEvent();
        private OnFontEvent _onFontChange = new OnFontEvent();

        public Vector2 PreferredSize { get; set; } = new Vector2();

        public UnityEvent<string> OnTextUpdate
        {
            get { return _onTextUpdate; }
        }

        public UnityEvent<Color> OnColorUpdate
        {
            get { return _onColorUpdate; }
        }

        public UnityEvent<int> OnFontChange
        {
            get { return _onFontChange; }
        }
    }
}
