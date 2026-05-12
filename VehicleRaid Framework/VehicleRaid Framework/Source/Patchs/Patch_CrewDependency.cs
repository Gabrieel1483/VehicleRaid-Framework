using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Vehicles;
using UnityEngine;

namespace VehicleRaidFramework
{
    [HarmonyPatch(typeof(VehiclePawn), "Tick")]
    public static class Patch_RaidVehicle_CrewDependency
    {
        static void Postfix(VehiclePawn __instance)
        {
            if (__instance == null || !__instance.Spawned || __instance.Destroyed || __instance.Map == null || !__instance.IsHashIntervalTick(250)) return;

            if (__instance.Faction == null || __instance.Faction.IsPlayer) return;

            CrewManager.ReassignCrew(__instance);
            CrewManager.CheckAbandonment(__instance);
            CrewManager.CheckRetreat(__instance);

            Lord lord = __instance.GetLord();
            if (lord != null)
            {
                if (lord.LordJob is LordJob_VehicleRaid && __instance.IsHashIntervalTick(1250))
                {
                    FeedCrewFromInventory(__instance);
                    RefuelFromInventory(__instance);
                }

                HandleOverlappingPawns(__instance);

                if (!__instance.AllPawnsAboard.Any() && (__instance.Dead || __instance.Destroyed))
                {
                    lord.Notify_PawnLost(__instance, (PawnLostCondition)3, null);
                }
            }
        }

        private static void FeedCrewFromInventory(VehiclePawn vehicle)
        {
            if (vehicle.inventory == null || vehicle.inventory.innerContainer == null || vehicle.inventory.innerContainer.Count == 0) return;

            Thing food = vehicle.inventory.innerContainer.FirstOrDefault(t => t.def.IsIngestible);
            if (food == null) return;

            foreach (var handler in vehicle.handlers)
            {
                foreach (Pawn occupant in handler.thingOwner)
                {
                    if (occupant != null && occupant.needs?.food != null)
                    {
                        if (occupant.needs.food.CurLevelPercentage < 0.4f)
                        {
                            float nutrition = food.def.ingestible.CachedNutrition;
                            occupant.needs.food.CurLevel += nutrition;
                            food.stackCount--;
                            if (food.stackCount <= 0)
                            {
                                food.Destroy();
                                food = vehicle.inventory.innerContainer.FirstOrDefault(t => t.def.IsIngestible);
                                if (food == null) return;
                            }
                        }
                    }
                }
            }
        }

        private static void RefuelFromInventory(VehiclePawn vehicle)
        {
            CompFueledTravel comp = vehicle.GetComp<CompFueledTravel>();
            if (comp == null || comp.Props.ElectricPowered || comp.FuelPercent > 0.10f) return;

            float needed = comp.FuelCapacity - comp.Fuel;
            if (needed <= 0) return;

            int availableFuel = 0;
            var fuelThings = CompFueledTravel.AllFuelFromInventory(vehicle).ToList();
            foreach (var t in fuelThings) availableFuel += t.stackCount;

            if (availableFuel > 0)
            {
                int toConsume = Mathf.Min(availableFuel, Mathf.CeilToInt(needed));
                comp.ConsumeFuelFromInventory(toConsume);
                
                Patch_VehicleNPCOnOff.UpdateVehiclePower(vehicle);
            }
        }

        private static void HandleOverlappingPawns(VehiclePawn vehicle)
        {
            if (vehicle == null || !vehicle.Spawned || vehicle.Destroyed || vehicle.Map == null || vehicle.Faction == null) return;

            CellRect rect = vehicle.OccupiedRect();
            Map map = vehicle.Map;

            foreach (IntVec3 cell in rect)
            {
                if (!cell.InBounds(map)) continue;

                List<Thing> thingList = cell.GetThingList(map);
                for (int i = thingList.Count - 1; i >= 0; i--)
                {
                    if (thingList[i] is Pawn p && p != vehicle && !(p is VehiclePawn) && !vehicle.handlers.Any(h => h.thingOwner.Contains(p)))
                    {
                        ResolvePawnOverlap(vehicle, p);
                    }
                }
            }
        }

        private static void ResolvePawnOverlap(VehiclePawn vehicle, Pawn pawn)
        {
            ApplyOverlapDamage(vehicle, pawn);

            Map map = vehicle.Map;
            IntVec3 bestPos = IntVec3.Invalid;
            float minDist = float.MaxValue;
            CellRect vehicleRect = vehicle.OccupiedRect();

            for (int radius = 1; radius <= 3; radius++)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, radius, true))
                {
                    if (cell.InBounds(map) && cell.Walkable(map) && !vehicleRect.Contains(cell))
                    {
                        float d = cell.DistanceToSquared(pawn.Position);
                        if (d < minDist)
                        {
                            minDist = d;
                            bestPos = cell;
                        }
                    }
                }
                if (bestPos.IsValid) break;
            }

            if (bestPos.IsValid)
            {
                pawn.Position = bestPos;
                pawn.Notify_Teleported(true, false);
            }
        }

        private static void ApplyOverlapDamage(VehiclePawn vehicle, Pawn pawn)
        {
            if (pawn.Faction == null || vehicle.Faction == null) return;

            float damageMultiplier = 0f;

            if (pawn.Faction == vehicle.Faction || !pawn.Faction.HostileTo(vehicle.Faction))
            {
                damageMultiplier = 0f;
            }
            else if (vehicle.Faction.RelationKindWith(pawn.Faction) == FactionRelationKind.Neutral)
            {
                damageMultiplier = 0.1f;
            }
            else
            {
                damageMultiplier = 1.0f;
            }

            if (damageMultiplier > 0)
            {
                var damages = VehiclePawn.CalculateImpactDamage(pawn, vehicle, 5f);
                float pawnDamage = damages.pawnDamage * damageMultiplier;

                if (pawnDamage > 0.5f)
                {
                    DamageInfo dinfo = new DamageInfo(DamageDefOf.Blunt, pawnDamage, 0f, -1f, vehicle);
                    pawn.TakeDamage(dinfo);
                }
            }
        }
    }
}
