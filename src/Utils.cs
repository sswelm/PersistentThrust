using System;
using System.Collections.Generic;

namespace PersistentThrust
{
    public class Utils
    {

        /// <summary>
        /// Formats thrust into μN, mN, N, kN.
        /// </summary>
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
            if (thrust < 1.0)
            {
                return $"{thrust * 1e3:F2} N";
            }
            else
            {
                return $"{thrust:F2} kN";
            }
        }


        public static double GetResourceMass(Dictionary<string, double> availableResources)
        {
            double mass = 0.0;

            foreach (var resource in availableResources)
            {
                mass += resource.Value * PartResourceLibrary.Instance.GetDefinition(resource.Key).density;
            }

            return mass;
        }



        /// <summary>
        /// Calculates DeltaV vector.
        /// </summary>
        public static Vector3d CalculateDeltaVVector(double densityPropellantAverage, double vesselMass, double deltaTime, double thrust, float isp, Vector3d thrustVector, out double mass)
        {
            // Mass flow rate
            var massFlowRate = isp > 0 ? thrust / (isp * PhysicsGlobals.GravitationalAcceleration) : 0;
            // Change in mass over time interval dT
            var deltaMass = massFlowRate * deltaTime;
            // Resource demand from propellants with mass
            mass = densityPropellantAverage > 0 ? deltaMass / densityPropellantAverage : 0;
            //// Resource demand from propellants with mass
            var remainingMass = vesselMass - deltaMass;
            // deltaV amount
            var deltaV = isp * PhysicsGlobals.GravitationalAcceleration * Math.Log(remainingMass > 0 ? vesselMass / remainingMass : 1);
            // Return deltaV vector
            return deltaV * thrustVector;
        }


        public static Vector3d CalculateDeltaVVector(double densityPropellantAverage, double vesselMass, double deltaTime, double thrust, float isp, Vector3d thrustVector)
        {
            // Mass flow rate
            var massFlowRate = isp > 0 ? thrust / (isp * PhysicsGlobals.GravitationalAcceleration) : 0;
            // Change in mass over time interval dT
            var deltaMass = massFlowRate * deltaTime;
            //// Resource demand from propellants with mass
            var remainingMass = vesselMass - deltaMass;
            // deltaV amount
            var deltaV = isp * PhysicsGlobals.GravitationalAcceleration * Math.Log(remainingMass > 0 ? vesselMass / remainingMass : 1);
            // Return deltaV vector
            return deltaV * thrustVector;
        }

    }
}
