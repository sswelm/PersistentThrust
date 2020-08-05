using UnityEngine;
using UnityEngine.UI;

namespace PersistentThrust.UI
{
    class TooltipText : MonoBehaviour
    {
        public static Text text;

        void Start()
        {
            text = gameObject.GetComponent<Text>();
            text.text = "Go To Vessel";
        }
        void Update()
        {
            transform.position = Input.mousePosition;
        }
    }
}
