using System;
using System.Collections.Generic;

namespace PersistentThrust.BackgroundProcessing
{
    public class PersistentEngineData
    {
        public int PartIndex { get; set; }
        public int ModuleIndex { get; set; }
        public uint PersistentPartId { get; set; }
        public Part ProtoPart { get; set; }
        public ProtoPartSnapshot ProtoPartSnapshot { get; set; }
        public PersistentEngine PersistentEngine { get; set; }
        public ProtoPartModuleSnapshot ProtoPartModuleSnapshot { get; set; }
        public Vector3d DeltaVVector { get; set; }
        public double DeltaV { get; set; }
        public float MaxThrust { get; set; }
    }

    public class SolarPanelData
    {
        public int PartIndex { get; set; }
        public int ModuleIndex { get; set; }
        public uint PersistentPartId { get; set; }
        public Part ProtoPart { get; set; }
        public ProtoPartSnapshot ProtoPartSnapshot { get; set; }
        public double ChargeRateMultiplier { get; set; } = 1;

        public List<ModuleDeployableSolarPanel> ModuleDeployableSolarPanels { get; set; }
        public List<ProtoPartModuleSnapshot> ProtoPartModuleSnapshots { get; set; }
    }

    public class ModuleGeneratorData
    {
        public int PartIndex { get; set; }
        public uint PersistentPartId { get; set; }
        public Part ProtoPart { get; set; }
        public ProtoPartSnapshot ProtoPartSnapshot { get; set; }
        public double OutputMultiplier { get; set; } = 1;
        public double InputMultiplier { get; set; } = 1;

        public List<ModuleGenerator> ModuleGenerators { get; set; }
        public List<int> ProtoPartModuleSnapshotIndexes { get; set; }
    }

    public class ModuleResourceConverterData
    {
        public int PartIndex { get; set; }
        public uint PersistentPartId { get; set; }
        public Part ProtoPart { get; set; }
        public ProtoPartSnapshot ProtoPartSnapshot { get; set; }
        public double InputMultiplier { get; set; } = 1;
        public double OutputMultiplier { get; set; } = 1;
        public double ReqMultiplier { get; set; } = 1;

        public List<ModuleResourceConverter> ModuleResourceConverters { get; set; }
        public List<int> ProtoPartModuleSnapshotIndexes { get; set; }
    }

    public class ResourceChange
    {
        public string Name { get; set; }
        public double Change { get; set; }
    }

    public class VesselData
    {
        public string persistentVesselTargetBodyName;
        public string persistentVesselTargetId = Guid.Empty.ToString();
        public double persistentManeuverUT;
        public float maneuverToleranceInDegree;
        public string persistentManeuverNextPatch;
        public string persistentManeuverPatch;

        public Vessel Vessel { get; set; }
        public double PersistentThrust { get; set; }
        public double TotalVesselMassInKg { get; set; }
        public double TotalVesselMassInTon { get; set; }
        public Vector3d Position { get; set; }
        public double Time { get; set; } = 0;
        public double DeltaTime { get; set; }
        public Orbit Orbit  { get; set; }
        public Vector3d OrbitalVelocityAtUt { get; set; }
        public VesselAutopilot.AutopilotMode PersistentAutopilotMode { get; set; }
        public Vector3d ThrustVector { get; set; }
        public Vector3d AccelerationVector { get; private set; }
        //public Vector3d DeltaVVector { get; set; }
        public bool? HasAnyActivePersistentEngine { get; set; }

        public Dictionary<uint, double> PartSizeMultipliers { get; set; } = new Dictionary<uint, double>();
        public Dictionary<uint, PersistentEngineData> Engines { get; set; } = new Dictionary<uint, PersistentEngineData>();
        public Dictionary<uint, ModuleGeneratorData> Generators { get; set; } = new Dictionary<uint, ModuleGeneratorData>();
        public Dictionary<uint, ModuleResourceConverterData> ResourceConverters { get; set; } = new Dictionary<uint, ModuleResourceConverterData>();

