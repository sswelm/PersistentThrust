using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace PersistentThrust
{
    public static class OrbitExtensions
    {
        /// <summary>
        /// Duplicates an serializedOrbit.
        /// </summary>
        public static Orbit Clone(this Orbit orbit0)
        {
            return new Orbit(orbit0.inclination, orbit0.eccentricity, orbit0.semiMajorAxis, orbit0.LAN, orbit0.argumentOfPeriapsis, orbit0.meanAnomalyAtEpoch, orbit0.epoch, orbit0.referenceBody);
        }

        /// <summary>
        /// Perturbs an serializedOrbit by a deltaV vector.
        /// </summary>
        public static void Perturb(this Orbit orbit, Vector3d deltaVV, double UT)
        {
            if (deltaVV.magnitude == 0)
                return;

            // Transpose deltaVV Y and Z to match serializedOrbit frame
            Vector3d deltaVVector_orbit = deltaVV.xzy;

            // Position vector
            Vector3d position = orbit.getRelativePositionAtUT(UT);

            // Update with current position and new velocity
            orbit.UpdateFromStateVectors(position, orbit.getOrbitalVelocityAtUT(UT) + deltaVVector_orbit, orbit.referenceBody, UT);
            orbit.Init();
            orbit.UpdateFromUT(UT);
        }

        public static Vector3d GetBurnVector(this Orbit currentOrbit, Orbit nextPatch, Orbit patch, double UT)
        {
            if (currentOrbit != null && nextPatch != null)
            {
                if (currentOrbit.referenceBody.flightGlobalsIndex != nextPatch.referenceBody.flightGlobalsIndex)
                {
                    return (nextPatch.getOrbitalVelocityAtUT(UT) - patch.getOrbitalVelocityAtUT(UT)).xzy;
                }
                return (nextPatch.getOrbitalVelocityAtUT(UT) - currentOrbit.getOrbitalVelocityAtUT(UT)).xzy;
            }

            return Vector3d.zero;
        }

        public static double GetVesselOrbitHeadingVersusManeuverVector(this Orbit vesselOrbit, Orbit nextPatch, Orbit patch, double maneuverUT)
        {
            //Debug.Log("[PersistentThrust]: maneuverUT " + maneuverUT);

            //Debug.Log("[PersistentThrust]: vesselOrbit inc=" + vesselOrbit.inclination
            //                                                 + " e=" + vesselOrbit.eccentricity
            //                                                 + " sma=" + vesselOrbit.semiMajorAxis
            //                                                 + " lan=" + vesselOrbit.LAN
            //                                                 + " argPe=" + vesselOrbit.argumentOfPeriapsis
            //                                                 + " mEp=" + vesselOrbit.meanAnomalyAtEpoch
            //                                                 + " t=" + vesselOrbit.epoch);

            //Debug.Log("[PersistentThrust]: patch       inc=" + patch.inclination
            //                                              + " e=" + patch.eccentricity
            //                                              + " sma=" + patch.semiMajorAxis
            //                                              + " lan=" + patch.LAN
            //                                              + " argPe=" + patch.argumentOfPeriapsis
            //                                              + " mEp=" + patch.meanAnomalyAtEpoch
            //                                              + " t=" + patch.epoch);

            //Debug.Log("[PersistentThrust]: nextPatch   inc=" + nextPatch.inclination
            //                                                 + " e=" + nextPatch.eccentricity
            //                                                 + " sma=" + nextPatch.semiMajorAxis
            //                                                 + " lan=" + nextPatch.LAN
            //                                                 + " argPe=" + nextPatch.argumentOfPeriapsis
            //                                                 + " mEp=" + nextPatch.meanAnomalyAtEpoch
            //                                                 + " t=" + nextPatch.epoch);

            var burnVector = vesselOrbit.GetBurnVector(nextPatch, patch, maneuverUT).normalized;
            //Debug.Log("[PersistentThrust]: burnVector x=" + burnVector.x + " y=" + burnVector.y + " z=" + burnVector.z);

            var obtVelocity = vesselOrbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime()).xzy.normalized;
            //Debug.Log("[PersistentThrust]: obtVelocity x=" + obtVelocity.x + " y=" + obtVelocity.y + " z=" + obtVelocity.z);

            var orbitHeadingAtManeuverVsBurn = Vector3d.Dot(vesselOrbit.getOrbitalVelocityAtUT(maneuverUT).xzy.normalized, burnVector);
            //Debug.Log("[PersistentThrust]: orbitHeadingAtManeuverVsBurn " + orbitHeadingAtManeuverVsBurn);

            var forward = orbitHeadingAtManeuverVsBurn > 0;
            if (forward)
                return Math.Min(orbitHeadingAtManeuverVsBurn, Vector3d.Dot(obtVelocity, burnVector));
            else
                return Math.Min(-orbitHeadingAtManeuverVsBurn, Vector3d.Dot(-obtVelocity, burnVector));
        }

        public static Vector3d GetThrustVectorToManeuver(this Orbit orbit, ProtoPartModuleSnapshot module_snapshot)
        {
            double persistentManeuverUT = 0;
            if (!module_snapshot.moduleValues.TryGetValue(nameof(persistentManeuverUT), ref persistentManeuverUT))
                return Vector3d.zero;

            float maneuverToleranceInDegree = 0;
            if (!module_snapshot.moduleValues.TryGetValue(nameof(maneuverToleranceInDegree), ref maneuverToleranceInDegree))
                return Vector3d.zero;

            string persistentManeuverNextPatch = string.Empty;
            module_snapshot.moduleValues.TryGetValue(nameof(persistentManeuverNextPatch), ref persistentManeuverNextPatch);

            if (string.IsNullOrEmpty(persistentManeuverNextPatch))
                return Vector3d.zero;

            string persistentManeuverPatch = string.Empty;
            module_snapshot.moduleValues.TryGetValue(nameof(persistentManeuverPatch), ref persistentManeuverPatch);

            Orbit nextPatch = Deserialize(persistentManeuverNextPatch);
            Orbit patch = Deserialize(persistentManeuverPatch);

            //if (nextPatch == null)
            //    Debug.Log("[PersistentThrust]: nextPatch is null");
            //if (patch == null)
            //    Debug.Log("[PersistentThrust]: patch is null");

            var vesselOrbitHeadingVersusManeuverVector = orbit.GetVesselOrbitHeadingVersusManeuverVector(nextPatch, patch, persistentManeuverUT);
            var vesselHeadingVersusManeuverInDegree = Math.Acos(Math.Max(-1, Math.Min(1, vesselOrbitHeadingVersusManeuverVector))) * Orbit.Rad2Deg;

            Debug.Log("[PersistentThrust]: vesselHeadingVersusManeuverInDegree " + vesselHeadingVersusManeuverInDegree);

            //return -(nextPatch.getOrbitalVelocityAtUT(maneuverUT).xzy - orbitalVelocityAtUt);
            if (vesselHeadingVersusManeuverInDegree < maneuverToleranceInDegree)
                return orbit.GetBurnVector(nextPatch, patch, persistentManeuverUT);
            else
                return Vector3d.zero;
        }

        public static string Serialize(this Orbit orbit)
        {
            var nextPatchDict = new Dictionary<string, double>
            {
                {nameof(orbit.inclination), orbit.inclination},
                {nameof(orbit.eccentricity), orbit.eccentricity},
                {nameof(orbit.semiMajorAxis), orbit.semiMajorAxis},
                {nameof(orbit.LAN), orbit.LAN},
                {nameof(orbit.argumentOfPeriapsis), orbit.argumentOfPeriapsis},
                {nameof(orbit.meanAnomalyAtEpoch), orbit.meanAnomalyAtEpoch},
                {nameof(orbit.epoch), orbit.epoch},
                {nameof(orbit.referenceBody.flightGlobalsIndex), orbit.referenceBody.flightGlobalsIndex},
            };

            return string.Join(";", nextPatchDict.Select(x => x.Key + "=" + x.Value).ToArray());
        }

        private static Orbit Deserialize(string serializedOrbit)
        {
            if (string.IsNullOrEmpty(serializedOrbit))
                return null;

            var orbitDict = CreateOrbitDict(serializedOrbit);

            Orbit nextPatch;
            orbitDict.TryGetValue(nameof(nextPatch.inclination), out double inclination);
            orbitDict.TryGetValue(nameof(nextPatch.eccentricity), out double eccentricity);
            orbitDict.TryGetValue(nameof(nextPatch.semiMajorAxis), out double semiMajorAxis);
            orbitDict.TryGetValue(nameof(nextPatch.LAN), out double LAN);
            orbitDict.TryGetValue(nameof(nextPatch.argumentOfPeriapsis), out double argumentOfPeriapsis);
            orbitDict.TryGetValue(nameof(nextPatch.meanAnomalyAtEpoch), out double meanAnomalyAtEpoch);
            orbitDict.TryGetValue(nameof(nextPatch.epoch), out double epoch);
            orbitDict.TryGetValue(nameof(nextPatch.referenceBody), out double referenceBody);

            CelestialBody celestialBody = FlightGlobals.Bodies.SingleOrDefault(m => m.flightGlobalsIndex == (int)referenceBody);

            if (celestialBody == null)
                return null;

            return new Orbit(inclination, eccentricity, semiMajorAxis, LAN, argumentOfPeriapsis, meanAnomalyAtEpoch, epoch, celestialBody);
        }

        private static Dictionary<string, double> CreateOrbitDict(string persistentManeuverNextPatch)
        {
            Dictionary<string, double> nextPatchDict = persistentManeuverNextPatch
                .Split(';').Select(s => s.Trim().Split('='))
                .ToDictionary(a => a[0], a => double.Parse(a[1]));
            return nextPatchDict;
        }
    }
}
