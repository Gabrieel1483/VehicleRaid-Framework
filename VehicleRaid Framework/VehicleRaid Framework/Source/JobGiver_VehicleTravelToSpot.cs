using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;
using Vehicles;

namespace VehicleRaidFramework
{
    public class JobGiver_VehicleTravelToSpot : ThinkNode_JobGiver
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn is VehiclePawn vehicle)) return null;
            if (!vehicle.Spawned || vehicle.Map == null) return null;

            
            if (vehicle.mindState?.duty == null) return null;
            IntVec3 dest = vehicle.mindState.duty.focus.Cell;
            if (!dest.IsValid) return null;

            
            if (vehicle.Position.InHorDistOf(dest, 10f))
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 60);
            }

            
            if (vehicle.CurJobDef == JobDefOf.Goto && vehicle.pather != null && vehicle.pather.Moving)
            {
                return null;
            }

            
            Patch_VehicleNPCOnOff.UpdateVehiclePower(vehicle);

            
            IntVec3 fixedSpot = VehicleRaidUtility.FixDestination(vehicle, dest);
            if (vehicle.CanReachVehicle(new LocalTargetInfo(fixedSpot), PathEndMode.OnCell, Danger.Deadly, TraverseMode.NoPassClosedDoors))
            {
                Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, fixedSpot);
                gotoJob.locomotionUrgency = LocomotionUrgency.Walk;
                gotoJob.expiryInterval = 5000;
                gotoJob.checkOverrideOnExpire = true;
                return gotoJob;
            }

            
            IntVec3 fallback = CellFinder.RandomClosewalkCellNear(dest, vehicle.Map, 15);
            if (fallback.IsValid && vehicle.CanReachVehicle(new LocalTargetInfo(fallback), PathEndMode.OnCell, Danger.Deadly, TraverseMode.NoPassClosedDoors))
            {
                Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, fallback);
                gotoJob.locomotionUrgency = LocomotionUrgency.Walk;
                gotoJob.expiryInterval = 5000;
                return gotoJob;
            }
return JobMaker.MakeJob(JobDefOf.Wait, 120);
        }
    }
}


