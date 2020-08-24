using System;
using System.Linq;

namespace PersistentThrust
{
    /// <summary>
    /// Detects whether RealFuels is installed to update their ignited field and avoid engine shutdown when dropping from timeWarp.
    /// </summary>
    public static class DetectRealFuels
    {
        private static bool? _realFuelsFound = null;

        public static bool Found
        {
            get
            {
                if (!_realFuelsFound.HasValue)
                {
                    _realFuelsFound = AssemblyLoader.loadedAssemblies.Any(a => string.Equals(a.name, "RealFuels", StringComparison.OrdinalIgnoreCase));
                }
                return _realFuelsFound.Value;
            }
        }
    }
}