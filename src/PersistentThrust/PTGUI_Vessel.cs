using PersistentThrust.UI.Interface;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PersistentThrust
{
    public class PTGUI_Vessel : MonoBehaviour, IVesselElement
    {
        public GameObject GameObj { get; set; }
        public Guid VesselId { get; set; }
        public string VesselName { get; set; }
        public bool HasPersistentThrustActive { get; set; }
        public bool PersistentThrustWasToggled { get; set; }
        public bool HasInfoWindowActive { get; set; }
        public Sprite VesselIcon { get; set; }

        private Vessel vessel;
        private VesselType currentVesselType;

        [KSPField(isPersistant = true)]
        private List<bool> hasPersistentThrustEnabled;
        [KSPField(isPersistant = false)]
        private bool didCheckPTEnabled = false;

        /// <summary>
        /// Adds a PTGUI_Vessel Monobehaviour component to a GameObject (or creates a new one).
        /// Initializes the script with values from the vessel.
        /// <para /> FIXME: Definitely not optimal. Creates 2 GameObjects for each vessel (one VesselElement, one PTGUI_Vessel).
        /// </summary>
        /// <param name="v"> The vessel whose parameters are used to initialize the script. </param>
        /// <param name="gameObject"> The GameObject to which the component will be added. </param>
        /// <returns> Initialized PTGUI_Vessel component </returns>
        public static PTGUI_Vessel Create(Vessel v, GameObject gameObject = null)
        {
            if (gameObject == null)
                gameObject = new GameObject();

            PTGUI_Vessel x = gameObject.AddComponent<PTGUI_Vessel>();

            x.GameObj = gameObject;
            x.vessel = v;
            x.VesselId = v.id;
            x.VesselName = v.vesselName;
            x.VesselIcon = GetVesselIcon(v.vesselType);
            x.HasInfoWindowActive = false;
            x.currentVesselType = v.vesselType;

            return x;
        }

        private void Start()
        {

        }

        /// <summary>
        /// Called by Unity every frame that the MonoBehaviour is active. Updates the vessel with data from the GUI and vice-versa.
        /// </summary>
        private void LateUpdate()
        {
            if (PersistentThrustWasToggled)
            {
                SetVesselWidePersistentThurst(HasPersistentThrustActive);

                PersistentThrustWasToggled = false;
            }

            UpdateVesselInfo();
        }

        private void OnDestroy()
        {
            SetVesselWidePersistentThurst(HasPersistentThrustActive);
        }

        /// <summary>
        /// Tries to find an icon for the input vessel type. Logs an exception if the type is unknonw.
        /// </summary>
        /// <param name="type"></param>
        /// <returns> The vessel Sprite </returns>
        public static Sprite GetVesselIcon(VesselType type)
        {
            try
            {
                return PTGUI_Loader.vesselSprites[type];
            }
            catch (Exception ex)
            {
                Debug.LogError("[PersistentThrust]: Failed to find vessel icon");
                Debug.LogException(ex);

                return PTGUI_Loader.vesselSprites[VesselType.Unknown];
            }
        }

        /// <summary>
        ///
        /// </summary>
        private void UpdateVesselInfo()
        {
            if (VesselName != vessel.vesselName)
                VesselName = vessel.vesselName;

            if (currentVesselType != vessel.vesselType)
            {
                currentVesselType = vessel.vesselType;
                VesselIcon = GetVesselIcon(vessel.vesselType);
            }
        }

        public void UpdateVesselThrustInfo(bool? isOn = null)
        {
            if (isOn != null)
                HasPersistentThrustActive = (bool)isOn;
            else
                isOn = PersistentThrustEnabled(vessel).Contains(true);

            HasPersistentThrustActive = (bool)isOn;
        }

        public List<bool> PersistentThrustEnabled(Vessel vessel)
        {
            if (didCheckPTEnabled)
                return hasPersistentThrustEnabled;

            if(hasPersistentThrustEnabled is null)
                hasPersistentThrustEnabled = new List<bool>();

            if (vessel.loaded)
            {
                List<PersistentEngine> PEmoduleList = vessel.FindPartModulesImplementing<PersistentEngine>();
                foreach (var PE in PEmoduleList)
                {
                    hasPersistentThrustEnabled.Add(PE.HasPersistentThrust);
                }
            }
            else
            {
                foreach (var protoPart in vessel.protoVessel.protoPartSnapshots)
                {
                    ProtoPartModuleSnapshot moduleSnapshot = protoPart.FindModule(nameof(PersistentEngine));

                    if (moduleSnapshot is null)
                        continue;

                    bool _enabled = true;
                    moduleSnapshot.moduleValues.TryGetValue(nameof(PersistentEngine.HasPersistentThrust), ref _enabled);
                    hasPersistentThrustEnabled.Add(_enabled);
                }
            }

            didCheckPTEnabled = true;

            return hasPersistentThrustEnabled;
        }

        public void ResetPTEnabledCheck()
        {
            didCheckPTEnabled = false;
        }

        public void SetVesselWidePersistentThurst(bool isOn)
        {

            if (vessel.loaded)
            {
                var PEmoduleList = vessel.FindPartModulesImplementing<PersistentEngine>();

                foreach (var module in PEmoduleList)
                {
                    module.HasPersistentThrust = isOn;
                }
            }
            else
            {
                foreach (var protoPart in vessel.protoVessel.protoPartSnapshots)
                {
                    ProtoPartModuleSnapshot moduleSnapshot = protoPart.FindModule(nameof(PersistentEngine));

                    if (moduleSnapshot is null)
                        continue;

                    moduleSnapshot.moduleValues.values.SetValue(nameof(PersistentEngine.HasPersistentThrust), isOn.ToString());
                }
            }

            ResetPTEnabledCheck();
        }
    }
}
