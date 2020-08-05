using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PersistentThrust
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class PTGUI_Loader : MonoBehaviour
    {
        public static GameObject PanelPrefab { get; private set; }
        public static Dictionary<VesselType, Sprite> vesselSprites;

        /// <summary>
        /// Called by Unity at initialization, which happens as soon as the game starts.
        /// Loads the GUI prefabs.
        /// </summary>
        private void Awake()
        {
            AssetBundle prefabs = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ptui.dat"));

            PanelPrefab = prefabs.LoadAsset("PTUIPanel") as GameObject;
        }

        /// <summary>
        /// Creates a dictionary associating each vessel icon with its type for fast look-up (if it hasn't been created yet).
        /// </summary>
        public static void LoadTextures()
        {
            if (vesselSprites != null) return;

            vesselSprites = new Dictionary<VesselType, Sprite>
            {
                // Load the textures and convert them to sprites
                [VesselType.Base] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Textures/base", false)),
                [VesselType.Lander] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Textures/lander", false)),
                [VesselType.Plane] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Textures/plane", false)),
                [VesselType.Probe] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Textures/probe", false)),
                [VesselType.Relay] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Textures/relay", false)),
                [VesselType.Rover] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Textures/rover", false)),
                [VesselType.Ship] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Textures/ship", false)),
                [VesselType.Unknown] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Textures/empty", false))
            };
        }

        /// <summary>
        /// Converts a texture to a sprite.
        /// </summary>
        /// <param name="tex"> The texture from which the sprite is created. </param>
        /// <returns> Empty white sprite if tex is null, otherwise new sprite.</returns>
        private static Sprite ToSprite(Texture2D tex)
        {
            if (tex is null)
                return Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
    }
}
