using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace VehicleRaidFramework
{
    [HarmonyPatch(typeof(FactionDialogMaker), "FactionDialogFor")]
    public static class Patch_CommsConsole_VehicleTrader
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn negotiator, Faction faction, ref DiaNode __result)
        {
            if (negotiator.Map == null || !negotiator.Map.IsPlayerHome) return;
            if (faction.PlayerRelationKind != FactionRelationKind.Ally) return;
            
            foreach (IncidentDef incident in DefDatabase<IncidentDef>.AllDefs)
            {
                HelicopterIncidentExtension heliExt = incident.GetModExtension<HelicopterIncidentExtension>();
                if (heliExt != null)
                {
                    if (heliExt.factionDef == null || faction.def == heliExt.factionDef)
                    {
                        DiaOption heliOpt = RequestTraderOption(negotiator.Map, faction, negotiator, incident, "VRF_RequestAerialTrader".Translate(30), 30, 42000);
                        if (heliOpt != null)
                        {
                            __result.options.Insert(0, heliOpt);
                        }
                    }
                }
            }

            VehicleMerchantExtension ext = faction.def.GetModExtension<VehicleMerchantExtension>();
            if (ext != null)
            {
                IncidentDef landIncident = DefDatabase<IncidentDef>.GetNamed("VRF_VehicleTraderArrival", false);
                if (landIncident != null)
                {
                    DiaOption landOpt = RequestTraderOption(negotiator.Map, faction, negotiator, landIncident, "VRF_RequestArmoredTrader".Translate(35), 35, 48000);
                    if (landOpt != null)
                    {
                        __result.options.Insert(0, landOpt);
                    }
                }
            }
        }

        private static DiaOption RequestTraderOption(Map map, Faction faction, Pawn negotiator, IncidentDef incident, string label, int cost, int delayTicks)
        {
            DiaOption opt = new DiaOption(label);

            int ticksLeft = faction.lastTraderRequestTick + 240000 - Find.TickManager.TicksGame;
            if (ticksLeft > 0)
            {
                opt.Disable("Wait time: " + ticksLeft.ToStringTicksToPeriod());
                return opt;
            }

            if (!faction.def.allowedArrivalTemperatureRange.ExpandedBy(-4f).Includes(map.mapTemperature.SeasonalTemp))
            {
                opt.Disable("Bad temperature");
                return opt;
            }

            DiaNode confirmNode = new DiaNode("VRF_ChooseTraderType".Translate());

            var heliExt = incident.GetModExtension<HelicopterIncidentExtension>();
            if (heliExt?.traderKind != null)
            {
                string choiceLabel = heliExt.traderKind.LabelCap;
                if (choiceLabel.NullOrEmpty()) choiceLabel = heliExt.traderKind.defName;

                DiaOption choice = new DiaOption(choiceLabel);
                choice.action = () =>
                {
                    IncidentParms parms = new IncidentParms
                    {
                        target = map,
                        faction = faction,
                        traderKind = heliExt.traderKind,
                        forced = true
                    };
                    Find.Storyteller.incidentQueue.Add(incident, Find.TickManager.TicksGame + delayTicks, parms, 240000);
                    faction.lastTraderRequestTick = Find.TickManager.TicksGame;
                    Faction.OfPlayer.TryAffectGoodwillWith(faction, -cost, reason: HistoryEventDefOf.RequestedTrader);
                };
                choice.resolveTree = true;
                confirmNode.options.Add(choice);
            }
            else
            {
                VehicleMerchantExtension ext = faction.def.GetModExtension<VehicleMerchantExtension>();
                if (ext != null)
                {
                    foreach (var mapper in ext.traderMappers)
                    {
                        if (!mapper.traderKind.requestable) continue;

                        DiaOption choice = new DiaOption(mapper.traderKind.LabelCap);
                        choice.action = () =>
                        {
                            IncidentParms parms = new IncidentParms
                            {
                                target = map,
                                faction = faction,
                                traderKind = mapper.traderKind,
                                forced = true
                            };
                            Find.Storyteller.incidentQueue.Add(incident, Find.TickManager.TicksGame + delayTicks, parms, 240000);
                            faction.lastTraderRequestTick = Find.TickManager.TicksGame;
                            Faction.OfPlayer.TryAffectGoodwillWith(faction, -cost, reason: HistoryEventDefOf.RequestedTrader);
                        };
                        choice.resolveTree = true;
                        confirmNode.options.Add(choice);
                    }
                }
            }

            if (confirmNode.options.Count == 0) return null;

            confirmNode.options.Add(new DiaOption("Go back") { linkLateBind = () => FactionDialogMaker.FactionDialogFor(negotiator, faction) });
            opt.link = confirmNode;

            return opt;
        }
    }
}


