using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using UnityEngine;

namespace PersistentThrust.Helpers
{
    public class TweakScaleData
    {
        public string ModuleName { get; set; }
        public Dictionary<string, double> Exponents { get; set; } = new Dictionary<string, double>();
    }


    public static class TweaksSaleHelper
    {
        private static IDictionary<string, TweakScaleData> _tweakScaleExponents;

        public static TweakScaleData GetTweakScaleExponents(string moduleName)
        {
            GetTweakScaleExponents().TryGetValue(moduleName, out TweakScaleData data);

            return data;
        }

        public static double? GetTweakScaleExponent(string moduleName, string exponentName)
        {
            GetTweakScaleExponents().TryGetValue(moduleName, out TweakScaleData data);

            if (data == null)
                return null;

            data.Exponents.TryGetValue(exponentName, out double exponentValue);

            return exponentValue;
        }

        public static IDictionary<string, TweakScaleData> GetTweakScaleExponents()
        {
            if (_tweakScaleExponents != null)
                return _tweakScaleExponents;

            ConfigNode[] tweakScaleNodes = GameDatabase.Instance.GetConfigNodes("TWEAKSCALEEXPONENTS");

            _tweakScaleExponents = new Dictionary<string, TweakScaleData>();

            if (tweakScaleNodes.Length > 0)
                Debug.Log("[PersistentThrust]: Loading TweakScaleExponents");
            else
                Debug.LogWarning("[PersistentThrust]: Failed to find TweakScaleExponents");

            foreach (ConfigNode tweakScaleNode in tweakScaleNodes)
            {
                var tweakScaleData = new TweakScaleData();

                foreach (ConfigNode.Value tweakScaleValue in tweakScaleNode.values)
                {
                    if (tweakScaleValue.name == "name")
                        tweakScaleData.ModuleName = tweakScaleValue.value;
                    else
                    {
                        if (double.TryParse(tweakScaleValue.value, out double value))
                            tweakScaleData.Exponents[tweakScaleValue.name] = value;
                    }
                }

                foreach (ConfigNode subNode in tweakScaleNode.nodes)
                {
                    string sub = subNode.name;
                    foreach (ConfigNode.Value tweakScaleValue in subNode.values)
                    {
                        if (double.TryParse(tweakScaleValue.value, out double value))
                            tweakScaleData.Exponents[sub + "." + tweakScaleValue.name] = value;
                    }
                }

                _tweakScaleExponents[tweakScaleData.ModuleName] = tweakScaleData;
            }

            return _tweakScaleExponents;
        }
    }
}
