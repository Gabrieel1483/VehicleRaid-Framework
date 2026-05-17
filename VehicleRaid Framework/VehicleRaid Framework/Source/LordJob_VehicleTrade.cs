using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;
using Vehicles;

namespace VehicleRaidFramework
{
    public class LordJob_VehicleTrade : LordJob
    {
        private Faction faction;
        private IntVec3 chillSpot;
        public List<VehiclePawn> tradeableVehicles = new List<VehiclePawn>();

        public IntVec3 ChillSpot => chillSpot;

        public LordJob_VehicleTrade() { }

        public LordJob_VehicleTrade(Faction faction, IntVec3 chillSpot, List<VehiclePawn> tradeable = null)
        {
            this.faction = faction;
            this.chillSpot = chillSpot;
            if (tradeable != null) this.tradeableVehicles.AddRange(tradeable);
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();

            
            LordToil_VehicleTravelToSpot travel = new LordToil_VehicleTravelToSpot(chillSpot);
            graph.StartingToil = travel;

            LordToil_VehicleDefendTraderCaravan defendTrade = new LordToil_VehicleDefendTraderCaravan(chillSpot);
            graph.AddToil(defendTrade);

            LordToil_VehicleExitMap exit = new LordToil_VehicleExitMap();
            graph.AddToil(exit);

            
            Transition arrive = new Transition(travel, defendTrade);
            arrive.AddTrigger(new Trigger_Memo("TravelArrived"));
            graph.AddTransition(arrive);

            
            Transition leaveTime = new Transition(defendTrade, exit);
            leaveTime.AddTrigger(new Trigger_TicksPassed(Rand.Range(27000, 45000)));
            leaveTime.AddPreAction(new TransitionAction_Message("MessageTraderCaravanLeaving".Translate(faction.Name)));
            leaveTime.AddPreAction(new TransitionAction_Custom(() =>
            {
                foreach (Pawn occupant in lord.ownedPawns)
                {
                    if (!(occupant is VehiclePawn) && occupant.Spawned)
                    {
                        
                        foreach (Pawn vPawn in lord.ownedPawns)
                        {
                            if (vPawn is VehiclePawn v && v.handlers.Any(h => h.AreSlotsAvailable))
                            {
                                
                                v.TryAddPawn(occupant);
                                break;
                            }
                        }
                    }
                }
            }));
            graph.AddTransition(leaveTime);

            
            Transition harmed = new Transition(travel, exit);
            harmed.AddSource(defendTrade);
            harmed.AddTrigger(new Trigger_PawnHarmed());
            harmed.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(harmed);

            return graph;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref chillSpot, "chillSpot");
            Scribe_Collections.Look(ref tradeableVehicles, "tradeableVehicles", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (tradeableVehicles == null) tradeableVehicles = new List<VehiclePawn>();
            }
        }
    }
    public class LordToil_VehicleTravelToSpot : LordToil
    {
        private IntVec3 destination;
        private const int TravelSpacing = 4;

        public new IntVec3 FlagLoc => destination;

        public override bool AllowSatisfyLongNeeds => false;

        public LordToil_VehicleTravelToSpot(IntVec3 dest)
        {
            this.destination = dest;
        }

        public override void UpdateAllDuties()
        {
            
            int vehicleIndex = 0;
            DutyDef vehicleTravelDuty = DefDatabase<DutyDef>.GetNamed("VRF_VehicleTravelToSpot", false);

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (pawn is VehiclePawn vehicle)
                {
                    
                    int maxDim = UnityEngine.Mathf.Max(vehicle.def.size.x, vehicle.def.size.z);
                    int spacing = maxDim + TravelSpacing;
                    int col = (vehicleIndex % 2 == 0) ? -1 : 1;
                    int row = vehicleIndex / 2;
                    IntVec3 offset = new IntVec3(col * (spacing / 2 + 1), 0, -row * spacing);
                    IntVec3 myDest = destination + offset;
                    vehicleIndex++;

                    if (vehicleTravelDuty != null)
                    {
                        pawn.mindState.duty = new PawnDuty(vehicleTravelDuty, (LocalTargetInfo)myDest);
                    }
                    else
                    {
                        pawn.mindState.duty = new PawnDuty(DutyDefOf.TravelOrLeave, (LocalTargetInfo)myDest);
                    }

                    Patch_VehicleNPCOnOff.UpdateVehiclePower(vehicle);
                }
                else
                {
                    
                    
                    if (pawn.Spawned)
                    {
                        pawn.mindState.duty = new PawnDuty(DutyDefOf.TravelOrLeave, (LocalTargetInfo)destination);
                    }
                }
            }
        }

        public override void LordToilTick()
        {
            if (Find.TickManager.TicksGame % 120 != 0) return;

            bool anyVehicle = false;
            bool nonTraderVehiclesArrived = true;
            bool traderVehicleArrived = false;

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (pawn is Vehicles.VehiclePawn v)
                {
                    anyVehicle = true;
                    
                    bool hasTrader = false;
                    foreach (Pawn occupant in v.AllPawnsAboard)
                    {
                        if (occupant.mindState?.wantsToTradeWithColony == true)
                        {
                            hasTrader = true;
                            break;
                        }
                    }
                    
                    if (hasTrader)
                    {
                        if (pawn.Position.InHorDistOf(destination, 20f))
                        {
                            traderVehicleArrived = true;
                        }
                    }
                    else
                    {
                        if (!pawn.Position.InHorDistOf(destination, 20f))
                        {
                            nonTraderVehiclesArrived = false;
                        }
                    }
                }
            }

            
            if (anyVehicle && (traderVehicleArrived || nonTraderVehiclesArrived))
            {
lord.ReceiveMemo("TravelArrived");
            }
        }
    }
}


