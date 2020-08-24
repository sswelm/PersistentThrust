using System;
using System.Linq;

namespace PersistentThrust
{
    public static class DetectPrincipia
    {
        private static bool? _principiaFound = null;

        public static bool Found
        {
            get
            {
                if (!_principiaFound.HasValue)
                {
                    _principiaFound = AssemblyLoader.loadedAssemblies.Any(a => string.Equals(a.name, "ksp_plugin_adapter", StringComparison.OrdinalIgnoreCase));
                }
                return _principiaFound.Value;
            }
        }
    }
}
