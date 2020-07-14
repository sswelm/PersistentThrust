using System;
using System.Reflection;

namespace PersistentThrust
{
    /// <summary>
    /// Detects wheter RealFuels is installed to update their ignited field and avoid engine shutdown when dropping from timewarp.
    /// </summary>
    public class DetectRealFuels
    {
        private static bool didScan = false;
        private static bool RealFuelsFound = false;

        public static bool Found()
        {
            if (didScan)
                return RealFuelsFound;

            foreach (var a in AssemblyLoader.loadedAssemblies)
            {
                // RF doesn't contain more than one assembly, but we use the same method that we use for Kerbalism just to be sure.

                AssemblyName nameObject = new AssemblyName(a.assembly.FullName);
                string realname = nameObject.Name;

                if (realname.Equals("RealFuels"))
                {
                    RealFuelsFound = true;
                    break;
                }
            }

            didScan = true;
            return RealFuelsFound;
        }
    }
}