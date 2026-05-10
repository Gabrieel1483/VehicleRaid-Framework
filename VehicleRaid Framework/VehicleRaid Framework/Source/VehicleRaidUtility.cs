using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using RimWorld;
using Vehicles;

namespace VehicleRaidFramework
{
    public static class VehicleRaidUtility
    {
        public static List<Pawn> SpawnArmoredDivision(Map map, Faction faction, IncidentParms parms, VehicleRaidExtension ext)
        {
            float pointsLeft = parms.points;
            List<Pawn> spawnedPawns = new List<Pawn>();
            List<VehiclePawn> vehiclesToSpawn = new List<VehiclePawn>();
            List<Pawn> infantryToSpawn = new List<Pawn>();

            if (ext.forcedPawns != null)
            {
                for (int i = 0; i < ext.forcedPawns.Count; i++)
                {
                    Pawn p = GenerateInfantry(ext.forcedPawns[i], faction, map);
                    if (p != null) infantryToSpawn.Add(p);
                }
            }

            if (ext.vehicleOptions != null)
            {
                for (int i = 0; i < ext.vehicleOptions.Count; i++)
                {
                    VehicleRaidOption option = ext.vehicleOptions[i];
                    if (option.forceCount > 0)
                    {
                        for (int j = 0; j < option.forceCount; j++)
                        {
                            VehiclePawn v = GenerateVehicleWithCrew(option, faction, map, ext, spawnedPawns);
                            if (v != null) vehiclesToSpawn.Add(v);
                        }
                    }
                }
            }

            if (ext.spawnInfantry)
            {
                float infantryBudget = parms.points * ext.infantryPointsFraction;
                float spentOnInfantry = 0;
                while (spentOnInfantry < infantryBudget)
                {
                    PawnKindDef kind = faction.RandomPawnKind();
                    if (kind == null || kind.combatPower > (infantryBudget - spentOnInfantry)) break;

                    Pawn p = GenerateInfantry(kind, faction, map);
                    if (p != null)
                    {
                        infantryToSpawn.Add(p);
                        spentOnInfantry += kind.combatPower;
                        pointsLeft -= kind.combatPower;
                    }
                }
            }

            bool firstVehicle = (vehiclesToSpawn.Count == 0);
            while (pointsLeft > 0 || firstVehicle)
            {

                List<VehicleRaidOption> spawnableOptions = new List<VehicleRaidOption>();
                foreach (var opt in ext.vehicleOptions)
                {
                    if (opt.spawnVehicle) spawnableOptions.Add(opt);
                }

                if (spawnableOptions.Count == 0) break;
                
                VehicleRaidOption result = null;
                float totalWeight = 0;
                foreach (var opt in spawnableOptions) totalWeight += opt.weight;
                float choice = Rand.Value * totalWeight;
                float currentSum = 0;
                foreach (var opt in spawnableOptions)
                {
                    currentSum += opt.weight;
                    if (currentSum >= choice) { result = opt; break; }
                }
                
                if (result == null) break;

                VehicleDef vehicleDef = result.kindDef.race as VehicleDef;
                if (vehicleDef == null) break;

                float totalCost = result.kindDef.combatPower;
                if (!firstVehicle && pointsLeft < totalCost) break;

                VehiclePawn vehicle = GenerateVehicleWithCrew(result, faction, map, ext, spawnedPawns);
                if (vehicle != null)
                {
                    vehiclesToSpawn.Add(vehicle);
                    pointsLeft -= totalCost;
                }
                else if (firstVehicle) break;

                firstVehicle = false;
                if (pointsLeft <= 0) break;
            }

            if (vehiclesToSpawn.Count == 0) return new List<Pawn>();

            List<IntVec3> groupCenters = new List<IntVec3>();
            List<IntVec3> allSpawnedCells = new List<IntVec3>();
            int batchSize = 4;

            for (int i = 0; i < vehiclesToSpawn.Count; i += batchSize)
            {
                int currentBatchSize = Math.Min(batchSize, vehiclesToSpawn.Count - i);
                List<VehiclePawn> batch = new List<VehiclePawn>();
                for (int k = 0; k < currentBatchSize; k++) batch.Add(vehiclesToSpawn[i + k]);
                
                IntVec3 groupBase;
                if (groupCenters.Count == 0)
                {
                    List<VehicleDef> batchDefs = new List<VehicleDef>();
                    foreach (var v in batch) batchDefs.Add(v.VehicleDef);

                    if (!VehicleTrafficManager.TryFindGroupEntryPoint(map, batchDefs, out groupBase))
                        groupBase = CellFinder.RandomEdgeCell(map);
                }
                else
                {
                    groupBase = VehicleTrafficManager.FindNearbySpawnZone(groupCenters[0], map, batch[0].VehicleDef, groupCenters);
                }
                
                groupCenters.Add(groupBase);
                Rot4 rot = groupBase.OnEdge(map) ? groupBase.GetBeginningOfRoadDirection(map) : Rot4.North;

                List<IntVec3> formationCells = VehicleTrafficManager.CalculateFormationCells(
                    groupBase, map, rot, batch, allSpawnedCells);

                for (int j = 0; j < batch.Count; j++)
                {
                    VehiclePawn v = batch[j];
                    IntVec3 finalCell = formationCells[j];
                    
                    GenSpawn.Spawn(v, finalCell, map, rot);
                    spawnedPawns.Add(v);
                    allSpawnedCells.Add(finalCell);

                }
            }

            foreach (var pawn in infantryToSpawn)
            {
                IntVec3 pCell = groupCenters.Count > 0 
                    ? CellFinder.RandomClosewalkCellNear(groupCenters[Rand.Range(0, groupCenters.Count)], map, 10) 
                    : CellFinder.RandomEdgeCell(map);
                GenSpawn.Spawn(pawn, pCell, map, Rot4.Random);
                spawnedPawns.Add(pawn);
            }

            return spawnedPawns;
        }

