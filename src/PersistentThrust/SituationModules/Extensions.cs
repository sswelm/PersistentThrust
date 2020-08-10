#region License
/*
 * Basic Orbit
 *
 * BasicExtensions - A few extensions for printing values in the correct units
 *
 * Copyright (C) 2016 DMagic
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *
 *
 */
#endregion

using KSP.Localization;
using System;
using System.Text;

namespace PersistentThrust.SituationModules
{
    public static class Extensions
    {
        private static int[] times = new int[5];
        private static string[] units = new string[5] { "s", "m", "h", "d", "y" };
        private static StringBuilder sb = new StringBuilder();
        private static double g0 = PhysicsGlobals.GravitationalAcceleration;

        public static string LocalizeBodyName(this string input)
        {
            return Localizer.Format("<<1>>", input);
        }

        public static string Distance(this double d, int figs = 2)
        {
            if (d < 1e6)
                return string.Format("{0} m", d.ToString("N" + figs.ToString()));
            else if (d < 1e9)
                return string.Format("{0} km", (d / 1e3).ToString("N1"));
            else if (d < 1e12)
                return string.Format("{0} Mm", (d / 1e6).ToString("N1"));
            else
                return string.Format("{0} Gm", (d / 1e9).ToString("N0"));
        }

        public static string CloseDistance(this double d)
        {
            if (Math.Abs(d) < 1e4)
            {
                if (Math.Abs(d) > 10)
                    return string.Format("{0} m", d.ToString("N1"));
                else if (Math.Abs(d) > 0.1)
                    return string.Format("{0} cm", (d * 100).ToString("N2"));
                else
                    return string.Format("{0} mm", (d * 1e3).ToString("N2"));
            }
            else if (d < 1e9)
                return string.Format("{0} km", (d / 1e3).ToString("N1"));
            else
                return string.Format("{0} Mm", (d / 1e6).ToString("N0"));
        }

        public static string Speed(this double d, int figs = 2, int cmFigs = 3)
        {
            if (Math.Abs(d) < 1)
                return string.Format("{0} cm/s", (d * 100).ToString("N" + cmFigs.ToString()));
            else if (Math.Abs(d) < 1e4)
                return string.Format("{0} m/s", d.ToString("N" + figs.ToString()));
            else
                return string.Format("{0} km/s", (d / 1000).ToString("N" + cmFigs.ToString()));
        }

        public static string Acceleration(this double a, int figs = 2, int mmFigs = 3)
        {
            if (a < 0.1)
                return string.Format("{0} mm/s²", (a * 1000).ToString("N" + mmFigs.ToString()));
            else if (a <= g0)
                return string.Format("{0} m/s²", a.ToString("N" + figs.ToString()));
            else
                return string.Format("{0} g", (a / g0).ToString("N" + figs.ToString()));
        }

        public static string Time(this double d, int values)
        {
            if (d == 0)
                return "0s";

            if (double.IsNaN(d) || double.IsInfinity(d))
                return "---";

            if (d >= int.MaxValue)
                return "---";
            else if (d <= int.MinValue)
                return "---";

            d.SetTimes();

            if (sb == null)
                sb = new StringBuilder();

            sb.Length = 0;

            if (d < 0)
                sb.Append("- ");

            for (int i = times.Length - 1; i >= 0; i--)
            {
                int t = times[i];

                if (t == 0)
                {
                    if (i < times.Length - 1 && times[i + 1] == 0)
                        continue;
                    else if (i >= times.Length - 1)
                        continue;
                }

                if (values <= 0)
                    continue;

                string format = "";

                if (i <= 1)
                    format = "D2";
                else if (i == 2 && times[3] != 0)
                    format = "D2";
                else
                    format = "";

                sb.Append(Math.Abs(t).ToString(format));
                sb.Append(units[i]);

                if (values > 1 && i > 0)
                    sb.Append(", ");

                values--;
            }

            return sb.ToString();
        }

        private static void SetTimes(this double d)
        {
            int year = KSPUtil.dateTimeFormatter.Year;
            int day = KSPUtil.dateTimeFormatter.Day;

            times[4] = (int)(d / year);
            d -= times[4] * year;

            times[3] = (int)(d / day);
            d -= times[3] * day;

            times[2] = (int)(d / 3600);
            d -= times[2] * 3600;

            times[1] = (int)(d / 60);
            d -= times[1] * 60;

            times[0] = (int)d;
        }
    }
}
