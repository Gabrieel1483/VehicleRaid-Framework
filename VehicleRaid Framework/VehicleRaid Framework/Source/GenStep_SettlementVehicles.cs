using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using Vehicles;
using Verse.AI.Group;

namespace VehicleRaidFramework
{
    public class GenStep_SettlementVehicles : GenStep
    {
        public override int SeedPart => 135792468;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (!MapGenerator.TryGetVar<CellRect>("SettlementRect", out var settlementRect))
            {
                return;
            }

            Faction faction = map.ParentFaction;
            if (faction == null) return;

            var vDef = DefDatabase<VRF_SettlementVehicleDef>.AllDefsListForReading
                .FirstOrDefault(d => d.faction == faction.def || (d.faction == null && faction.def == FactionDefOf.Empire && d.defName.Contains("Empire")));

            if (vDef == null || vDef.vehicles.NullOrEmpty()) return;

            float points = vDef.totalCombatPoints;
            List<VehiclePawn> spawnedVehicles = new List<VehiclePawn>();

            foreach (var entry in vDef.vehicles.Where(e => e.forceCount > 0))
            {
                for (int i = 0; i < entry.forceCount; i++)
                {
                    SpawnVehicle(map, settlementRect, entry, faction, spawnedVehicles);
                    points -= (entry.vehicleKind?.combatPower ?? 100f);
                }
            }

            int safetyLimit = 20;
            while (points > 0 && safetyLimit > 0)
            {
                if (vDef.vehicles.TryRandomElementByWeight(e => e.weight, out var entry))
                {
                    float cost = entry.vehicleKind?.combatPower ?? 100f;
                    if (cost <= points || points > -50)
                    {
                        SpawnVehicle(map, settlementRect, entry, faction, spawnedVehicles);
                        points -= cost;
                    }
                    else
                    {
                        safetyLimit--;
                    }
                }
                else break;
            }

            if (spawnedVehicles.Count > 0)
            {
                Lord lord = map.lordManager.lords.FirstOrDefault(l => l.faction == faction && l.LordJob is LordJob_DefendBase);
                if (lord == null)
                {
                    lord = LordMaker.MakeNewLord(faction, new LordJob_DefendBase(faction, settlementRect.CenterCell, 25000), map);
                }

                foreach (var v in spawnedVehicles)
                {
                    if (!lord.ownedPawns.Contains(v))
                        lord.AddPawn(v);
                }
            }
        }

        private void SpawnVehicle(Map map, CellRect settlementRect, SettlementVehicleEntry entry, Faction faction, List<VehiclePawn> spawnedList)
        {
            var vehicleDef = entry.vehicleKind?.race as VehicleDef;
            if (vehicleDef == null) return;

            if (TryFindSpawnCell(map, settlementRect, vehicleDef.size, out IntVec3 cell))
            {
                VehiclePawn vehicle = VehicleSpawner.GenerateVehicle(vehicleDef, faction);
                if (vehicle == null) return;

                GenSpawn.Spawn(vehicle, cell, map);
                
                foreach (var role in vehicleDef.properties.roles)
                {
                    if (role.HandlingTypes != HandlingType.None)
                    {
                        for (int i = 0; i < role.Slots; i++)
                        {
                            PawnKindDef kind = faction.RandomPawnKind() ?? PawnKindDefOf.AncientSoldier;
                            Pawn crew = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, map.Tile));
                            vehicle.TryAddPawn(crew, vehicle.GetHandler(role.key));
                        }
                    }
                }

                if (!entry.cargoItems.NullOrEmpty())
                {
                    foreach (var stack in entry.cargoItems)
                    {
                        if (stack.thingDef != null)
                        {
                            Thing item = ThingMaker.MakeThing(stack.thingDef);
                            item.stackCount = stack.count.RandomInRange;
                            vehicle.inventory.innerContainer.TryAdd(item);
                        }
                    }
                }

                if (entry.isMortar)
                {
                   map.GetComponent<VRF_LeaderManager>()?.RegisterMortar(vehicle);
                }

                Patch_VehicleNPCOnOff.UpdateVehiclePower(vehicle);

                spawnedList.Add(vehicle);
            }
        }

        private bool TryFindSpawnCell(Map map, CellRect rect, IntVec2 size, out IntVec3 cell)
        {
            for (int i = 0; i < 50; i++)
            {
                CellRect expanded = rect.ExpandedBy(15);
                IntVec3 c = expanded.RandomCell;

                if (rect.Contains(c)) continue; 

                CellRect spawnRect = GenAdj.OccupiedRect(c, Rot4.North, size);
                if (spawnRect.InBounds(map) && !spawnRect.Cells.Any(cl => cl.Impassable(map) || cl.GetEdifice(map) != null || cl.Roofed(map)))
                {
                    cell = c;
                    return true;
                }
            }
            cell = IntVec3.Invalid;
            return false;
        }
    }
}
