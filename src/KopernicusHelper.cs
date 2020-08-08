using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PersistentThrust
{
    class StarLight
    {
        public CelestialBody star;
        public double relativeLuminosity;
    }

    static class KopernicusHelper
    {
        static List<StarLight> _stars;
        static Dictionary<CelestialBody, StarLight> _starsByBody;

        static private double _astronomicalUnit;
        static public double AstronomicalUnit
        {
            get
            {
                if (_astronomicalUnit == 0)
                    _astronomicalUnit = FlightGlobals.GetHomeBody().orbit.semiMajorAxis;
                return _astronomicalUnit;
            }
        }

        /// <summary>
        /// Retrieves list of Starlight data
        /// </summary>
        static public List<StarLight> Stars
        {
            get { return _stars ?? (_stars = ExtractStarData(moduleName)); }
        }

        static public Dictionary<CelestialBody, StarLight> StarsByBody
        {
            get { return _starsByBody ?? (_starsByBody = Stars.ToDictionary(m => m.star)); }
        }

        public static bool IsStar(CelestialBody body)
        {
            return GetLuminosity(body) > 0;
        }

        public static double GetLuminosity(CelestialBody body)
        {
            if (StarsByBody.TryGetValue(body, out StarLight starlight))
                return starlight.relativeLuminosity;
            else
                return 0;
        }


        const string moduleName = "PersistentThrust";
        public const double kerbinAU = 13599840256;
        const double kerbalLuminocity = 3.1609409786213e+24;


        /// <summary>
        /// // Scan the Kopernicus config nodes and extract Kopernicus star data
        /// </summary>
        /// <param name="modulename"></param>
        /// <returns></returns>
        public static List<StarLight> ExtractStarData(string modulename)
        {
            var debugPrefix = "[" + modulename + "] - ";

            List<StarLight> stars = new List<StarLight>();

            var celestialBodiesByName = FlightGlobals.Bodies.ToDictionary(m => m.name);

            ConfigNode[] kopernicusNodes = GameDatabase.Instance.GetConfigNodes("Kopernicus");

            if (kopernicusNodes.Length > 0)
                Debug.Log(debugPrefix + "Loading Kopernicus Configuration Data");
            else
                Debug.LogWarning(debugPrefix + "Failed to find Kopernicus Configuration Data");

            for (int i = 0; i < kopernicusNodes.Length; i++)
            {
                ConfigNode[] bodies = kopernicusNodes[i].GetNodes("Body");

                Debug.Log(debugPrefix + "Found " + bodies.Length + " celestial bodies");

                for (int j = 0; j < bodies.Length; j++)
                {
                    ConfigNode currentBody = bodies[j];

                    string bodyName = currentBody.GetValue("name");

                    celestialBodiesByName.TryGetValue(bodyName, out CelestialBody celestialBody);
                    if (celestialBody == null)
                    {
                        Debug.LogWarning(debugPrefix + "Failed to find celestialBody " + bodyName);
                        continue;
                    }

                    double solarLuminosity = 0;
                    bool usesSunTemplate = false;

                    ConfigNode sunNode = currentBody.GetNode("Template");
                    if (sunNode != null)
                    {
                        string templateName = sunNode.GetValue("name");
                        usesSunTemplate = templateName == "Sun";
                        if (usesSunTemplate)
                            Debug.Log(debugPrefix + "Will use default Sun template for " + bodyName);
                    }

                    if (!usesSunTemplate)
                        continue;

                    ConfigNode scaledVersionsNode = currentBody.GetNode("ScaledVersion");
                    if (scaledVersionsNode != null)
                    {
                        ConfigNode lightsNode = scaledVersionsNode.GetNode("Light");
                        if (lightsNode != null)
                        {
                            string luminosityText = lightsNode.GetValue("luminosity");
                            if (string.IsNullOrEmpty(luminosityText))
                                Debug.LogWarning(debugPrefix + "luminosity is missing in Light ConfigNode for " + bodyName);
                            else
                            {
                                if (double.TryParse(luminosityText, out double luminosity))
                                {
                                    solarLuminosity = (4 * Math.PI * kerbinAU * kerbinAU * luminosity) / kerbalLuminocity;
                                    Debug.Log(debugPrefix + "calculated solarLuminosity " + solarLuminosity + " based on luminosity " + luminosity + " for " + bodyName);
                                }
                                else
                                    Debug.LogError(debugPrefix + "Error converting " + luminosityText + " into luminosity for " + bodyName);
                            }
                        }
                        else
                            Debug.LogWarning(debugPrefix + "failed to find Light node for " + bodyName);

                    }
                    else
                        Debug.LogWarning(debugPrefix + "failed to find ScaledVersion node for " + bodyName);


                    ConfigNode propertiesNode = currentBody.GetNode("Properties");
                    if (propertiesNode != null)
                    {
                        string starLuminosityText = propertiesNode.GetValue("starLuminosity");

                        if (string.IsNullOrEmpty(starLuminosityText))
                        {
                            if (usesSunTemplate)
                                Debug.LogWarning(debugPrefix + "starLuminosity is missing in ConfigNode for " + bodyName);
                        }
                        else
                        {
                            double.TryParse(starLuminosityText, out solarLuminosity);

                            if (solarLuminosity > 0)
                            {
                                Debug.Log(debugPrefix + "Added Star " + celestialBody.name + " with defined luminosity " + solarLuminosity);
                                stars.Add(new StarLight() { star = celestialBody, relativeLuminosity = solarLuminosity });
                                continue;
                            }
                        }
                    }

                    if (solarLuminosity > 0)
                    {
                        Debug.Log(debugPrefix + "Added Star " + celestialBody.name + " with calculated luminosity of " + solarLuminosity);
                        stars.Add(new StarLight() { star = celestialBody, relativeLuminosity = solarLuminosity });
                    }
                    else
                    {
                        Debug.Log(debugPrefix + "Added Star " + celestialBody.name + " with default luminosity of 1");
                        stars.Add(new StarLight() { star = celestialBody, relativeLuminosity = 1 });
                    }

                }
            }

            // add local sun if Kopernicus configuration was not found or did not contain any star
            var homePlanetSun = Planetarium.fetch.Sun;
            if (stars.All(m => m.star.name != homePlanetSun.name))
            {
                Debug.LogWarning(debugPrefix + "HomePlanet localStar was not found, adding HomePlanet localStar as default sun");
                stars.Add(new StarLight() { star = Planetarium.fetch.Sun, relativeLuminosity = 1 });
            }

            return stars;
        }

        public static double GetSolarDistanceMultiplier(Vector3d vesselPosition, CelestialBody star, double astronomicalUnit)
        {
            var distanceToSurfaceStar = (vesselPosition - star.position).magnitude - star.Radius;
            var distanceInAu = distanceToSurfaceStar / astronomicalUnit;
            return 1d / (distanceInAu * distanceInAu);
        }


        public static bool GetLineOfSight(ModuleDeployablePart solarPanel, StarLight star, Vector3d trackDir)
        {
            var trackingBody = solarPanel.trackingBody;
            solarPanel.trackingTransformLocal = star.star.transform;
            solarPanel.trackingTransformScaled = star.star.scaledBody.transform;
            string blockingObject = "";
            var trackingLos = solarPanel.CalculateTrackingLOS(trackDir, ref blockingObject);
            solarPanel.trackingTransformLocal = trackingBody.transform;
            solarPanel.trackingTransformScaled = trackingBody.scaledBody.transform;
            return trackingLos;
        }

        public static bool LineOfSightToSun(Vector3d vesselPosition, CelestialBody star)
        {
            return LineOfSightToTransmitter(vesselPosition, star.position, star.name);
        }

        public static bool LineOfSightToTransmitter(Vector3d vesselPosition, Vector3d transmitterPosition, string ignoreBody = "")
        {
            Vector3d bminusa = transmitterPosition - vesselPosition;

            foreach (CelestialBody referenceBody in FlightGlobals.Bodies)
            {
                // the star should not block line of sight to the sun
                if (referenceBody.name == ignoreBody)
                    continue;

                Vector3d refminusa = referenceBody.position - vesselPosition;

                if (Vector3d.Dot(refminusa, bminusa) <= 0)
                    continue;

                var normalizedBminusa = bminusa.normalized;

                var cosReferenceSunNormB = Vector3d.Dot(refminusa, normalizedBminusa);

                if (cosReferenceSunNormB >= bminusa.magnitude)
                    continue;

                Vector3d tang = refminusa - cosReferenceSunNormB * normalizedBminusa;
                if (tang.magnitude < referenceBody.Radius)
                    return false;
            }
            return true;
        }


    }
}
