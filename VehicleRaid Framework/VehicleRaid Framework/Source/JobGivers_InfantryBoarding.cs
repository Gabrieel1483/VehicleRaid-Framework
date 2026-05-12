using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Vehicles;
using SmashTools;

namespace VehicleRaidFramework
{
    public class JobGiver_InfantryBoardNearbyVehicle : ThinkNode_JobGiver
    {
        private float searchRadius = 80f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Faction == null || pawn.Map == null || pawn.Downed || pawn.Dead) return null;
            if (pawn.CurJob != null && pawn.CurJob.def.defName == "Board") return null;

            var vehicles = pawn.Map.mapPawns.AllPawnsSpawned
                .OfType<VehiclePawn>()
                .Where(v => v.Faction == pawn.Faction && !v.Dead && v.Spawned && v.CanMove && v.handlers.Any(h => h.AreSlotsAvailable))
                .OrderBy(v => v.Position.DistanceToSquared(pawn.Position));

            foreach (var vehicle in vehicles)
            {
                if (pawn.Position.DistanceTo(vehicle.Position) > searchRadius) continue;

                VehicleRoleHandler handler = GetBestAvailableHandler(vehicle, pawn);

                if (handler != null && pawn.CanReach(vehicle, PathEndMode.Touch, Danger.Deadly))
                {
                    JobDef boardJobDef = DefDatabase<JobDef>.GetNamed("Board", false);
                    if (boardJobDef != null)
                    {
                        vehicle.GiveLoadJob(pawn, handler);

                        Job job = JobMaker.MakeJob(boardJobDef, vehicle);
                        job.expiryInterval = 3000;
                        job.locomotionUrgency = pawn.Position.DistanceToSquared(vehicle.Position) > 100f
                            ? LocomotionUrgency.Sprint
                            : LocomotionUrgency.Jog;
                        return job;
                    }
                }
            }

            return null;
        }

        private VehicleRoleHandler GetBestAvailableHandler(VehiclePawn vehicle, Pawn pawn)
        {
            var move = vehicle.GetNextAvailableHandler(pawn, HandlingType.Movement);
            if (move != null && move.AreSlotsAvailable) return move;

            var turret = vehicle.GetNextAvailableHandler(pawn, HandlingType.Turret);
            if (turret != null && turret.AreSlotsAvailable) return turret;

            var any = vehicle.GetNextAvailableHandler(pawn, HandlingType.None);
            if (any != null && any.AreSlotsAvailable) return any;

            return null;
        }
    }

    public class JobGiver_FollowVehicle : ThinkNode_JobGiver
    {
        private float followRadius = 100f;
        private float minDistance = 10f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Faction == null || pawn.Map == null || pawn.Downed || pawn.Dead) return null;

            VehiclePawn closest = null;
            float closestDist = float.MaxValue;

            foreach (Pawn p in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (p is VehiclePawn v && v.Faction == pawn.Faction && !v.Dead && v.Spawned && CrewManager.HasOperationalDriver(v))
                {
                    float dist = v.Position.DistanceToSquared(pawn.Position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = v;
                    }
                }
            }

            if (closest == null) return null;
            if (closestDist > followRadius * followRadius) return null;
            if (closestDist < minDistance * minDistance) return null;

            if (!pawn.CanReach(closest, PathEndMode.Touch, Danger.Deadly)) return null;

            Job job = JobMaker.MakeJob(JobDefOf.Goto, closest.Position);
            job.expiryInterval = 500;
            job.checkOverrideOnExpire = true;
            job.locomotionUrgency = LocomotionUrgency.Jog;
            return job;
        }
    }
}
