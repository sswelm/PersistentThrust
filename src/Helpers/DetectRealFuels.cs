using System.Reflection;

namespace PersistentThrust
{
    /// <summary>
    /// Detects whether RealFuels is installed to update their ignited field and avoid engine shutdown when dropping from timeWarp.
    /// </summary>
    public class DetectRealFuels
    {
        private static bool _didScan;
        private static bool _realFuelsFound;

        public static bool Found()
        {
            if (_didScan)
                return _realFuelsFound;

            foreach (var a in AssemblyLoader.loadedAssemblies)
            {
                // RF doesn't contain more than one assembly, but we use the same method that we use for Kerbalism just to be sure.

                AssemblyName nameObject = new AssemblyName(a.assembly.FullName);
                string realName = nameObject.Name;

                if (realName.Equals("RealFuels"))
                {
                    _realFuelsFound = true;
                    break;
                }
            }

            _didScan = true;
            return _realFuelsFound;
        }
    }
}