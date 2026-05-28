using System.Collections.Generic;
using Verse;

namespace VehicleRaidFramework
{
    public class VRF_TurretAmmoEntry : IExposable
    {
        public string turretKey;
        public float ammoPercent = 50f;

        public VRF_TurretAmmoEntry() { }

        public VRF_TurretAmmoEntry(string key)
        {
            turretKey = key;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref turretKey, "turretKey");
            Scribe_Values.Look(ref ammoPercent, "ammoPercent", 50f);
        }
    }

    public class VRF_NaturalRaidVehicleEntry : IExposable
    {
        public string vehicleKindDefName;
        public bool enabled = false;
        public float combatPowerOverride = 0f;
        public float minRaidPoints = 0f;
        public float fuelPercent = 100f;
        public List<VRF_TurretAmmoEntry> turretAmmo = new List<VRF_TurretAmmoEntry>();
        public bool helicopterMode = false;
        public float helicopterMoveSpeed = 8.5f;

        public VRF_NaturalRaidVehicleEntry() { }

        public VRF_NaturalRaidVehicleEntry(string defName)
        {
            vehicleKindDefName = defName;
        }

        public VRF_TurretAmmoEntry GetOrCreateTurretAmmo(string turretKey)
        {
            var t = turretAmmo.Find(e => e.turretKey == turretKey);
            if (t == null)
            {
                t = new VRF_TurretAmmoEntry(turretKey);
                turretAmmo.Add(t);
            }
            return t;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref vehicleKindDefName, "vehicleKindDefName");
            Scribe_Values.Look(ref enabled, "enabled", false);
            Scribe_Values.Look(ref combatPowerOverride, "combatPowerOverride", 0f);
            Scribe_Values.Look(ref minRaidPoints, "minRaidPoints", 0f);
            Scribe_Values.Look(ref fuelPercent, "fuelPercent", 100f);
            Scribe_Collections.Look(ref turretAmmo, "turretAmmo", LookMode.Deep);
            if (turretAmmo == null) turretAmmo = new List<VRF_TurretAmmoEntry>();
            Scribe_Values.Look(ref helicopterMode, "helicopterMode", false);
            Scribe_Values.Look(ref helicopterMoveSpeed, "helicopterMoveSpeed", 8.5f);
        }
    }

    public class VRF_NaturalRaidFactionConfig : IExposable
    {
        public string factionDefName;
        public List<VRF_NaturalRaidVehicleEntry> vehicleEntries = new List<VRF_NaturalRaidVehicleEntry>();

        public VRF_NaturalRaidFactionConfig() { }

        public VRF_NaturalRaidFactionConfig(string defName)
        {
            factionDefName = defName;
        }

        public VRF_NaturalRaidVehicleEntry GetOrCreate(string kindDefName)
        {
            var entry = vehicleEntries.Find(e => e.vehicleKindDefName == kindDefName);
            if (entry == null)
            {
                entry = new VRF_NaturalRaidVehicleEntry(kindDefName);
                vehicleEntries.Add(entry);
            }
            return entry;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref factionDefName, "factionDefName");
            Scribe_Collections.Look(ref vehicleEntries, "vehicleEntries", LookMode.Deep);
            if (vehicleEntries == null) vehicleEntries = new List<VRF_NaturalRaidVehicleEntry>();
        }
    }

    public class VRF_ModSettings : ModSettings
    {
        public List<VRF_NaturalRaidFactionConfig> factionConfigs = new List<VRF_NaturalRaidFactionConfig>();
        public bool autoLoadPresets = true;

        public VRF_NaturalRaidFactionConfig GetOrCreateFactionConfig(string factionDefName)
        {
            var config = factionConfigs.Find(c => c.factionDefName == factionDefName);
            if (config == null)
            {
                config = new VRF_NaturalRaidFactionConfig(factionDefName);
                factionConfigs.Add(config);
            }
            return config;
        }

        public VRF_NaturalRaidFactionConfig GetFactionConfig(string factionDefName)
        {
            return factionConfigs.Find(c => c.factionDefName == factionDefName);
        }

        public void ResetAllSettings()
        {
            factionConfigs.Clear();
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref factionConfigs, "factionConfigs", LookMode.Deep);
            if (factionConfigs == null) factionConfigs = new List<VRF_NaturalRaidFactionConfig>();
            Scribe_Values.Look(ref autoLoadPresets, "autoLoadPresets", true);
        }
    }
}
