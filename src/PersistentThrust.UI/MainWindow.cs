using PersistentThrust.UI.Interface;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PersistentThrust.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class MainWindow : MonoBehaviour
    {
        [SerializeField]
        public Text m_VersionText = null;
        [SerializeField]
        public GameObject m_VesselElementPrefab = null;
        [SerializeField]
        public Transform m_VesselListTransform = null;

        private Dictionary<string, VesselElement> vesselElements = new Dictionary<string, VesselElement>();
        private IMainWindow mainWindowInterface;

        public void setInitialState(IMainWindow main)
        {
            if (main == null)
                return;

            mainWindowInterface = main;

            if (m_VersionText != null)
                m_VersionText.text = main.Version;

            CreateVesselList(main.GetVessels);
        }

        public void Update()
        {
            if (mainWindowInterface is null || !mainWindowInterface.IsVisible) return;
        }

        public void UpdateVesselList()
        {
            //foreach
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
            Debug.Log("[PersistentThrust]: Instantiating IVesselElement");
            GameObject obj = Instantiate(m_VesselElementPrefab);

            if (obj == null)
                return;

            obj.transform.SetParent(m_VesselListTransform, false);

            VesselElement vElement = obj.GetComponent<VesselElement>();

            if (vElement == null)
                return;

            vElement.setElement(elementInterface);

            vElement.gameObject.SetActive(mainWindowInterface.IsVisible);

            vesselElements.Add(elementInterface.VesselName, vElement);
        }

        // get all vessels currently in flight to make a dynamic list in the scroll view content window
    }
}
