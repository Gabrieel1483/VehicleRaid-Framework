using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;
using Vehicles;

namespace VehicleRaidFramework
{
    public static class VehicleTrafficManager
    {
        private const int MinSeparationBetweenHitboxes = 3;



        public static IntVec3 GetSafeSpawnCell(IntVec3 root, Map map, VehicleDef vehicleDef, int extraPadding = 2)
        {
            int size = Mathf.Max(vehicleDef.size.x, vehicleDef.size.z);
            int padding = Mathf.CeilToInt(size / 2f) + extraPadding;

            int x = Mathf.Clamp(root.x, padding, map.Size.x - padding - 1);
            int z = Mathf.Clamp(root.z, padding, map.Size.z - padding - 1);

            return new IntVec3(x, root.y, z);
        }







        public static bool TryFindGroupEntryPoint(Map map, List<VehicleDef> vehicleDefs, out IntVec3 cell)
        {
            VehicleDef widest = null;
            int widestSize = 0;
            foreach (var v in vehicleDefs)
            {
                int s = Mathf.Max(v.size.x, v.size.z);
                if (s > widestSize) { widestSize = s; widest = v; }
            }

            if (widest == null)
            {
                cell = IntVec3.Invalid;
                return false;
            }

            int groupZoneRadius = Mathf.Max(12, vehicleDefs.Count * 6);

            IntVec3 bestCell = IntVec3.Invalid;
            float bestScore = float.MinValue;
            int candidatesFound = 0;

            for (int i = 0; i < 120; i++)
            {
                IntVec3 potentialCell;
                if (!CellFinderExtended.TryFindRandomEdgeCellWith(
                    c => c.Standable(map) && !c.Fogged(map),
                    map, Rot4.Invalid, widest, CellFinder.EdgeRoadChance_Hostile, out potentialCell))
                    continue;

                IntVec3 safeCell = GetSafeSpawnCell(potentialCell, map, widest);

                if (!CanReachObjectiveArea(safeCell, map)) continue;

                float score = ScoreSpawnZone(safeCell, map, groupZoneRadius);
                if (score < 0) continue;

                candidatesFound++;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = safeCell;
                }

                if (candidatesFound >= 8 && bestScore > 80f) break;
            }

            if (bestCell.IsValid)
            {
                cell = bestCell;
                return true;
            }

