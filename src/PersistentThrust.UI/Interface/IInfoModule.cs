namespace PersistentThrust.UI.Interface
{
    public interface IInfoModule
    {
        string ModuleTitle { get; }

        string FieldText { get; }

        bool IsVisible { get; set; }

        bool IsActive { get; set; }

        void Update();
    }
}
