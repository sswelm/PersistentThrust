using KSP.UI;
using KSP.UI.Screens;
using PersistentThrust.UI;
using PersistentThrust.UI.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;

namespace PersistentThrust
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class PTGUI : MonoBehaviour, IMainWindow
    {
        ApplicationLauncherButton button;
        MainWindow window;
        InfoWindow infoWindow;

        public Vector2 Position { get; set; } = new Vector2();
        public string Version { get; private set; }
        public bool IsVisible { get; private set; } = false;
        public static PTGUI Instance { get; private set; } = null;
        public GameObject VesselElementPrefab { get; private set; }
        public Dictionary<Guid, IVesselElement> IvesselElements { get; set; } = new Dictionary<Guid, IVesselElement>();
        public List<IVesselElement> Vessels => IvesselElements.Values.ToList();
        public float Scale
        {
            get { return PTGUI_Settings.Instance.UIScale * UIMasterController.Instance.uiScale; }
        }

        /// <summary>
        /// Called by Unity when the script is being loaded, which is at every scene change.
        /// Adds the event adding the appLauncher button to onGuiApplicationLauncherReady.
        /// Loads the vessel icons. Gets the assembly version.
        /// </summary>
        private void Awake()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
            PTGUI_Loader.LoadTextures();

            Instance = this;
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            if (VesselElementPrefab is null)
                VesselElementPrefab = PTGUI_Loader.VesselElementPrefab;
        }

        private void Start()
        {
            Position = PTGUI_Settings.Instance.mainWindowPosition;
        }

        /// <summary>
        /// Called every frame by Unity if the MonoBehaviour is enabled.
        /// Updates the IVesselElements list.
        /// </summary>
        private void Update()
        {
            if (Instance is null || !IsVisible) return; // GUI not initialized or not visible, no need to update anything.

            for (int i = FlightGlobals.Vessels.Count - 1; i >= 0; i--)
            {
                AddVesselToIVesselList(FlightGlobals.Vessels[i]);
            }

            if (infoWindow != null)
                PTGUI_Info.Instance.UpdateDisplayInfo();
        }

        /// <summary>
        /// Instantiates a new MainWindow prefab GameObject, sets its parent to the MainCanvas and saves a reference to the MainWindow component.
        /// Sets IsVisible to true, Updates the IVesselElements list, initializes the window.
        /// </summary>
        private void OpenWindow()
        {
            if (PTGUI_Loader.MainWindowPrefab == null)
                return;

            window = Instantiate(PTGUI_Loader.MainWindowPrefab).GetComponent<MainWindow>();

            if (window == null)
                return;

            window.transform.SetParent(MainCanvasUtil.MainCanvas.transform);

            IsVisible = true;

            // Update the vessel list by clearing it and forcing an Update.
            IvesselElements.Clear();
            Update();

            // Pass the instance to the MainWindow class as an interface to initialize the window.
            window.SetInitialState(Instance);
        }

        /// <summary>
        /// Closes the main window. Sets IsVisible to false.
        /// </summary>
        private void CloseWindow()
        {
            CloseInfoWindow();

            IsVisible = false;

            PTGUI_Settings.Instance.mainWindowPosition = Position;

            if (window != null)
                window.gameObject.DestroyGameObject();
            if (infoWindow != null)
                infoWindow.gameObject.DestroyGameObject();
        }

        public void OpenInfoWindow(Vessel vessel)
        {
            infoWindow = PTGUI_Info.Instance.OperWindow(vessel);
        }

        public void CloseInfoWindow()
        {
            if (PTGUI_Info.Instance != null)
                PTGUI_Info.Instance.CloseWindow();
        }

        /// <summary>
        /// Tries adding a applicationLauncher button for the GUI. Logs the exception if it fails.
        /// </summary>
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
                    GameDatabase.Instance.GetTexture("PersistentThrust/Resources/Textures/toolbar", false));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PersistentThrust]: Failed to add Applauncher button");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Called by Unity when the MonoBehaviour is destroyed. Closes the window and prevents duplicate toolbar buttons.
        /// </summary>
        public void OnDestroy()
        {
            CloseWindow();
            ApplicationLauncher.Instance.RemoveModApplication(button); // needed since the script gets re-initialized by KSP at every scene change.
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady); // removes the method that adds the button, otherwise it would get added multiple times.

            if (PTGUI_Settings.Instance.Save())
                Debug.Log($"[PersistentThrustSettings]: Settings saved.");
        }

        /// <summary>
        /// Checks if a vessel is valid. If it is, creates a new PTGUI_Vessel Monobehaviour and adds its interface to the IVesselElements list.
        /// Otherwise, it removes it from the vessel list.
        /// </summary>
        /// <param name="v"> Vessel that is being examined. </param>
        private void AddVesselToIVesselList(Vessel v)
        {
            // Check if vessel is valid, in a valid situation, and if it has any persistent engine
            bool remove;

            if (IvesselElements.ContainsKey(v.id))
                remove = !v.IsVesselValid() || !v.IsVesselSituationValid();
            else
                remove = !v.IsVesselValid() || !v.IsVesselSituationValid() || !v.HasPersistentEngineModules();

            // If not in the dictionary and valid, add it
            if (!IvesselElements.ContainsKey(v.id) && !remove)
            {
                var veInterface = PTGUI_Vessel.Create(v); // Add new vessel to the list and instantiate
                IvesselElements[v.id] = veInterface;
                veInterface.HasPersistentThrustActive = veInterface.PersistentThrustEnabled(v).Contains(true);
            }

            // Fails filter, so remove the vessel
            if (remove && IvesselElements.ContainsKey(v.id))
            {
                window.RemoveVessel(IvesselElements[v.id]);
                IvesselElements.Remove(v.id);
            }
        }

        public void ClampToScreen(RectTransform rect)
        {
            UIMasterController.ClampToScreen(rect, -rect.sizeDelta / 2);
        }
    }
}
