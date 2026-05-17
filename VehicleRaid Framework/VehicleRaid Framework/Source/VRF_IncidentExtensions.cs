using System.Collections.Generic;
using Verse;
using RimWorld;

namespace VehicleRaidFramework
{
    public class HelicopterIncidentExtension : DefModExtension
    {
        public PawnKindDef vehicleKind;
        public FactionDef factionDef;
        public TraderKindDef traderKind;

        public List<VehicleCrewSlot> driverCrew     = new List<VehicleCrewSlot>();
        public List<VehicleCrewSlot> gunnerCrew     = new List<VehicleCrewSlot>();
        public List<VehicleCrewSlot> passengerCrew  = new List<VehicleCrewSlot>();
    }
}
