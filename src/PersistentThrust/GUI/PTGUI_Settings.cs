using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PersistentThrust
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    class PTGUI_Settings : MonoBehaviour
    {
        [Persistent]
        public Vector2 mainWindowPosition = new Vector2(100, -100);
        [Persistent]
        public Vector2 infoWindowPosition = new Vector2(800, -100);
        [Persistent]
        public bool showPeriapsis = true;
        [Persistent]
        public bool showApoapsis = true;
        [Persistent]
        public bool showSMA = true;
        [Persistent]
        public bool showEccentricity = true;
        [Persistent]
        public bool showInclination = true;
        [Persistent]
        public bool showAltitude = true;
        [Persistent]
        public bool showVelocity = true;
        [Persistent]
        public bool showAcceleration = true;
        [Persistent]
        public float UIScale = 0.8f;

        private const string fileName = "PluginData/settings.cfg";
        private string fullPath;

        private static bool loaded;

        public static PTGUI_Settings Instance { get; set; }

        private void Awake()
        {
            if (loaded)
                Destroy(gameObject);

            DontDestroyOnLoad(gameObject);

            loaded = true;

            Instance = this;

            fullPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), fileName).Replace("\\", "/");

            if (Load())
                Debug.Log("[PersistentThrustSettings]: Settings file loaded");
            else if(Save())
                Debug.Log($"[PersistentThrustSettings]: New Settings files generated at:\n{fullPath}");
        }

        private void OnDestroy()
        {
            if(Save())
                Debug.Log($"[PersistentThrustSettings]: New Settings files generated at:\n{fullPath}");
        }

        public bool Load()
        {
            bool settingsLoaded;

            try
            {
                if (File.Exists(fullPath))
                {
                    ConfigNode node = ConfigNode.Load(fullPath);
                    ConfigNode unwrapped = node.GetNode(GetType().Name);
                    ConfigNode.LoadObjectFromConfig(this, unwrapped);
                    settingsLoaded = true;
                }
                else
                {
                    Debug.Log($"[PersistentThrustSettings]: Settings file could not be found [{fullPath}]");
                    settingsLoaded = false;
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"[PersistentThrustSettings]: Error while loading settings file from [{fullPath}]\n{ex}");
                settingsLoaded = false;
            }

            return settingsLoaded;
        }

        public bool Save()
        {
            bool settingsSaved;

            try
            {
                ConfigNode node = AsConfigNode();
                ConfigNode wrapper = new ConfigNode(GetType().Name);
                wrapper.AddNode(node);
                wrapper.Save(fullPath);
                settingsSaved = true;
            }
            catch (Exception ex)
            {
                Debug.Log($"[PersistentThrustSettings]: Error while saving settings file from [{fullPath}]\n{ex}");
                settingsSaved = false;
            }

            return settingsSaved;
        }

        private ConfigNode AsConfigNode()
        {
            try
            {
                ConfigNode node = new ConfigNode(GetType().Name);

                node = ConfigNode.CreateConfigFromObject(this, node);
                return node;
            }
            catch (Exception ex)
            {
                Debug.Log($"[PersistentThrustSettings]: Failed to generate settings file node...\n{ex}");
                return new ConfigNode(GetType().Name);
            }
        }
    }
}

