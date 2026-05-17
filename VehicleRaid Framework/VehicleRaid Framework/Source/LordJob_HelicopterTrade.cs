using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;
using RimWorld.Planet;
using Vehicles;

namespace VehicleRaidFramework
{
    public class LordJob_HelicopterTrade : LordJob
    {
        private Faction faction;
        private IntVec3 landingSpot;
        public List<VehiclePawn> tradeVehicles = new List<VehiclePawn>();

        public LordJob_HelicopterTrade() { }

        public LordJob_HelicopterTrade(Faction faction, IntVec3 landingSpot, List<VehiclePawn> tradeVehicles)
        {
            this.faction = faction;
            this.landingSpot = landingSpot;
            this.tradeVehicles = tradeVehicles;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();

            LordToil_VehicleDefendTraderCaravan defendTrade = new LordToil_VehicleDefendTraderCaravan(landingSpot);
            graph.StartingToil = defendTrade;

            LordToil_HelicopterExit exit = new LordToil_HelicopterExit();
            graph.AddToil(exit);

            Transition leaveTime = new Transition(defendTrade, exit);
            leaveTime.AddTrigger(new Trigger_TicksPassed(Rand.Range(27000, 45000)));
            leaveTime.AddPreAction(new TransitionAction_Message("MessageTraderCaravanLeaving".Translate(faction.Name)));
            leaveTime.AddPreAction(new TransitionAction_Custom(() =>
            {
                foreach (Pawn p in lord.ownedPawns)
                {
                    if (!(p is VehiclePawn) && p.Spawned)
                    {
                        foreach (Pawn vPawn in lord.ownedPawns)
                        {
                            if (vPawn is VehiclePawn v && v.handlers.Any(h => h.AreSlotsAvailable))
                            {
                                v.TryAddPawn(p);
                                break;
                            }
                        }
                    }
                }
            }));
            graph.AddTransition(leaveTime);

            Transition harmed = new Transition(defendTrade, exit);
            harmed.AddTrigger(new Trigger_PawnHarmed());
            harmed.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(harmed);

            return graph;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref landingSpot, "landingSpot");
            Scribe_Collections.Look(ref tradeVehicles, "tradeVehicles", LookMode.Reference);
        }
    }

    public class LordToil_HelicopterExit : LordToil
    {
        public override void UpdateAllDuties()
        {
            foreach (Pawn p in lord.ownedPawns)
            {
                if (p is VehiclePawn v)
                {
                    p.mindState.duty = new PawnDuty(DefDatabase<DutyDef>.GetNamed("VRF_HelicopterTakeoff"), v.Position);
                }
                else
                {
                    p.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBest);
                }
            }
        }

        public override void LordToilTick()
        {
            if (Find.TickManager.TicksGame % 60 != 0) return;

            foreach (Pawn p in lord.ownedPawns)
            {
                if (p is VehiclePawn v && v.Spawned)
                {
                    bool allAboard = !lord.ownedPawns.Any(other => !(other is VehiclePawn) && other.Spawned);

                    if (allAboard)
                    {
                        CompVehicleLauncher launcher = v.CompVehicleLauncher;
                        if (launcher != null && !launcher.inFlight)
                        {
                            launcher.inFlight = true;
                            launcher.launchProtocol.OrderProtocol(LaunchProtocol.LaunchType.Takeoff);
                            
                            VehicleSkyfaller_Leaving skyfaller = (VehicleSkyfaller_Leaving)VehicleSkyfallerMaker.MakeSkyfaller(launcher.Props.skyfallerLeaving, v);
                            skyfaller.createWorldObject = false;
                            
                            GenSpawn.Spawn(skyfaller, v.Position, v.Map);
                        }
                    }
                }
            }
        }
    }
}
