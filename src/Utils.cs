using System;
using System.Linq;
using UnityEngine;

namespace PersistentThrust
{
    public class Utils
    {
        // Format thrust into mN, N, kN
        public static string FormatThrust(double thrust)
        {
            if (thrust < 1e-6)
            {
                return $"{thrust * 1e9:F2} μN";
            }
            if (thrust < 1e-3)
            {
                return $"{thrust * 1e6:F2} mN";
            }
            else if (thrust < 1.0)
            {
                return $"{thrust * 1e3:F2} N";
            }
            else
            {
                return $"{thrust:F2} kN";
            }
        }
    }
}
