using System.Collections.Generic;
using UnityEngine;

namespace PersistentThrust
{
    // Container for propellant info used by PersistentEngine
    public class PersistentPropellant
    {
        #region Fields

        public Propellant propellant;
        public PartResourceDefinition definition;
        public double density;
        public double ratio;
        public double normalizedRatio;

        public double normalizedDemand;
        public double totalEnginesDemand;

        public double maxAmount;
        public double amount;
        public double missionTime;
        public double demandIn;
        public double demandOut;

        #endregion


        /// <summary>
        /// Constructor.
        /// </summary>
        private PersistentPropellant(Propellant p)
        {
            propellant = p;
            definition = PartResourceLibrary.Instance.GetDefinition(propellant.name);
            density = definition.density;
            ratio = propellant.ratio;
        }


        /// <summary>
        /// Calculates demand of this propellant given the total demand of the moduleEngine.
        /// </summary>
        public double CalculateDemand(double demand)
        {
            return demand * normalizedRatio;
        }



        #region Static Methods

        /// <summary>
        /// Returns wheter the infinite resource cheat option is checked for this propellant.
        /// </summary>
        public static bool IsInfinite(Propellant propellant)
        {
            if (propellant.resourceDef.density == 0)
                return CheatOptions.InfiniteElectricity;
            else
                return CheatOptions.InfinitePropellant;
        }



        /// <summary>
        /// Loads a Propellant node from input propellant name and ratio.
        /// </summary>
        public static ConfigNode LoadPropellant(string akName, float akRatio)
        {
            Debug.Log("[PersistentThrust]: LoadPropellant: " + akName + " " + akRatio);

            var propellantNode = new ConfigNode().AddNode("PROPELLANT");
            propellantNode.AddValue("name", akName);
            propellantNode.AddValue("ratio", akRatio);
            propellantNode.AddValue("DrawGauge", true);

            return propellantNode;
        }



        /// <summary>
        ///  Generates list of PersistentPropellant from propellant list.
        /// </summary>
        public static List<PersistentPropellant> MakeList(List<Propellant> plist)
        {
            // Sum of ratios of propellants with mass
            var ratioMassSum = 0.0;
            // Create list of PersistentPropellant and calculate ratioSum & ratioMassSum
            var pplist = new List<PersistentPropellant>();
            foreach (var p in plist)
            {
                var pp = new PersistentPropellant(p);
                pplist.Add(pp);
                if (pp.density > 0)
                    ratioMassSum += pp.ratio;
            }

            // Normalize ratios to ratioMassSum
            if (ratioMassSum > 0)
            {
                foreach (var pp in pplist)
                {
                    pp.normalizedRatio = pp.ratio / ratioMassSum;
                }
            }

            return pplist;
        }

        #endregion

    }

    // Extensions to list of PersistentPropellant
    public static class PPListExtensions
    {

        /// <summary>
        /// Calculates average density from a list of PersistentPropellant.
        /// </summary>
        public static double AverageDensity(this List<PersistentPropellant> pplist)
        {
            double avgDensity = 0;
            foreach (var pp in pplist)
            {
                if (pp.density > 0)
                    avgDensity += pp.normalizedRatio * pp.density;
            }
            return avgDensity;
        }



        /// <summary>
        /// Generates string with list of propellant names for use in moduleEngine GUI.
        /// UNUSED?
        /// </summary>
        public static string ResourceNames(this List<PersistentPropellant> pplist)
        {
            var title = "";
            foreach (var pp in pplist)
            {
                // If multiple resources, put | between them
                if (title != string.Empty)
                {
                    title += "|";
                }
                // Add name of resource
                title += pp.propellant.name;
            }
            title += " use";
            return title;
        }



        /// <summary>
        /// Generates string with list of current propellant amounts for use in moduleEngine GUI.
        /// UNUSED?
        /// </summary>
        /// <param name="dT"> current step size </param>
        public static string ResourceAmounts(this List<PersistentPropellant> pplist, double dT)
        {
            if (dT == 0)
                return "";

            var amounts = "";
            foreach (var pp in pplist)
            {
                // If multiple resources, put | between them
                if (amounts != string.Empty)
                    amounts += "|";

                // Add current amount * dT
                amounts += (pp.propellant.currentAmount / dT).ToString("E3");
            }
            amounts += " U/s";
            return amounts;
        }

    }
}
