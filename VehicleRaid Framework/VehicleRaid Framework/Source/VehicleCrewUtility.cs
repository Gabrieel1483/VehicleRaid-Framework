using System.Collections.Generic;
using Verse;
using RimWorld;
using Vehicles;

namespace VehicleRaidFramework
{
    public static class VehicleCrewUtility
    {
        public static List<VehicleCrewSlot> GetCrewSlotsForRole(
            VehicleRole role,
            List<VehicleCrewSlot> driverCrew,
            List<VehicleCrewSlot> gunnerCrew,
            List<VehicleCrewSlot> passengerCrew)
        {
            bool isDriver    = (role.HandlingTypes & HandlingType.Movement) != 0;
            bool isGunner    = (role.HandlingTypes & HandlingType.Turret)   != 0;
            bool isPassenger = !isDriver && !isGunner;

            if (isDriver    && !driverCrew.NullOrEmpty())    return driverCrew;
            if (isGunner    && !gunnerCrew.NullOrEmpty())    return gunnerCrew;
            if (isPassenger && !passengerCrew.NullOrEmpty()) return passengerCrew;

            return null;
        }

        public static void FillRole(
            VehiclePawn vehicle,
            VehicleRole role,
            List<VehicleCrewSlot> customSlots,
            Faction faction,
            Map map,
            List<Pawn> allPawns,
            bool traderOccupied = false)
        {
            int maxSlots = role.Slots;
            int filled   = 0;

            if (!customSlots.NullOrEmpty())
            {
                foreach (VehicleCrewSlot slot in customSlots)
                {
                    if (slot.pawnDef == null || slot.count <= 0) continue;

                    int toSpawn = System.Math.Min(slot.count, maxSlots - filled);
                    for (int i = 0; i < toSpawn; i++)
                    {
                        Pawn crew = PawnGenerator.GeneratePawn(
                            new PawnGenerationRequest(slot.pawnDef, faction,
                                PawnGenerationContext.NonPlayer, map.Tile,
                                forceGenerateNewPawn: true, mustBeCapableOfViolence: true));

                        if (crew != null && vehicle.TryAddPawn(crew, vehicle.GetHandler(role.key)))
                        {
                            allPawns.Add(crew);
                            filled++;
                        }
                    }

                    if (filled >= maxSlots) break;
                }
            }

            int startSlot = traderOccupied ? 1 : 0;
            for (int i = filled + startSlot; i < maxSlots; i++)
            {
                PawnKindDef kind = faction.RandomPawnKind() ?? PawnKindDefOf.AncientSoldier;
                Pawn crew = PawnGenerator.GeneratePawn(
                    new PawnGenerationRequest(kind, faction,
                        PawnGenerationContext.NonPlayer, map.Tile,
                        forceGenerateNewPawn: true, mustBeCapableOfViolence: true));

                if (crew != null && vehicle.TryAddPawn(crew, vehicle.GetHandler(role.key)))
                    allPawns.Add(crew);
            }
        }
    }
}
