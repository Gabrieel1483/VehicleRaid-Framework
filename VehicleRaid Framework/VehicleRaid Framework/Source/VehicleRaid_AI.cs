using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;
using Vehicles;
using SmashTools;

namespace VehicleRaidFramework
{
    
    
    
    public class LordJob_VehicleRaid : LordJob
    {
        private Faction assaulterFaction;
        private int stayTicks = 30000;
        public bool updatingDuties = false;
        private static readonly IntRange AssaultTimeRange = new IntRange(30000, 45000);

        public LordJob_VehicleRaid() { }
        public LordJob_VehicleRaid(Faction faction, int stayTicks = 0)
        {
            this.assaulterFaction = faction;
            this.stayTicks = stayTicks > 0 ? stayTicks : AssaultTimeRange.RandomInRange;
        }

        public override bool GuiltyOnDowned => true;

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            LordToil assaultToil = new LordToil_VehicleSearchAndDestroy();
            stateGraph.AddToil(assaultToil);

            LordToil exitToil = new LordToil_VehicleExitMap();
            stateGraph.AddToil(exitToil);

            Transition timeoutTransition = new Transition(assaultToil, exitToil);
            timeoutTransition.AddTrigger(new Trigger_TicksPassed(stayTicks));
            timeoutTransition.AddPreAction(new TransitionAction_Message("MessageRaidersGivenUpLeaving".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
            stateGraph.AddTransition(timeoutTransition);

            Transition peaceTransition = new Transition(assaultToil, exitToil);
            peaceTransition.AddTrigger(new Trigger_BecameNonHostileToPlayer());
            peaceTransition.AddPreAction(new TransitionAction_Message("MessageRaidersLeaving".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
            stateGraph.AddTransition(peaceTransition);

            Transition satisfiedTransition = new Transition(assaultToil, exitToil);
            satisfiedTransition.AddTrigger(new Trigger_FractionColonyDamageTaken(0.30f, 900f));
            satisfiedTransition.AddPreAction(new TransitionAction_Message("MessageRaidersSatisfiedLeaving".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
            stateGraph.AddTransition(satisfiedTransition);

            Transition retreatTransition = new Transition(assaultToil, exitToil);
            retreatTransition.AddTrigger(new Trigger_FractionPawnsLost(0.5f));
            retreatTransition.AddPreAction(new TransitionAction_Message("MessageRaidersGivenUpLeaving".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
            stateGraph.AddTransition(retreatTransition);

            if (!assaulterFaction.HostileTo(Faction.OfPlayer))
            {
                Transition allyVictory = new Transition(assaultToil, exitToil);
                allyVictory.AddTrigger(new Trigger_TicksPassedWithoutHarm(5000));
                allyVictory.AddPreAction(new TransitionAction_Message("MessageFriendlyFightersLeaving".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
                stateGraph.AddTransition(allyVictory);
            }

            return stateGraph;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref assaulterFaction, "assaulterFaction");
            Scribe_Values.Look(ref stayTicks, "stayTicks", 30000);
        }
    }

    
    
    
    public class LordToil_VehicleSearchAndDestroy : LordToil
    {
        public override bool ForceHighStoryDanger => true;
        public override bool AllowSatisfyLongNeeds => false;

        public override void Init()
        {
            base.Init();
            LessonAutoActivator.TeachOpportunity(ConceptDefOf.Drafting, OpportunityType.Critical);
        }

        public override void UpdateAllDuties()
        {
            var vJob = this.lord.LordJob as LordJob_VehicleRaid;
            if (vJob != null) vJob.updatingDuties = true;

            try
            {
                var leaderManager = this.lord.Map.GetComponent<VRF_LeaderManager>();
                leaderManager?.ForceRefresh();
                
                List<Pawn> pawnsToRemove = new List<Pawn>();
                
                foreach (Pawn pawn in this.lord.ownedPawns)
                {
                    if (pawn is VehiclePawn v)
                    {
                        if (v.mindState.duty?.def.defName == "VRF_VehicleExitMap" || v.mindState.duty?.def == DutyDefOf.ExitMapBest)
                        {
                            continue;
                        }

                        if (CrewManager.HasOperationalDriver(v) || CrewManager.AnyFriendlyInfantryNearby(v) || CrewManager.IsAnyPawnBoarding(v))
                        {
                            var dutyDef = VRF_DutyDefOf.VRF_VehicleSearchAndDestroy ?? DefDatabase<DutyDef>.GetNamed("VRF_VehicleSearchAndDestroy", false);
                            if (pawn.mindState.duty?.def != dutyDef)
                            {
                                pawn.mindState.duty = new PawnDuty(dutyDef);
                            }
                        }
                        else
                        {
                            pawnsToRemove.Add(v);
                        }
                    }
                    else
                    {
                        var dutyDef = VRF_DutyDefOf.VRF_InfantryAssault ?? DefDatabase<DutyDef>.GetNamed("VRF_InfantryAssault", false);
                        if (pawn.mindState.duty?.def != dutyDef)
                        {
                            pawn.mindState.duty = new PawnDuty(dutyDef);
                        }
                    }
                }
                
                foreach (Pawn p in pawnsToRemove)
                {
                    if (p.jobs != null)
                    {
                        p.jobs.StopAll();
                        p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait_Combat, 1000, true), JobCondition.InterruptForced);
                    }
                    p.mindState.duty = null;
                    this.lord.RemovePawn(p);
                }
            }
            finally
            {
                if (vJob != null) vJob.updatingDuties = false;
            }
        }

        public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
        {
            base.Notify_PawnLost(p, condition);
            var vJob = this.lord.LordJob as LordJob_VehicleRaid;
            if (p is VehiclePawn && !(vJob?.updatingDuties ?? false))
            {
                var leaderManager = this.lord.Map.GetComponent<VRF_LeaderManager>();
                leaderManager?.ForceRefresh();
                UpdateAllDuties();
            }
        }
    }

    public class LordToil_VehicleExitMap : LordToil
    {
        public override bool AllowSatisfyLongNeeds => false;

        public override void UpdateAllDuties()
        {
            var vJob = this.lord.LordJob as LordJob_VehicleRaid;
            if (vJob != null) vJob.updatingDuties = true;

            try
            {
                List<Pawn> pawnsToRemove = new List<Pawn>();
            
                foreach (Pawn pawn in this.lord.ownedPawns)
                {
                    if (pawn is VehiclePawn v)
                    {
                        if (CrewManager.HasOperationalDriver(v) || CrewManager.AnyFriendlyInfantryNearby(v) || CrewManager.IsAnyPawnBoarding(v))
                        {
                            var dutyDef = VRF_DutyDefOf.VRF_VehicleExitMap ?? DefDatabase<DutyDef>.GetNamed("VRF_VehicleExitMap", false) ?? DutyDefOf.ExitMapBest;
                            if (pawn.mindState.duty?.def != dutyDef)
                            {
                                pawn.mindState.duty = new PawnDuty(dutyDef);
                            }
                        }
                        else
                        {
                            pawnsToRemove.Add(v);
                        }
                    }
                    else
                    {
                        var dutyDef = VRF_DutyDefOf.VRF_InfantryExit ?? DefDatabase<DutyDef>.GetNamed("VRF_InfantryExit", false) ?? DutyDefOf.ExitMapBest;
                        if (pawn.mindState.duty?.def != dutyDef)
                        {
                            pawn.mindState.duty = new PawnDuty(dutyDef);
                        }
                    }
                }
                
                foreach (Pawn p in pawnsToRemove)
                {
                    if (p.jobs != null)
                    {
                        p.jobs.StopAll();
                        p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait_Combat, 1000, true), JobCondition.InterruptForced);
                    }
                    p.mindState.duty = null;
                    this.lord.RemovePawn(p);
                }
            }
            finally
            {
                if (vJob != null) vJob.updatingDuties = false;
            }
        }
    }

    
    
    
    public class VRF_JobGiver_DynamicAssault : ThinkNode_JobGiver
    {
        private const int WallBaseCost = 20;
        private const float WallHpCost = 0.015f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn is VehiclePawn vehicle) || !vehicle.Spawned || vehicle.Map == null) return null;

            var leaderManager = vehicle.Map.GetComponent<VRF_LeaderManager>();
            if (leaderManager != null && leaderManager.IsMortar(vehicle)) return null;

            if (!CrewManager.CanMove(vehicle))
            {
                return JobMaker.MakeJob(JobDefOf.Wait_Combat, 2000, true);
            }

            if (CrewManager.IsAnyPawnBoarding(vehicle))
            {
                if (vehicle.CurJob != null && vehicle.CurJob.def == JobDefOf.Wait_Combat) return null;
                return JobMaker.MakeJob(JobDefOf.Wait_Combat, 500, true);
            }

            if (CrewManager.IsOutOfAmmo(vehicle))
            {
                CrewManager.CheckRetreat(vehicle);
                return null;
            }

            if (vehicle.CurJobDef == JobDefOf.Goto && vehicle.pather.Moving)
            {
                return null;
            }

            Thing enemy = FindNearestEnemy(vehicle);
            if (enemy == null) return null;

            vehicle.mindState.enemyTarget = enemy;
            float maxRange = vehicle.CompVehicleTurrets?.MaxRange ?? 60f;
            float minRange = vehicle.CompVehicleTurrets?.MinRange ?? 0f;
            float idealRange = Mathf.Clamp(maxRange * 0.5f, minRange + 2f, maxRange - 2f);
            int vehicleWidth = (vehicle.Rotation == Rot4.North || vehicle.Rotation == Rot4.South) ? vehicle.def.size.x : vehicle.def.size.z;

            if (vehicle.CanReachVehicle(new LocalTargetInfo(enemy.Position), PathEndMode.Touch, Danger.Deadly, TraverseMode.NoPassClosedDoors))
            {
                return HandleDirectAssault(vehicle, enemy, idealRange, maxRange, minRange);
            }

            int widthMultiplier = Mathf.Max(vehicleWidth, 1);
            PathFinderCostTuning tuning = new PathFinderCostTuning { costBlockedDoor = WallBaseCost * widthMultiplier, costBlockedWallBase = WallBaseCost * widthMultiplier, costBlockedDoorPerHitPoint = WallHpCost * widthMultiplier, costBlockedWallExtraPerHitPoint = WallHpCost * widthMultiplier };

            Thing wallToBreak = null;
            using (PawnPath path = pawn.Map.pathFinder.FindPathNow(pawn.Position, new LocalTargetInfo(enemy.Position), TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings), tuning))
            {
                if (path.Found) wallToBreak = path.FirstBlockingBuilding(out _, pawn);
                
                if (!path.Found) return JobMaker.MakeJob(JobDefOf.Wait_Combat, 500, true);
            }

            if (wallToBreak == null)
            {
                if (vehicle.CanReachVehicle(new LocalTargetInfo(enemy.Position), PathEndMode.Touch, Danger.Deadly, TraverseMode.NoPassClosedDoors))
                {
                    return HandleDirectAssault(vehicle, enemy, idealRange, maxRange, minRange);
                }
                else
                {
                    wallToBreak = FindNearestWallTowardEnemy(vehicle, enemy);
                    if (wallToBreak == null) return JobMaker.MakeJob(JobDefOf.Wait_Combat, 500, true);
                }
            }

            if (IsBlockedByFriendlyVehicle(vehicle, enemy, out VehiclePawn blockingAlly))
            {
                if (blockingAlly.vehiclePather != null && blockingAlly.vehiclePather.Moving)
                {
                    return JobMaker.MakeJob(JobDefOf.Wait_Combat, 150, true);
                }
                
                if (vehicle.Position.DistanceToSquared(enemy.Position) > blockingAlly.Position.DistanceToSquared(enemy.Position))
                {
                    return JobMaker.MakeJob(JobDefOf.Wait_Combat, 250, true);
                }
            }

            if (wallToBreak != null && wallToBreak.Faction != null && !wallToBreak.Faction.HostileTo(vehicle.Faction))
            {
                return JobMaker.MakeJob(JobDefOf.Wait_Combat, 500, true);
            }

            Thing targetWall = FindBestBreachTarget(vehicle, wallToBreak, vehicleWidth) ?? wallToBreak;
            return HandleBreaching(vehicle, targetWall, idealRange, maxRange, minRange);
        }

