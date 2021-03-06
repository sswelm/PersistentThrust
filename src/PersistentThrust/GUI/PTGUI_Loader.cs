﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PersistentThrust.UI;
using UnityEditor;

namespace PersistentThrust
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class PTGUI_Loader : MonoBehaviour
    {
        private const string bundleName = "/ptui.ksp";
        private static GameObject[] loadedPrefabs;
        public static Dictionary<VesselType, Sprite> vesselSprites;

        public static GameObject MainWindowPrefab { get; private set; }
        public static GameObject VesselElementPrefab { get; private set; }
        public static GameObject InfoWindowPrefab { get; private set; }
        public static GameObject SituationModulePrefab { get; private set; }

        /// <summary>
        /// Called by Unity at initialization, which happens as soon as the game starts.
        /// Loads the GUI prefabs.
        /// </summary>
        private void Awake()
        {
            if (loadedPrefabs is null)
            {
                string path = KSPUtil.ApplicationRootPath + "GameData/PersistentThrust/Resources";

                AssetBundle prefabs = AssetBundle.LoadFromFile(path + bundleName);

                if (prefabs != null)
                    loadedPrefabs = prefabs.LoadAllAssets<GameObject>();
            }

            if (loadedPrefabs != null)
            {
                if (UISkinManager.defaultSkin != null)
                    ProcessUIPrefabs();
            }
        }

        private void ProcessUIPrefabs()
        {
            for (int i = loadedPrefabs.Length - 1; i >= 0; i--)
            {
                GameObject o = loadedPrefabs[i];

                if (o.name == "PTUIPanel")
                    MainWindowPrefab = o;

                else if (o.name == "VesselElement")
                    VesselElementPrefab = o;

                else if (o.name == "InfoPanel")
                    InfoWindowPrefab = o;

                else if (o.name == "SituationModule")
                    SituationModulePrefab = o;

                if (o != null)
                {
                    ProcessTMP(o);
                    ProcessUIComponents(o);
                }
            }
        }
        private void ProcessUIComponents(GameObject obj)
        {
            Style[] styles = obj.GetComponentsInChildren<Style>(true);

            if (styles == null)
                return;

            for (int i = 0; i < styles.Length; i++)
                ProcessComponents(styles[i]);
        }

        private void ProcessComponents(Style style)
        {
            if (style == null)
                return;

            UISkinDef skin = UISkinManager.defaultSkin;

            if (skin == null)
                return;

            switch (style.ElementType)
            {
                case Style.ElementTypes.Window:
                    style.SetImage(skin.window.normal.background, Image.Type.Sliced);
                    break;
                case Style.ElementTypes.Box:
                    style.SetImage(skin.box.normal.background, Image.Type.Sliced);
                    break;
                case Style.ElementTypes.Button:
                    style.SetButton(skin.button.normal.background, skin.button.highlight.background, skin.button.active.background, skin.button.disabled.background);
                    break;
                case Style.ElementTypes.Toggle:
                    style.SetToggle(skin.button.normal.background, skin.button.highlight.background, skin.button.active.background, skin.button.disabled.background);
                    break;
                case Style.ElementTypes.Slider:
                    style.SetSlider(skin.horizontalSlider.normal.background, skin.horizontalSliderThumb.normal.background, skin.horizontalSliderThumb.highlight.background, skin.horizontalSliderThumb.active.background, skin.horizontalSliderThumb.disabled.background);
                    break;
                case Style.ElementTypes.Scrollbar:
                    style.SetScrollbar(skin.verticalScrollbar.normal.background, skin.verticalScrollbarThumb.normal.background, skin.verticalScrollbarThumb.highlight.background, skin.verticalScrollbar.active.background, skin.verticalScrollbarThumb.disabled.background);
                    break;
                case Style.ElementTypes.Scrollview:
                    style.SetImage(skin.scrollView.normal.background, Image.Type.Sliced);
                    break;
                default:
                    break;
            }
        }

        private void ProcessTMP(GameObject obj)
        {
            TextHandler[] handlers = obj.GetComponentsInChildren<TextHandler>(true);

            if (handlers == null)
                return;

            for (int i = 0; i < handlers.Length; i++)
                TMProFromText(handlers[i]);
        }

        private void TMProFromText(TextHandler handler)
        {
            if (handler == null)
                return;

            //The TextHandler element should be attached only to objects with a Unity Text element
            //Note that the "[RequireComponent(typeof(Text))]" attribute cannot be attached to TextHandler since Unity will not allow the Text element to be removed
            Text text = handler.GetComponent<Text>();

            if (text == null)
                return;

            //Cache all of the relevent information from the Text element
            string t = text.text;
            Color c = text.color;
            int i = text.fontSize;
            bool resize = text.resizeTextForBestFit;
            int maxs = text.resizeTextMaxSize;
            int mins = text.resizeTextMinSize;
            bool r = text.raycastTarget;
            FontStyles sty = TMPProUtil.FontStyle(text.fontStyle);
            TextAlignmentOptions align = TMPProUtil.TextAlignment(text.alignment);
            float spacing = text.lineSpacing;
            GameObject obj = text.gameObject;

            //The existing Text element must by destroyed since Unity will not allow two UI elements to be placed on the same GameObject
            DestroyImmediate(text);

            PTTextMeshProHolder tmp = obj.AddComponent<PTTextMeshProHolder>();

            //Populate the TextMeshPro fields with the cached data from the old Text element
            tmp.text = t;
            tmp.color = c;
            tmp.fontSize = i;
            tmp.enableAutoSizing = resize;
            tmp.fontSizeMax = maxs;
            tmp.fontSizeMin = mins;
            tmp.raycastTarget = r;
            tmp.alignment = align;
            tmp.fontStyle = sty;
            tmp.lineSpacing = spacing;

            tmp.font = UISkinManager.TMPFont;
            tmp.fontSharedMaterial = Resources.Load("Fonts/Materials/Calibri Dropshadow", typeof(Material)) as Material;

            tmp.enableWordWrapping = true;
            tmp.isOverlay = false;
            tmp.richText = true;

            //No idea why this is needed, but TMP needs this field to be set to true for correct initialization of the alignment style
            var prop = tmp.GetType().GetField("m_isAlignmentEnumConverted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            prop.SetValue(tmp, true);
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
                [VesselType.Base] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Resources/Textures/base", false)),
                [VesselType.Lander] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Resources/Textures/lander", false)),
                [VesselType.Plane] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Resources/Textures/plane", false)),
                [VesselType.Probe] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Resources/Textures/probe", false)),
                [VesselType.Relay] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Resources/Textures/relay", false)),
                [VesselType.Rover] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Resources/Textures/rover", false)),
                [VesselType.Ship] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Resources/Textures/ship", false)),
                [VesselType.Unknown] = ToSprite(GameDatabase.Instance.GetTexture("PersistentThrust/Resources/Textures/empty", false))
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
