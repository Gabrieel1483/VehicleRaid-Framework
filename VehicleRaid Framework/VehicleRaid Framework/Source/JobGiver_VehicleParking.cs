using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;
using Vehicles;

namespace VehicleRaidFramework
{
    public class JobGiver_VehicleParking : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn is VehiclePawn vehicle)) return null;
            if (!vehicle.Spawned || vehicle.Map == null) return null;

            
            if (vehicle.mindState?.duty == null || !vehicle.mindState.duty.focus.IsValid)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 200);
            }

            IntVec3 mySpot = vehicle.mindState.duty.focus.Cell;

            
            if (vehicle.Position.InHorDistOf(mySpot, 5f))
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 300);
            }

            
            if (vehicle.CurJobDef == JobDefOf.Goto && vehicle.pather != null && vehicle.pather.Moving)
            {
                return null;
            }

            
            Patch_VehicleNPCOnOff.UpdateVehiclePower(vehicle);

            
            IntVec3 fixedSpot = VehicleRaidUtility.FixDestination(vehicle, mySpot);
            if (vehicle.CanReachVehicle(new LocalTargetInfo(fixedSpot), PathEndMode.OnCell, Danger.Deadly, TraverseMode.NoPassClosedDoors))
            {
                Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, fixedSpot);
                gotoJob.locomotionUrgency = LocomotionUrgency.Walk;
                gotoJob.expiryInterval = 5000;
                return gotoJob;
            }

            
            IntVec3 fallback = CellFinder.RandomClosewalkCellNear(mySpot, vehicle.Map, 10);
            if (fallback.IsValid && vehicle.CanReachVehicle(new LocalTargetInfo(fallback), PathEndMode.OnCell, Danger.Deadly, TraverseMode.NoPassClosedDoors))
            {
                Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, fallback);
                gotoJob.locomotionUrgency = LocomotionUrgency.Walk;
                gotoJob.expiryInterval = 5000;
                return gotoJob;
            }

            return JobMaker.MakeJob(JobDefOf.Wait, 200);
        }
    }
}


