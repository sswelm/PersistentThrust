using KSP.UI.Screens;
using PersistentThrust.UI;
using PersistentThrust.UI.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PersistentThrust
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class PTGUI : MonoBehaviour, IMainWindow
    {
        MainWindow window;
        ApplicationLauncherButton button;
        public string Version { get; private set; }
        public bool IsVisible { get; private set; } = false;
        public static PTGUI Instance { get; private set; } = null;
        public Dictionary<string, IVesselElement> IvesselElements { get; set; } = new Dictionary<string, IVesselElement>();


        private void Awake()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
            PTGUI_Loader.LoadTextures();

            Instance = this;
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        private void Update()
        {
            if (Instance is null) return;

            foreach (Vessel v in FlightGlobals.Vessels)
            {
                AddVesselToIVesselList(v);
            }
        }

        private void OpenWindow()
        {
            if (PTGUI_Loader.PanelPrefab == null)
                return;

            GameObject obj = Instantiate(PTGUI_Loader.PanelPrefab, new Vector3(0, 0, 0), Quaternion.identity) as GameObject;

            if (obj == null)
                return;

            obj.transform.SetParent(MainCanvasUtil.MainCanvas.transform);

            window = obj.GetComponent<MainWindow>();

            if (window == null)
                return;

            IsVisible = true;

            window.setInitialState(Instance);
        }

        private void CloseWindow()
        {
            IsVisible = false;
            window.enabled = false;

            IvesselElements.Clear();

            if (window != null)
                window.gameObject.SetActive(false);
        }

        private void OnSceneChange(GameScenes s)
        {
            if (s == GameScenes.EDITOR)
                CloseWindow();
        }

        private void OnGUIAppLauncherReady()
        {
            try
            {
                button = ApplicationLauncher.Instance.AddModApplication(
                    OpenWindow,
                    CloseWindow,
                    null,
                    null,
                    null,
                    null,
                    (ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW | ApplicationLauncher.AppScenes.TRACKSTATION),
                    GameDatabase.Instance.GetTexture("PersistentThrust/Textures/toolbar", false));
                GameEvents.onGameSceneLoadRequested.Add(this.OnSceneChange);
            }
            catch (Exception ex)
            {
                Debug.LogError("[PersistentThrust]: Failed to add Applauncher button");
                Debug.LogException(ex);
            }
        }

        public void OnDestroy()
        {
            CloseWindow();
            ApplicationLauncher.Instance.RemoveModApplication(button);
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
        }

        private void AddVesselToIVesselList(Vessel v)
        {
            bool keep = true;
            if (v.isEVA || !v.isCommandable || v.vesselType == VesselType.SpaceObject)
                keep = false;
            if (v.situation != Vessel.Situations.ORBITING && v.situation != Vessel.Situations.SUB_ORBITAL && v.situation != Vessel.Situations.ESCAPING)
                keep = false;

            // Fails filter, so remove from the dictionary
            if (!keep)
            {
                IvesselElements.Remove(v.vesselName);
                return; // Fails filter, so return
            }

            if (IvesselElements.TryGetValue(v.vesselName, out var x)) // Already in the dictionary, so update the dictionary entry
            {
                x.VesselName = v.vesselName;
                x.VesselIcon = PTGUI_Vessel.GetVesselTypeIcon(v.vesselType);
                x.HasPersistentThrustActive = v.HasPersistentThrustEnabled();
            }

            var veInterface = new PTGUI_Vessel // Add new vessel to the list
            {
                VesselName = v.vesselName,
                VesselIcon = PTGUI_Vessel.GetVesselTypeIcon(v.vesselType),
                HasPersistentThrustActive = v.HasPersistentThrustEnabled(),
                HasInfoWindowActive = false,
            };

            IvesselElements[v.vesselName] = veInterface;
        }

        public IList<IVesselElement> GetVessels
        {
            get { return new List<IVesselElement>(IvesselElements.Values.ToArray()); }
        }
    }
}