            return RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Hostile);
        }




        private static float ScoreSpawnZone(IntVec3 center, Map map, int radius)
        {
            int walkableCount = 0;
            int buildingCount = 0;
            int waterCount = 0;
            int totalChecked = 0;

            CellRect rect = CellRect.CenteredOn(center, radius, radius);

            foreach (IntVec3 cell in rect)
            {
                if (!cell.InBounds(map)) continue;
                totalChecked++;

                if (cell.Walkable(map) && cell.GetEdifice(map) == null)
                {
                    walkableCount++;

                    TerrainDef terrain = cell.GetTerrain(map);
                    if (terrain != null && (terrain.passability == Traversability.Impassable ||
                        terrain.defName.Contains("Water") || terrain.defName.Contains("Marsh")))
                    {
                        waterCount++;
                    }
                }
                else if (cell.GetEdifice(map) != null)
                {
                    buildingCount++;
                }
            }

            if (totalChecked == 0) return -1;

            float openRatio = (float)walkableCount / totalChecked;

            if (openRatio < 0.85f) return -1;

            float score = openRatio * 60f;

            if (openRatio > 0.95f) score += 25f;
            else if (openRatio > 0.90f) score += 15f;

            score -= Mathf.Min(waterCount * 0.5f, 15f);

            score -= Mathf.Min(buildingCount * 1f, 20f);

            if (HasNearbyRoad(center, map)) score += 10f;

            return score;
        }

        private static bool HasNearbyRoad(IntVec3 center, Map map)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    IntVec3 check = center + new IntVec3(dx, 0, dz);
                    if (check.InBounds(map))
                    {
                        TerrainDef terrain = check.GetTerrain(map);
                        if (terrain != null && terrain.defName.Contains("Road"))
                            return true;
                    }
                }
            }
            return false;
        }

        private static bool CanReachObjectiveArea(IntVec3 start, Map map)
        {
            return map.reachability.CanReach(start, map.Center, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly));
        }








        public static List<IntVec3> CalculateFormationCells(
            IntVec3 groupBase, Map map, Rot4 arrivalDirection,
            List<VehiclePawn> vehicles, List<IntVec3> previouslySpawnedCells)
        {
            List<IntVec3> result = new List<IntVec3>();
            List<CellRect> reservedRects = new List<CellRect>();

            if (previouslySpawnedCells != null)
            {
                foreach (var c in previouslySpawnedCells)
                {

                    reservedRects.Add(CellRect.CenteredOn(c, 5, 5).ExpandedBy(MinSeparationBetweenHitboxes));
                }
            }

            IntVec3 forward = arrivalDirection.FacingCell;
            IntVec3 side = new IntVec3(-forward.z, 0, forward.x);

            for (int i = 0; i < vehicles.Count; i++)
            {
                VehiclePawn v = vehicles[i];
                int vSizeX = v.VehicleDef.size.x;
                int vSizeZ = v.VehicleDef.size.z;
                int maxDim = Mathf.Max(vSizeX, vSizeZ);

                int col = i % 2;
                int row = i / 2;

                int lateralBase = (maxDim / 2) + MinSeparationBetweenHitboxes + 1;
                int lateralOffset = (col == 0) ? -lateralBase : lateralBase;

                lateralOffset += Rand.RangeInclusive(-2, 2);

                int depthBase = row * (maxDim + MinSeparationBetweenHitboxes + 2);

                int stagger = (col == 1) ? Rand.RangeInclusive(2, 4) : 0;
                int depthOffset = depthBase - stagger;

                IntVec3 targetCell;
                if (i == 0)
                {
                    targetCell = groupBase;
                }
                else
                {
                    targetCell = groupBase + (side * lateralOffset) + (forward * -depthOffset);
                }

                IntVec3 finalCell = FindBestSpawnCell(targetCell, map, v.VehicleDef, reservedRects);

                result.Add(finalCell);
                reservedRects.Add(CellRect.CenteredOn(finalCell, vSizeX, vSizeZ).ExpandedBy(MinSeparationBetweenHitboxes));
            }

            return result;
        }





        private static IntVec3 FindBestSpawnCell(IntVec3 target, Map map, VehicleDef vehicleDef, List<CellRect> reservedRects)
        {

            IntVec3 clamped = GetSafeSpawnCell(target, map, vehicleDef);

            if (IsVehicleAreaClear(clamped, map, vehicleDef, reservedRects))
                return clamped;

            Vector3 toCenterDir = (map.Center - clamped).ToVector3().normalized;
            IntVec3 toCenter = new IntVec3(
                Mathf.RoundToInt(toCenterDir.x),
                0,
                Mathf.RoundToInt(toCenterDir.z)
            );

            if (toCenter.x == 0 && toCenter.z == 0) toCenter = IntVec3.North;

            for (int dist = 1; dist <= 15; dist++)
            {
                for (int spread = -2; spread <= 2; spread++)
                {
                    IntVec3 checkPos = clamped + (toCenter * dist) + new IntVec3(-toCenter.z * spread, 0, toCenter.x * spread);
                    checkPos = GetSafeSpawnCell(checkPos, map, vehicleDef);
                    if (IsVehicleAreaClear(checkPos, map, vehicleDef, reservedRects))
                        return checkPos;
                }
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(clamped, 25f, true))
            {
                if (!cell.InBounds(map)) continue;
                IntVec3 safed = GetSafeSpawnCell(cell, map, vehicleDef);
                if (IsVehicleAreaClear(safed, map, vehicleDef, reservedRects))
                    return safed;
            }

            return clamped;
        }




        private static bool IsVehicleAreaClear(IntVec3 center, Map map, VehicleDef vehicleDef, List<CellRect> reservedRects)
        {
            CellRect vehicleRect = CellRect.CenteredOn(center, vehicleDef.size.x, vehicleDef.size.z);
            CellRect checkRect = vehicleRect.ExpandedBy(1);

            if (reservedRects != null)
            {
                for (int i = 0; i < reservedRects.Count; i++)
                {
                    if (vehicleRect.Overlaps(reservedRects[i])) return false;
                }
            }

            foreach (IntVec3 cell in checkRect)
            {
                if (!cell.InBounds(map)) return false;
                if (!cell.Walkable(map)) return false;

                Building edifice = cell.GetEdifice(map);
                if (edifice != null) return false;
            }

            return true;
        }







        public static IntVec3 FindNearbySpawnZone(IntVec3 primaryEntry, Map map, VehicleDef refVehicle, List<IntVec3> usedZones)
        {
            int minDistFromUsed = 30;

            IntVec3 bestCandidate = IntVec3.Invalid;
            float bestScore = float.MinValue;

            for (int attempt = 0; attempt < 80; attempt++)
            {
                IntVec3 candidate;
                if (!CellFinderExtended.TryFindRandomEdgeCellWith(
                    c => c.Standable(map) && !c.Fogged(map),
                    map, Rot4.Invalid, refVehicle, CellFinder.EdgeRoadChance_Hostile, out candidate))
                    continue;

                IntVec3 safeCandidate = GetSafeSpawnCell(candidate, map, refVehicle);

                bool tooClose = false;
                foreach (var used in usedZones)
                {
                    if (safeCandidate.DistanceTo(used) < minDistFromUsed)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                if (!CanReachObjectiveArea(safeCandidate, map)) continue;

                float score = ScoreSpawnZone(safeCandidate, map, 14);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidate = safeCandidate;
                }

                if (bestScore > 70f) break;
            }

            if (bestCandidate.IsValid) return bestCandidate;

            IntVec3 fallback = primaryEntry + new IntVec3(Rand.RangeInclusive(-25, 25), 0, Rand.RangeInclusive(-25, 25));
            return GetSafeSpawnCell(fallback, map, refVehicle);
        }




        public static Rot4 GetBeginningOfRoadDirection(this IntVec3 cell, Map map)
        {
            if (cell.x == 0) return Rot4.East;
            if (cell.x == map.Size.x - 1) return Rot4.West;
            if (cell.z == 0) return Rot4.North;
            if (cell.z == map.Size.z - 1) return Rot4.South;
            return Rot4.North;
        }



        public static bool TryFindEntryCell(Map map, VehicleDef vehicleDef, out IntVec3 cell)
        {
            if (CellFinderExtended.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map), map, Rot4.Invalid, vehicleDef, CellFinder.EdgeRoadChance_Hostile, out cell))
            {
                cell = GetSafeSpawnCell(cell, map, vehicleDef);
                return true;
            }

            if (RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Hostile))
            {
                cell = GetSafeSpawnCell(cell, map, vehicleDef);
                return true;
            }

            cell = IntVec3.Invalid;
            return false;
        }



        public static bool TryFindExitCell(VehiclePawn vehicle, out IntVec3 exitCell)
        {
            if (CellFinderExtended.TryFindBestExitSpot(vehicle, out exitCell) || CellFinderExtended.TryFindRandomExitSpot(vehicle, out exitCell))
            {
                return true;
            }

            exitCell = IntVec3.Invalid;
            return false;
        }
    }
}

