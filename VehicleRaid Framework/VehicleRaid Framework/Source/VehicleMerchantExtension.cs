using System.Collections.Generic;
using Verse;
using RimWorld;

namespace VehicleRaidFramework
{
    public class VehicleMerchantOption
    {
        public PawnKindDef kindDef;
        public IntRange count = new IntRange(1, 1);
        public List<CargoItemOption> cargoItems = new List<CargoItemOption>();
        public bool tradeCargo = true;
        public VehicleColorConfig colorConfig = null;
    }

    public class TraderVehicleMapper
    {
        public TraderKindDef traderKind;
        
        public VehicleMerchantOption principalVehicle;
        public List<VehicleMerchantOption> cargoVehicles = new List<VehicleMerchantOption>();
        public List<VehicleMerchantOption> escortVehicles = new List<VehicleMerchantOption>();
    }

    public class VehicleMerchantExtension : DefModExtension
    {
        public List<TraderVehicleMapper> traderMappers = new List<TraderVehicleMapper>();

        public TraderVehicleMapper GetMapperFor(TraderKindDef kind)
        {
            if (traderMappers == null) return null;
            return traderMappers.Find(x => x.traderKind == kind);
        }
    }
}


