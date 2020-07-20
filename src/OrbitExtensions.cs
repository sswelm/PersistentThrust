namespace PersistentThrust
{
    public static class OrbitExtensions
    {
        /// <summary>
        /// Duplicates an orbit.
        /// </summary>
        public static Orbit Clone(this Orbit orbit0)
        {
            return new Orbit(orbit0.inclination, orbit0.eccentricity, orbit0.semiMajorAxis, orbit0.LAN, orbit0.argumentOfPeriapsis, orbit0.meanAnomalyAtEpoch, orbit0.epoch, orbit0.referenceBody);
        }

        /// <summary>
        /// Perturbs an orbit by a deltaV vector.
        /// </summary>
        public static void Perturb(this Orbit orbit, Vector3d deltaVV, double UT)
        {
            if (deltaVV.magnitude == 0)
                return;

            // Transpose deltaVV Y and Z to match orbit frame
            Vector3d deltaVVector_orbit = deltaVV.xzy;

            // Position vector
            Vector3d position = orbit.getRelativePositionAtUT(UT);

            // Update with current position and new velocity
            orbit.UpdateFromStateVectors(position, orbit.getOrbitalVelocityAtUT(UT) + deltaVVector_orbit, orbit.referenceBody, UT);
            orbit.Init();
            orbit.UpdateFromUT(UT);
        }

        public static Vector3d GetBurnVector(this Orbit currentOrbit, Orbit patch, Orbit nextPatch, double UT)
        {
            if (currentOrbit != null && nextPatch != null)
            {
                if (currentOrbit.referenceBody != nextPatch.referenceBody)
                {
                    return (nextPatch.getOrbitalVelocityAtUT(UT) - patch.getOrbitalVelocityAtUT(UT)).xzy;
                }
                return (nextPatch.getOrbitalVelocityAtUT(UT) - currentOrbit.getOrbitalVelocityAtUT(UT)).xzy;
            }

            return Vector3d.zero;
        }
    }
}
