using UnityEngine;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;
using Vehicles;
using VehicleRaid;

namespace VehicleRaidFramework
{
    public class JobGiver_HoverNPCAssault : ThinkNode_JobGiver
    {
        private const int TicksBetweenTargetUpdate = 60;
        private const int MaxPositionSearchCells = 300;

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn is VehiclePawn vehicle)) return null;
            if (!vehicle.Spawned || vehicle.Map == null) return null;

            CompVehicleHover hoverComp = vehicle.GetComp<CompVehicleHover>();
            if (hoverComp == null || hoverComp.State != HoverState.Hovering) return null;

            if (vehicle.mindState?.duty?.def?.defName != "VRF_VehicleSearchAndDestroy") return null;

            if (vehicle.IsHashIntervalTick(TicksBetweenTargetUpdate))
            {
                float maxRange = vehicle.CompVehicleTurrets?.MaxRange ?? 60f;
                float minRange = GetEffectiveMinRange(vehicle);
                float idealRange = Mathf.Clamp(maxRange * 0.5f, minRange + 2f, maxRange - 2f);
                Vector2 hoverPos = hoverComp.realPos;

                if (HasEnemyTooClose(vehicle, hoverComp, minRange))
                {
                    Vector3 evadePos = FindEvadePosition(vehicle, hoverComp, minRange, maxRange);
                    if (evadePos != Vector3.zero)
                        hoverComp.SetTarget(evadePos);
                }
                else
                {
                    Thing enemy = FindBestEnemy(vehicle, hoverComp, minRange, maxRange);
                    if (enemy != null)
                    {
                        float dist = Vector2.Distance(hoverPos, new Vector2(enemy.DrawPos.x, enemy.DrawPos.z));
                        IntVec3 hoverCell = new IntVec3(Mathf.RoundToInt(hoverPos.x - 0.5f), 0, Mathf.RoundToInt(hoverPos.y - 0.5f));
                        bool hasLOS = GenSight.LineOfSight(hoverCell, enemy.Position, vehicle.Map, true);

                        RoofDef enemyRoof = vehicle.Map.roofGrid.RoofAt(enemy.Position);
                        bool enemyUnderBlockingRoof = enemyRoof != null && HoverRoofUtil.IsBlockingRoof(enemyRoof);

                        if (enemyUnderBlockingRoof)
                        {
                            hoverComp.isAttackingTarget = false;
                            hoverComp.attackTarget = LocalTargetInfo.Invalid;
                            hoverComp.isFacingTargetNPC = false;
                            hoverComp.facingTargetNPC = null;
                        }
                        else
                        {
                            bool needsReposition = !hasLOS || dist < minRange || dist > maxRange;
                            if (needsReposition)
                            {
                                Vector3 bestPos = FindBestFirePosition(vehicle, hoverComp, enemy, idealRange, maxRange, minRange);
                                if (bestPos != Vector3.zero)
                                    hoverComp.SetTarget(bestPos);
                            }

                            if (!hoverComp.isAttackingTarget || hoverComp.attackTarget.Thing != enemy)
                            {
                                hoverComp.attackTarget = enemy;
                                hoverComp.isAttackingTarget = true;
                                hoverComp.facingTargetNPC = enemy;
                                hoverComp.isFacingTargetNPC = true;
                            }
                        }
                    }
                    else
                    {
                        hoverComp.isAttackingTarget = false;
                        hoverComp.attackTarget = LocalTargetInfo.Invalid;
                        hoverComp.isFacingTargetNPC = false;
                        hoverComp.facingTargetNPC = null;
                    }
                }
            }

            return JobMaker.MakeJob(JobDefOf.Wait_Combat, TicksBetweenTargetUpdate, true);
        }

        private bool HasEnemyTooClose(VehiclePawn vehicle, CompVehicleHover hoverComp, float minRange)
        {
            if (minRange <= 0f) return false;

            var hostileTargets = vehicle.Map.attackTargetsCache.TargetsHostileToFaction(vehicle.Faction);
            if (hostileTargets == null) return false;

            Vector2 hoverPos = hoverComp.realPos;
            float minRangeSq = minRange * minRange;

            foreach (var target in hostileTargets)
            {
                Thing t = target.Thing;
                if (t == null || t.Destroyed || !t.Spawned) continue;
                if (t is Pawn p && (p.Dead || p.Downed)) continue;
                if (t.Map.fogGrid.IsFogged(t.Position)) continue;

                float distSq = (hoverPos - new Vector2(t.DrawPos.x, t.DrawPos.z)).sqrMagnitude;
                if (distSq < minRangeSq)
                    return true;
            }
            return false;
        }

        private Vector3 FindEvadePosition(VehiclePawn vehicle, CompVehicleHover hoverComp, float minRange, float maxRange)
        {
            Map map = vehicle.Map;
            Vector2 hoverPos = hoverComp.realPos;
            IntVec3 hoverCell = new IntVec3(Mathf.RoundToInt(hoverPos.x - 0.5f), 0, Mathf.RoundToInt(hoverPos.y - 0.5f));

            Vector2 awayDir = Vector2.zero;
            int count = 0;
            var hostileTargets = vehicle.Map.attackTargetsCache.TargetsHostileToFaction(vehicle.Faction);
            float minRangeSq = minRange * minRange;

            foreach (var target in hostileTargets)
            {
                Thing t = target.Thing;
                if (t == null || t.Destroyed || !t.Spawned) continue;
                if (t is Pawn p && (p.Dead || p.Downed)) continue;

                Vector2 toEnemy = new Vector2(t.DrawPos.x, t.DrawPos.z) - hoverPos;
                if (toEnemy.sqrMagnitude < minRangeSq)
                {
                    awayDir -= toEnemy.normalized;
                    count++;
                }
            }

            if (count == 0) return Vector3.zero;

            awayDir = awayDir.normalized;
            float evadeDist = minRange + 5f;

            for (float d = evadeDist; d <= maxRange; d += 2f)
            {
                IntVec3 candidate = new IntVec3(
                    Mathf.RoundToInt(hoverPos.x + awayDir.x * d - 0.5f),
                    0,
                    Mathf.RoundToInt(hoverPos.y + awayDir.y * d - 0.5f));

                if (candidate.InBounds(map))
                    return candidate.ToVector3Shifted();
            }

            return Vector3.zero;
        }

        private Vector3 FindBestFirePosition(VehiclePawn vehicle, CompVehicleHover hoverComp, Thing enemy, float idealRange, float maxRange, float minRange)
        {
            Map map = vehicle.Map;
            IntVec3 enemyCell = enemy.Position;
            Vector2 hoverPos = hoverComp.realPos;

            Vector3 bestPos = Vector3.zero;
            float bestScore = float.MaxValue;
            int checked_ = 0;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(enemyCell, maxRange, false))
            {
                if (checked_++ >= MaxPositionSearchCells) break;
                if (!cell.InBounds(map)) continue;

                float distToEnemy = cell.DistanceTo(enemyCell);
                if (distToEnemy < minRange || distToEnemy > maxRange) continue;

                if (!GenSight.LineOfSight(cell, enemyCell, map, true)) continue;

                float distFromCurrent = Vector2.Distance(hoverPos, new Vector2(cell.x + 0.5f, cell.z + 0.5f));
                float rangeScore = Mathf.Abs(distToEnemy - idealRange);
                float score = rangeScore * 2f + distFromCurrent * 0.5f + Rand.Range(0f, 5f);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestPos = cell.ToVector3Shifted();
                }
            }

            return bestPos;
        }

        private Thing FindBestEnemy(VehiclePawn vehicle, CompVehicleHover hoverComp, float minRange, float maxRange)
        {
            var hostileTargets = vehicle.Map.attackTargetsCache.TargetsHostileToFaction(vehicle.Faction);
            if (hostileTargets == null || hostileTargets.Count == 0) return null;

            Vector2 hoverPos = hoverComp.realPos;

            Thing bestInRange = null;
            float bestInRangeDistSq = float.MaxValue;
            Thing bestOutOfRange = null;
            float bestOutOfRangeDistSq = float.MaxValue;

            foreach (var target in hostileTargets)
            {
                Thing t = target.Thing;
                if (t == null || t.Destroyed || t.Map == null) continue;
                if (t is Pawn p && (p.Dead || p.Downed)) continue;
                if (t.Map.fogGrid.IsFogged(t.Position)) continue;

                RoofDef roof = t.Map.roofGrid.RoofAt(t.Position);
                if (roof != null && HoverRoofUtil.IsBlockingRoof(roof)) continue;

                float dist = Vector2.Distance(hoverPos, new Vector2(t.DrawPos.x, t.DrawPos.z));

                if (dist >= minRange && dist <= maxRange)
                {
                    float distSq = dist * dist;
                    if (distSq < bestInRangeDistSq)
                    {
                        bestInRangeDistSq = distSq;
                        bestInRange = t;
                    }
                }
                else if (dist > minRange)
                {
                    float distSq = dist * dist;
                    if (distSq < bestOutOfRangeDistSq)
                    {
                        bestOutOfRangeDistSq = distSq;
                        bestOutOfRange = t;
                    }
                }
            }

            return bestInRange ?? bestOutOfRange;
        }

        private float GetEffectiveMinRange(VehiclePawn vehicle)
        {
            var turretComp = vehicle.CompVehicleTurrets;
            if (turretComp == null || turretComp.turrets == null) return 0f;

            float maxMinRange = 0f;
            foreach (var turret in turretComp.turrets)
            {
                if (turret == null) continue;
                if (turret.MinRange > maxMinRange)
                    maxMinRange = turret.MinRange;
            }
            return maxMinRange;
        }
    }

    public class JobGiver_HoverNPCExit : ThinkNode_JobGiver
    {
        private const int TicksBetweenUpdate = 120;

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn is VehiclePawn vehicle)) return null;
            if (!vehicle.Spawned || vehicle.Map == null) return null;

            CompVehicleHover hoverComp = vehicle.GetComp<CompVehicleHover>();
            if (hoverComp == null || hoverComp.State != HoverState.Hovering) return null;

            string dutyName = vehicle.mindState?.duty?.def?.defName;
            if (dutyName != "VRF_VehicleExitMap" && dutyName != "ExitMapBest") return null;

            if (vehicle.IsHashIntervalTick(TicksBetweenUpdate))
            {
                hoverComp.isAttackingTarget = false;
                hoverComp.attackTarget = LocalTargetInfo.Invalid;
                hoverComp.isFacingTargetNPC = false;
                hoverComp.facingTargetNPC = null;

                IntVec3 exitCell;
                if (VehicleTrafficManager.TryFindExitCell(vehicle, out exitCell))
                {
                    hoverComp.SetTarget(exitCell.ToVector3Shifted());

                    Vector2 hoverPos = hoverComp.realPos;
                    float distToExit = Vector2.Distance(hoverPos, new Vector2(exitCell.x + 0.5f, exitCell.z + 0.5f));
                    if (distToExit < 3f)
                    {
                        vehicle.ExitMap(false, CellRect.WholeMap(vehicle.Map).GetClosestEdge(vehicle.Position));
                    }
                }
            }

            return JobMaker.MakeJob(JobDefOf.Wait_Combat, TicksBetweenUpdate, true);
        }
    }
}