        public Dictionary<uint, SolarPanelData> SolarPanels { get; set; } = new Dictionary<uint, SolarPanelData>();
        public Dictionary<string, ResourceChange> ResourceChanges { get; set; } = new Dictionary<string, ResourceChange>();

        public Dictionary<string, double> AvailableResources { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> MaxAmountResources { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> AvailableStorage{ get; set; } = new Dictionary<string, double>();

        public VesselData(Vessel vessel)
        {
            Vessel = vessel;
        }

        public void ResourceChange(string resourceName, double changeAmount)
        {
            if (changeAmount == 0)
                return;

            ResourceChanges.TryGetValue(resourceName, out ResourceChange resourceChange);

            if (resourceChange == null)
            {
                resourceChange = new ResourceChange { Name = resourceName };
                ResourceChanges.Add(resourceName, resourceChange);
            }

            resourceChange.Change += changeAmount;
        }

        public void UpdateAvailableResource(string resourceName, double changeAmount)
        {
            if (changeAmount == 0)
                return;

            AvailableResources.TryGetValue(resourceName, out double availableAmount);
            AvailableResources[resourceName] = availableAmount + changeAmount;
        }

        public void UpdateUnloadedVesselData()
        {
            // clear reference dictionaries
            foreach (var vesselDataResourceChange in ResourceChanges)
            {
                vesselDataResourceChange.Value.Change = 0;
            }

            // clear value dictionaries
            AvailableResources.Clear();
            MaxAmountResources.Clear();
            AvailableStorage.Clear();

            Position = Vessel.GetWorldPos3D();
            Orbit = Vessel.GetOrbit();
            OrbitalVelocityAtUt = Orbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime()).xzy;

            // calculate thrust vector once per frame per vessel
            ThrustVector = EngineBackgroundProcessing.GetThrustVectorForAutoPilot(vesselData: this, UT: Planetarium.GetUniversalTime());

            if (ThrustVector != Vector3d.zero && DeltaTime != 0)
            {
                // apply Perturb once per frame per vessel
                AccelerationVector = Vector3d.zero;
                foreach (KeyValuePair<uint, PersistentEngineData> engineData in Engines)
                {
                    PersistentEngineData persistentEngineData = engineData.Value;

                    persistentEngineData.DeltaVVector = persistentEngineData.DeltaV * ThrustVector.normalized;
                    AccelerationVector += persistentEngineData.DeltaVVector / DeltaTime;
                }

                Orbit.Perturb(AccelerationVector * TimeWarp.fixedDeltaTime, Planetarium.GetUniversalTime());
            }

            // calculate vessel mass and total resource amounts
            TotalVesselMassInTon = 0;
            foreach (ProtoPartSnapshot protoPartSnapshot in Vessel.protoVessel.protoPartSnapshots)
            {
                TotalVesselMassInTon += protoPartSnapshot.mass;
                foreach (ProtoPartResourceSnapshot protoPartResourceSnapshot in protoPartSnapshot.resources)
                {
                    if (protoPartResourceSnapshot.definition == null)
                        continue;

                    TotalVesselMassInTon += protoPartResourceSnapshot.amount * protoPartResourceSnapshot.definition.density;

                    MaxAmountResources.TryGetValue(protoPartResourceSnapshot.resourceName, out double maxAmount);
                    MaxAmountResources[protoPartResourceSnapshot.resourceName] = maxAmount + protoPartResourceSnapshot.maxAmount;

                    UpdateAvailableResource(protoPartResourceSnapshot.resourceName, Math.Min(protoPartResourceSnapshot.maxAmount, protoPartResourceSnapshot.amount));
                }
            }
            TotalVesselMassInKg = TotalVesselMassInTon * 1000;

            // calculate storage room for resources
            foreach (KeyValuePair<string, double> availableResource in AvailableResources)
            {
                AvailableResources.TryGetValue(availableResource.Key, out double availableAmount);
                MaxAmountResources.TryGetValue(availableResource.Key, out double maxAmount);
                AvailableStorage[availableResource.Key] = maxAmount - availableAmount;
            }
        }
    }
}