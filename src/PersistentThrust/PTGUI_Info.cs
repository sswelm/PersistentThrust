using KSP.UI;
using PersistentThrust.UI;
using PersistentThrust.UI.Interface;
using System.Linq;
using UnityEngine;


namespace PersistentThrust
{
    public class PTGUI_Info : MonoBehaviour, IInfoWindow
    {
        InfoWindow infoWindow;

        public static PTGUI_Info Instance { get; private set; } = null;
        public string VesselName { get; private set; }
        public float Throttle { get; set; }
        public bool IsVisible { get; private set; }
        public bool DeltaVVisible { get; set; }
        public bool SituationVisible { get; set; }
        public bool ThrottleVisible { get; set; }
        public Vector2 Position { get; set; } = new Vector2();
        public float Scale { get; private set; }

        public Vessel currentVessel;

        public PTGUI_Info(Vessel v)
        {
            Instance = this;
            currentVessel = v;
            //IsActiveVessel = v.isActiveVessel;
            VesselName = v.vesselName;
        }

        // here or in PTGUI??
        public void OperWindow(Vessel vessel)
        {
            if (PTGUI_Loader.InfoWindowPrefab == null)
                return;

            infoWindow = Instantiate(PTGUI_Loader.InfoWindowPrefab).GetComponent<InfoWindow>();

            if (infoWindow == null)
                return;

            infoWindow.transform.SetParent(MainCanvasUtil.MainCanvas.transform);

            IsVisible = true;

            infoWindow.SetInitialState(Instance);
        }
        public void DisplayInfoForVessel(Vessel v)
        {
            currentVessel = v;
        }

        public void UpdateDisplayInfo()
        {
            if (!IsVisible)
                return;

            //IsActiveVessel = v.isActiveVessel;
            if(VesselName != currentVessel.vesselName)
                VesselName = currentVessel.vesselName;
            if (DeltaVVisible) ;
            if (SituationVisible) ;
            if (ThrottleVisible)
            {
                if (!currentVessel.packed)
                    Throttle = currentVessel.ctrlState.mainThrottle;
                else
                {
                    Throttle = float.Parse(currentVessel.FindPersistentEngineModuleSnapshots().First().moduleValues.GetValue("persistentThrottle"));
                }
            }
        }

        public void CloseWindow()
        {
            IsVisible = false;
            PTGUI_Settings.Instance.infoWindowPosition = Position;
            infoWindow.gameObject.DestroyGameObject();
            Instance = null;
        }


        public void ClampToScreen(RectTransform rect)
        {
            UIMasterController.ClampToScreen(rect, -rect.sizeDelta / 3);
        }
    }
}
