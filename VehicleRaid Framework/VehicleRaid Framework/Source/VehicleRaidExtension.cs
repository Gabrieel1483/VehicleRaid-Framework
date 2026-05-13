using System.Collections.Generic;
using Verse;
using RimWorld;

namespace VehicleRaidFramework
{
    public class CargoItemOption
    {
        public ThingDef thingDef;
        public IntRange count = new IntRange(1, 1);
        public bool tradeable = true; 
    }

    public class VehicleRaidOption
    {
        public PawnKindDef kindDef;
        public float weight = 1f;
        public int forceCount = 0;
        public bool spawnVehicle = true;
        public bool pawnFollowVehicle = false;
        public bool isMortar = false;
        public List<CargoItemOption> cargoItems = new List<CargoItemOption>();
    }

    public class VehicleRaidExtension : DefModExtension
    {

        public List<VehicleRaidOption> vehicleOptions = new List<VehicleRaidOption>();

        public float infantryPointsFraction = 0f;

        public bool spawnInfantry = true;

        public int stayTicks = 30000;

        public List<PawnKindDef> forcedPawns = new List<PawnKindDef>();

        public FactionDef factionDef;
        public List<FactionDef> factionDefs = new List<FactionDef>();
        public List<CargoItemOption> cargoItems = new List<CargoItemOption>();

        public string letterLabel = "VRF_LetterLabel_VehicleRaid";
        public string letterText = "VRF_LetterText_VehicleRaid";

        public override IEnumerable<string> ConfigErrors()
        {
            if (vehicleOptions.NullOrEmpty())
            {
                yield return "VehicleRaidExtension requires at least one entry in vehicleOptions.";
            }
        }
    }
}



