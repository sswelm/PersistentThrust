using PersistentThrust.UI.Interface;
using System.Diagnostics.Eventing.Reader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PersistentThrust.UI
{
    [RequireComponent(typeof(InputField))]
    public class ThrottleInputField : MonoBehaviour, IPointerClickHandler
    {
        IInfoWindow window;
        private InputField inputField;

        public void SetInterface(IInfoWindow i)
        {
            window = i;
            inputField = gameObject.GetComponent<InputField>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            SetInputLocks(true);
            inputField.Select();
            window.ThrottleVisible = false;
        }

        private void SetInputLocks(bool on)
        {
            window.SetInputLock(on);
        }
    }
}
