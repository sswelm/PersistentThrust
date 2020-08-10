using PersistentThrust.UI.Interface;

namespace PersistentThrust.UI.SituationModules
{
    public abstract class SituationModule : IInfoModule
    {
        public string ModuleTitle { get; }
        public string FieldText { get; private set; }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get { return _isVisible; }
            set
            {
                _isVisible = value;

                UpdateVisible();
            }
        }

        public SituationModule(string t)
        {
            ModuleTitle = t;
        }

        protected abstract void UpdateVisible();

        public bool IsActive { get; set; } = true;

        public void Update()
        {
            FieldText = FieldUpdate();
        }

        protected abstract string FieldUpdate();
    }
}
