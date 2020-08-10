using PersistentThrust.UI.Interface;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PersistentThrust.UI
{
    public class ThrottleInputField : MonoBehaviour, ISelectHandler
    {
        IInfoWindow main;

        public void SetInterface(IInfoWindow inputField)
        {
            main = inputField;
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetInputLocks(true);
            main.ThrottleVisible = false;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetInputLocks(false);
            main.ThrottleVisible = true;
        }

        private void SetInputLocks(bool on)
        {
            main.SetInputLock(on);
        }
    }
}