        private Job HandleDirectAssault(VehiclePawn vehicle, Thing enemy, float idealRange, float maxRange, float minRange)
        {
            float dist = vehicle.Position.DistanceTo(enemy.Position);
            
            if (dist >= minRange && dist <= maxRange && GenSight.LineOfSight(vehicle.Position, enemy.Position, vehicle.Map))
            {
                return JobMaker.MakeJob(JobDefOf.Wait_Combat, 250, true);
            }

            IntVec3 targetPos = FindPositionAtIdealRange(vehicle, enemy, idealRange, maxRange, minRange);
            if (targetPos.IsValid && targetPos != vehicle.Position)
            {
                Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, targetPos);
                gotoJob.expiryInterval = 2000;
                gotoJob.checkOverrideOnExpire = true;
                return gotoJob;
            }

            Job ramJob = JobMaker.MakeJob(JobDefOf.Goto, enemy);
            ramJob.expiryInterval = 2000;
            return ramJob;
        }

        private Job HandleBreaching(VehiclePawn vehicle, Thing wall, float baseIdealRange, float maxRange, float minRange)
        {
            if (wall.Destroyed)
            {
                return JobMaker.MakeJob(JobDefOf.Wait_Combat, 30, true);
            }

            float breachRange = Rand.Range(Mathf.Max(minRange + 5f, 10f), maxRange - 4f);
            
            float dist = vehicle.Position.DistanceTo(wall.Position);
            if (dist < minRange + 1f)
            {
                IntVec3 retreatPos = FindPositionAtIdealRange(vehicle, wall, breachRange, maxRange, minRange);
                if (retreatPos.IsValid) return JobMaker.MakeJob(JobDefOf.Goto, new LocalTargetInfo(retreatPos), 500, true);
            }
            if (dist >= minRange && dist <= maxRange && GenSight.LineOfSight(vehicle.Position, wall.Position, vehicle.Map))
            {
                vehicle.mindState.breachingTarget = new BreachingTargetData(wall, vehicle.Position);
                return JobMaker.MakeJob(JobDefOf.Wait_Combat, 120, true);
            }
            IntVec3 approachPos = FindPositionAtIdealRange(vehicle, wall, breachRange, maxRange, minRange);
            if (approachPos.IsValid) return JobMaker.MakeJob(JobDefOf.Goto, new LocalTargetInfo(approachPos), 500, true);

            return JobMaker.MakeJob(JobDefOf.Wait_Combat, 120, true);
        }

