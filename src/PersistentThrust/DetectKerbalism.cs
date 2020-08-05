using System.Reflection;

namespace PersistentThrust
{
    /// <summary>
    /// Detects whether Kerbalism is installed to enable their resource consumption and background persistent thrusting features.
    /// </summary>
    public class DetectKerbalism
    {
        private static bool _didScan;
        private static bool _kerbalismFound;
        public static Assembly KerbalismAssembly = null;

        public static bool Found()
        {
            if (_didScan)
                return _kerbalismFound;

            foreach (var a in AssemblyLoader.loadedAssemblies)
            {
                // Kerbalism comes with more than one assembly. There is Kerbalism for debug builds, KerbalismBootLoader,
                // then there are Kerbalism18 or Kerbalism16_17 depending on the KSP version, and there might be other
                // assemblies like KerbalismContracts etc.
                // So look at the assembly name object instead of the assembly name (which is the file name and could be renamed).

                AssemblyName nameObject = new AssemblyName(a.assembly.FullName);
                string realName = nameObject.Name; // Will always return "Kerbalism" as defined in the AssemblyName property of the csproj

                if (realName.Equals("Kerbalism"))
                {
                    _kerbalismFound = true;
                    break;
                }
            }

            _didScan = true;
            return _kerbalismFound;
        }
    }
}