        private static VehiclePawn GenerateVehicleWithCrew(VehicleRaidOption option, Faction faction, Map map, VehicleRaidExtension ext, List<Pawn> allPawns)
        {
            VehicleDef vehicleDef = option.kindDef.race as VehicleDef;
            VehiclePawn vehicle = VehicleSpawner.GenerateVehicle(vehicleDef, faction);
            if (vehicle == null) return null;

            LoadFuel(vehicle);

            var finalCargo = (option.cargoItems != null && option.cargoItems.Count > 0) ? option.cargoItems : ext.cargoItems;
            if (finalCargo != null)
            {
                foreach (var cargo in finalCargo) LoadCargo(vehicle, cargo.thingDef, cargo.count.RandomInRange);
            }

            if (vehicleDef.properties?.roles != null)
            {
                foreach (VehicleRole role in vehicleDef.properties.roles)
                {
                    if (role.HandlingTypes != HandlingType.None)
                    {
                        for (int i = 0; i < role.Slots; i++)
                        {
                            Pawn crew = GenerateCrewPawn(faction, map, option.kindDef);
                            if (crew != null)
                            {
                                BoardPawnInRole(vehicle, crew, role.key);
                                allPawns.Add(crew);
                            }
                        }
                    }
                }
            }

            if (vehicle.ignition != null) vehicle.ignition.Drafted = true;

            var leaderManager = map.GetComponent<VRF_LeaderManager>();
            if (leaderManager != null)
            {
                int optIdx = ext.vehicleOptions.IndexOf(option);
                if (optIdx >= 0)
                {
                    if (option.pawnFollowVehicle) leaderManager.RegisterLeader(vehicle, optIdx);
                    if (option.isMortar) leaderManager.RegisterMortar(vehicle);
                }
            }

            return vehicle;
        }

        private static Pawn GenerateInfantry(PawnKindDef kind, Faction faction, Map map)
        {
            return PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, map.Tile, forceGenerateNewPawn: true, mustBeCapableOfViolence: true));
        }

        private static void LoadFuel(VehiclePawn vehicle)
        {
            CompFueledTravel fuelComp = vehicle.GetComp<CompFueledTravel>();
            if (fuelComp != null) fuelComp.Refuel(fuelComp.FuelCapacity);
        }

        private static void LoadCargo(VehiclePawn vehicle, ThingDef thingDef, int count)
        {
            if (thingDef == null || count <= 0) return;
            Thing item = ThingMaker.MakeThing(thingDef);
            item.stackCount = count;
            vehicle.inventory.innerContainer.TryAdd(item);
        }

        private static Pawn GenerateCrewPawn(Faction faction, Map map, PawnKindDef vehicleKind)
        {
            PawnKindDef kind = faction.RandomPawnKind() ?? PawnKindDefOf.AncientSoldier;
            return PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, map.Tile, forceGenerateNewPawn: true, mustBeCapableOfViolence: true));
        }

        private static void BoardPawnInRole(VehiclePawn vehicle, Pawn pawn, string roleKey)
        {
            VehicleRoleHandler handler = vehicle.GetHandler(roleKey);
            if (handler != null) vehicle.TryAddPawn(pawn, handler);
        }

    }

}

