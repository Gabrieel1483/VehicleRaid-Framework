using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace VehicleRaidFramework
{
    public class VRF_SettlementVehicleDef : Def
    {
        public FactionDef faction;
        public List<SettlementVehicleEntry> vehicles = new List<SettlementVehicleEntry>();
        public float totalCombatPoints = 500f; // Budget for the settlement
    }

    public class SettlementVehicleEntry
    {
        public PawnKindDef vehicleKind;
        public int weight = 10;
        public int forceCount = 0;
        public List<CargoItemOption> cargoItems;
        public bool isMortar = false;
    }
}