        private Thing FindBestBreachTarget(VehiclePawn vehicle, Thing mainWall, int vehicleWidth)
        {
            Map map = vehicle.Map;
            IntVec3 wallPos = mainWall.Position;
            IntVec3 dir = wallPos - vehicle.Position;
            bool expandX = Mathf.Abs(dir.x) < Mathf.Abs(dir.z);
            Thing closest = null;
            float closestDist = float.MaxValue;

            for (int offset = -(vehicleWidth/2); offset <= (vehicleWidth/2); offset++)
            {
                IntVec3 checkPos = expandX ? wallPos + new IntVec3(offset, 0, 0) : wallPos + new IntVec3(0, 0, offset);
                Building edifice = checkPos.GetEdifice(map);
                if (edifice != null && !edifice.Destroyed && edifice.def.useHitPoints && (edifice.def.fillPercent > 0.5f || edifice is Building_Door))
                {
                    float d = edifice.Position.DistanceToSquared(vehicle.Position);
                    if (d < closestDist) { closestDist = d; closest = edifice; }
                }
            }
            return closest;
        }

        private IntVec3 FindPositionAtIdealRange(VehiclePawn vehicle, Thing target, float idealRange, float maxRange, float minRange)
        {
            Map map = vehicle.Map;
            var allyRects = new List<CellRect>();
            var allyDestinations = new List<KeyValuePair<IntVec3, int>>();

            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (p is VehiclePawn v && v != vehicle && v.Faction == vehicle.Faction)
                {
                    int vSize = Mathf.Max(v.def.size.x, v.def.size.z);
                    allyRects.Add(v.OccupiedRect().ExpandedBy(3));

                    if (v.CurJob != null && v.CurJob.def == JobDefOf.Goto && v.CurJob.targetA.IsValid)
                    {
                        allyDestinations.Add(new KeyValuePair<IntVec3, int>(v.CurJob.targetA.Cell, vSize));
                    }
                }
            }

