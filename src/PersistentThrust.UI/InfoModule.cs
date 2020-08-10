using PersistentThrust.UI.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace PersistentThrust.UI
{
    /// <summary>
	/// This class controls the readout module text
	/// </summary>
    class InfoModule : MonoBehaviour
    {
        [SerializeField]
        private Text m_title = null;
        [SerializeField]
        private Text m_field = null;

        private IInfoModule moduleInterface;

        /// <summary>
        /// This method is used to initialize the readout module; sets the readout title field
        /// </summary>
        /// <param name="module">The readout module interface</param>
        public void SetModule(IInfoModule module)
        {
            if (module == null || m_title == null || m_field == null)
                return;

            moduleInterface = module;

            m_title.text = module.ModuleTitle + ":";
        }

        /// <summary>
        /// Public property for accessing the visibility status of this module; visibility is controlled through the settings panel
        /// </summary>
        public bool IsVisible
        {
            get
            {
                if (moduleInterface == null)
                    return false;

                return moduleInterface.IsVisible;
            }
        }

        /// <summary>
        /// Public property for accessing the active status of the module; this status is updated based on the current vessel status and situation
        /// </summary>
        public bool IsActive
        {
            get
            {
                if (moduleInterface == null)
                    return false;

                return moduleInterface.IsActive;
            }
        }

        /// <summary>
        /// Method used to update the upstream readout module controller and to update the readout text field
        /// </summary>
        public void UpdateModule()
        {
            if (moduleInterface == null)
                return;

            if (!moduleInterface.IsVisible)
                return;

            if (!moduleInterface.IsActive)
                return;

            if (m_field == null)
                return;

            moduleInterface.Update();

            m_field.text = moduleInterface.FieldText;
        }
    }
}
