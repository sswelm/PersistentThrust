using PersistentThrust.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace PersistentThrust
{
    /// <summary>
	/// Extension of the TextMeshProUGUI class that allows for text field updating through Unity Events
	/// </summary>
    public class PTTextMeshProHolder : TextMeshProUGUI
    {
        /// <summary>
		/// Reference to the text script attached to the same GameObject; attach the script either in the Unity Editor
		/// or through code at startup
		/// </summary>
        private TextHandler _handler;

        /// <summary>
		/// Get the attached TextHandler and add an event listener
		/// </summary>
        new private void Awake()
        {
            base.Awake();

            _handler = GetComponent<TextHandler>();

            if (_handler == null)
                return;

            _handler.OnTextUpdate.AddListener(new UnityAction<string>(UpdateText));
            _handler.OnColorUpdate.AddListener(new UnityAction<Color>(UpdateColor));
            _handler.OnFontChange.AddListener(new UnityAction<int>(UpdateFontSize));
        }

        private void UpdateColor(Color c)
        {
            color = c;
        }

        private void UpdateText(string t)
        {
            text = t;

            _handler.PreferredSize = new Vector2(preferredWidth, preferredHeight);
        }

        private void UpdateFontSize(int i)
        {
            fontSize += i;
        }
    }
}