            var candidates = new List<KeyValuePair<IntVec3, float>>();
            int cellsChecked = 0;

            float scanRadius = Mathf.Min(idealRange + 5f, maxRange);
            scanRadius = Mathf.Min(scanRadius, 60f);

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(target.Position, scanRadius, true))
            {
                if (cellsChecked++ > 500) break;

                float distToTarget = cell.DistanceTo(target.Position);
                if (distToTarget < minRange + 1f || !cell.Standable(map) || !GenSight.LineOfSight(cell, target.Position, map)) continue;

                bool insideAlly = false;
                for (int i = 0; i < allyRects.Count; i++)
                {
                    if (allyRects[i].Contains(cell)) { insideAlly = true; break; }
                }
                if (insideAlly) continue;

                bool destinationTaken = false;
                for (int i = 0; i < allyDestinations.Count; i++)
                {
                    float standoffDistance = allyDestinations[i].Value + 2f;
                    if (cell.DistanceToSquared(allyDestinations[i].Key) < (standoffDistance * standoffDistance))
                    {
                        destinationTaken = true;
                        break;
                    }
                }
                if (destinationTaken) continue;

                float score = Mathf.Abs(distToTarget - idealRange) * 3f + cell.DistanceTo(vehicle.Position) + Rand.Range(0f, 15f);
                candidates.Add(new KeyValuePair<IntVec3, float>(cell, score));
            }

            candidates.Sort((a, b) => a.Value.CompareTo(b.Value));

            int pathChecks = 0;
            foreach (var kvp in candidates)
            {
                if (pathChecks++ >= 5) break;
                if (vehicle.CanReachVehicle(new LocalTargetInfo(kvp.Key), PathEndMode.OnCell, Danger.Deadly, TraverseMode.NoPassClosedDoors))
                {
                    return kvp.Key;
                }
            }

            return IntVec3.Invalid;
        }

        private Thing FindNearestEnemy(VehiclePawn vehicle)
        {
            var hostileTargets = vehicle.Map.attackTargetsCache.TargetsHostileToFaction(vehicle.Faction);
            if (hostileTargets == null || hostileTargets.Count == 0) return null;

            Thing bestTarget = null;
            float bestDistSq = float.MaxValue;

            foreach (var target in hostileTargets)
            {
                Thing t = target.Thing;
                if (t == null || t.Destroyed || t.Map == null || t.Map.fogGrid.IsFogged(t.Position)) continue;

                if (t is Pawn p && (p.Dead || p.Downed)) continue;

                float distSq = t.Position.DistanceToSquared(vehicle.Position);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestTarget = t;
                }
            }

            return bestTarget;
        }

        private Thing FindNearestWallTowardEnemy(VehiclePawn vehicle, Thing enemy)
        {
            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(vehicle.Position, enemy.Position))
            {
                Building edifice = cell.GetEdifice(vehicle.Map);
                if (edifice != null && !edifice.Destroyed && edifice.def.useHitPoints && (edifice.def.fillPercent > 0.5f || edifice is Building_Door)) return edifice;
            }
            return null;
        }

        private bool IsBlockedByFriendlyVehicle(VehiclePawn vehicle, Thing enemy, out VehiclePawn blocker)
        {
            blocker = null;
            if (vehicle.Map == null || enemy == null) return false;

            Vector3 dir = (enemy.Position.ToVector3Shifted() - vehicle.Position.ToVector3Shifted()).normalized;
            
            for (int dist = 1; dist <= 4; dist++)
            {
                IntVec3 checkCenter = vehicle.Position + (dir * dist).ToIntVec3();
                CellRect checkRect = CellRect.CenteredOn(checkCenter, vehicle.def.size.x, vehicle.def.size.z);
                
                foreach (IntVec3 cell in checkRect)
                {
                    if (!cell.InBounds(vehicle.Map)) continue;
                    
                    VehiclePawn other = PathingHelper.AnyVehicleBlockingPathAt(cell, vehicle);
                    if (other != null && other != vehicle && other.Faction == vehicle.Faction)
                    {
                        blocker = other;
                        return true;
                    }
                }
            }
            return false;
        }
    }

    public class VRF_JobGiver_VehicleExitMap : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn is VehiclePawn vehicle) || !vehicle.Spawned || vehicle.Map == null) return null;

            if (!CrewManager.CanMove(vehicle))
            {
                return JobMaker.MakeJob(JobDefOf.Wait_Combat, 2000, true);
            }

            if (CrewManager.IsAnyPawnBoarding(vehicle) && !CrewManager.HasOperationalDriver(vehicle))
            {
                if (vehicle.CurJob != null && vehicle.CurJob.def == JobDefOf.Wait_Combat) return null;
                return JobMaker.MakeJob(JobDefOf.Wait_Combat, 500, true);
            }

            IntVec3 exitCell;
            if (VehicleTrafficManager.TryFindExitCell(vehicle, out exitCell))
            {
                if (!vehicle.CanReachVehicle(new LocalTargetInfo(exitCell), PathEndMode.OnCell, Danger.Deadly, TraverseMode.NoPassClosedDoors))
                {
                    return JobMaker.MakeJob(JobDefOf.Wait_Combat, 300, true);
                }
                return CreateExitJob(exitCell);
            }
            
            return JobMaker.MakeJob(JobDefOf.Wait_Combat, 300, true);
        }

        private Job CreateExitJob(IntVec3 target)
        {
            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("VRF_VehicleExitMap"), target);
            job.exitMapOnArrival = true;
            job.locomotionUrgency = LocomotionUrgency.Jog;
            return job;
        }
    }

    
    
    
    public class JobDriver_VehicleExitMap : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) 
        {
            this.pawn.Map.pawnDestinationReservationManager.Reserve(this.pawn, this.job, this.job.targetA.Cell);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil gotoToil = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            gotoToil.tickAction = () => { if (pawn is VehiclePawn v && v.Position.DistanceToSquared(TargetA.Cell) <= 4) PathingHelper.ExitMapForVehicle(v, job); };
            yield return gotoToil;

            Toil forceExit = ToilMaker.MakeToil();
            forceExit.initAction = () => { if (pawn is VehiclePawn v) PathingHelper.ExitMapForVehicle(v, job); };
            yield return forceExit;
        }
    }
}
