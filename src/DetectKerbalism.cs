using System;
using System.Reflection;

namespace PersistentThrust
{
    /// <summary>
    /// Detects wheter Kerbalism is installed to enable their resource consumption and background persistent thrusting features.
    /// </summary>
    public class DetectKerbalism
    {
        private static bool didScan = false;
        private static bool kerbalismFound = false;

        public static bool Found()
        {
            if (didScan)
                return kerbalismFound;

            foreach (var a in AssemblyLoader.loadedAssemblies)
            {
                // Kerbalism comes with more than one assembly. There is Kerbalism for debug builds, KerbalismBootLoader,
                // then there are Kerbalism18 or Kerbalism16_17 depending on the KSP version, and there might be ohter
                // assemblies like KerbalismContracts etc.
                // So look at the assembly name object instead of the assembly name (which is the file name and could be renamed).

                AssemblyName nameObject = new AssemblyName(a.assembly.FullName);
                string realName = nameObject.Name; // Will always return "Kerbalism" as defined in the AssemblyName property of the csproj

                if (realName.Equals("Kerbalism"))
                {
                    kerbalismFound = true;
                    break;
                }
            }

            didScan = true;
            return kerbalismFound;
        }
    }
}