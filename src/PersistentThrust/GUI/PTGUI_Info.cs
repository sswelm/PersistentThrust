using KSP.UI;
using PersistentThrust.SituationModules;
using PersistentThrust.UI;
using PersistentThrust.UI.Interface;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PersistentThrust
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class PTGUI_Info : MonoBehaviour, IInfoWindow
    {
        InfoWindow infoWindow;
        Periapsis peri;
        Apoapsis apo;
        SemiMajorAxis sma;
        Eccentricity ecc;
        Inclination inc;
        Altitude alt;
        Velocity vel;
        Acceleration acc;

        public static PTGUI_Info Instance { get; private set; } = null;
        public string VesselName { get; private set; }
        public float Throttle { get; set; }
        public float ThrottleChangedByKSP { get; set; }
        public bool IsVisible { get; private set; } = false;
        public bool DeltaVVisible { get; set; } = true;
        public double DeltaV { get; } = 0;
        public bool SituationVisible { get; set; } = true;
        public bool ThrottleVisible { get; set; } = true;
        public string SituationTextString { get; set; } = null;

        private List<IInfoModule> modules = new List<IInfoModule>();
        public List<IInfoModule> Modules => modules.ToList();
        public Vector2 Position { get; set; } = new Vector2();
        public float Scale
        {
            get { return PTGUI_Settings.Instance.UIScale * UIMasterController.Instance.uiScale; }
        }

        public Vessel currentVessel;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            Position = PTGUI_Settings.Instance.infoWindowPosition;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            Instance = null;

            if (apo != null)
                apo.IsActive = false;
            if (peri != null)
                peri.IsActive = false;
            if (sma != null)
                sma.IsActive = false;
            if (ecc != null)
                ecc.IsActive = false;
            if (inc != null)
                inc.IsActive = false;
            if (alt != null)
                alt.IsActive = false;
            if (vel != null)
                vel.IsActive = false;
            if (acc != null)
                acc.IsActive = false;
        }

        public InfoWindow OperWindow(Vessel vessel)
        {
            if (PTGUI_Loader.InfoWindowPrefab == null)
                return null;

            infoWindow = Instantiate(PTGUI_Loader.InfoWindowPrefab).GetComponent<InfoWindow>();

            if (infoWindow == null)
                return null;

            infoWindow.transform.SetParent(MainCanvasUtil.MainCanvas.transform);

            IsVisible = true;

            currentVessel = vessel;
            InitializeModules();
            UpdateDisplayInfo();

            infoWindow.SetInitialState(Instance);

            return infoWindow;
        }

        public void UpdateDisplayInfo()
        {
            if (!IsVisible)
                return;

            //IsActiveVessel = v.isActiveVessel;
            if (VesselName != currentVessel.vesselName)
                VesselName = currentVessel.vesselName;

            // Throttle
            if (ThrottleVisible)
            {
                if (currentVessel.loaded)
                {
                    Throttle = currentVessel.ctrlState.mainThrottle;
                }
                else
                {
                    Throttle = float.Parse(currentVessel.FindPersistentEngineModuleSnapshots().First().moduleValues.GetValue("persistentThrottle"));
                }
            }

            // DeltaV
            if (DeltaVVisible)
            {

            }

            // Situation Visible
            if (SituationVisible)
                SituationTextString = Vessel.GetSituationString(currentVessel);

            peri.IsActive = SituationVisible;
            apo.IsActive = SituationVisible;
            sma.IsActive = SituationVisible;
            ecc.IsActive = SituationVisible;
            inc.IsActive = SituationVisible;
            alt.IsActive = SituationVisible;
            vel.IsActive = SituationVisible;
            acc.IsActive = SituationVisible;
        }

        public void UpdatePersistentThrottle(float value)
        {
            Throttle = value;
            if (currentVessel.loaded)
            {
                currentVessel.ctrlState.mainThrottle = value;
            }
            else
            {
                foreach (var m in currentVessel.FindPersistentEngineModuleSnapshots())
                    m.moduleValues.values.SetValue(nameof(PersistentEngine.persistentThrottle), value.ToString());
            }
        }

        private void InitializeModules()
        {
            modules = new List<IInfoModule>();

            peri = new Periapsis("Periapsis");
            apo = new Apoapsis("Apoapsis");
            sma = new SemiMajorAxis("Semi-Major Axis");
            ecc = new Eccentricity("Eccentricity");
            inc = new Inclination("Inclination");
            alt = new Altitude("Current Altitude");
            vel = new Velocity("Current Speed");
            acc = new Acceleration("Current Acceleration");

            peri.IsVisible = PTGUI_Settings.Instance.showPeriapsis;
            apo.IsVisible = PTGUI_Settings.Instance.showApoapsis;
            sma.IsVisible = PTGUI_Settings.Instance.showSMA;
            ecc.IsVisible = PTGUI_Settings.Instance.showApoapsis;
            inc.IsVisible = PTGUI_Settings.Instance.showInclination;
            alt.IsVisible = PTGUI_Settings.Instance.showAltitude;
            vel.IsVisible = PTGUI_Settings.Instance.showVelocity;
            acc.IsVisible = PTGUI_Settings.Instance.showAcceleration;

            peri.Vessel = currentVessel;
            apo.Vessel = currentVessel;
            sma.Vessel = currentVessel;
            ecc.Vessel = currentVessel;
            inc.Vessel = currentVessel;
            alt.Vessel = currentVessel;
            vel.Vessel = currentVessel;
            acc.Vessel = currentVessel;

            modules.Add(peri);
            modules.Add(apo);
            modules.Add(sma);
            modules.Add(ecc);
            modules.Add(inc);
            modules.Add(alt);
            modules.Add(vel);
            modules.Add(acc);
        }

        public void CloseWindow()
        {
            IsVisible = false;

            PTGUI_Settings.Instance.infoWindowPosition = Position;

            if (infoWindow != null)
                infoWindow.gameObject.DestroyGameObject();
        }

        public void SetInputLock(bool on)
        {
            if (on)
            {
                InputLockManager.SetControlLock("ThrottleInputField");
            }
            else
                InputLockManager.ClearControlLocks();
        }

        public void ClampToScreen(RectTransform rect)
        {
            UIMasterController.ClampToScreen(rect, -rect.sizeDelta / 3);
        }
    }
}
