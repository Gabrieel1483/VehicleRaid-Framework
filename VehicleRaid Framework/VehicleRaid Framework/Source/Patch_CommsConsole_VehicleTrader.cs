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
                opt.Disable("WaitTime".Translate(ticksLeft.ToStringTicksToPeriod()));
                return opt;
            }

            if (!faction.def.allowedArrivalTemperatureRange.ExpandedBy(-4f).Includes(map.mapTemperature.SeasonalTemp))
            {
                opt.Disable("BadTemperature".Translate());
                return opt;
            }

            DiaNode confirmNode = new DiaNode("VRF_ChooseTraderType".Translate());

            var heliExt = incident.GetModExtension<HelicopterIncidentExtension>();
            if (heliExt?.traderKind != null)
            {
                TraderKindDef tk = heliExt.traderKind;
                DiaOption choice = new DiaOption(tk.LabelCap.NullOrEmpty() ? tk.defName : tk.LabelCap);

                if (tk.TitleRequiredToTrade != null &&
                    (negotiator.royalty == null ||
                     tk.TitleRequiredToTrade.seniority > negotiator.GetCurrentTitleSeniorityIn(faction)))
                {
                    DiaNode deniedNode = new DiaNode("TradeCaravanRequestDeniedDueTitle".Translate(
                        negotiator.Named("NEGOTIATOR"),
                        tk.TitleRequiredToTrade.GetLabelCapFor(negotiator).Named("TITLE"),
                        faction.Named("FACTION")));
                    DiaOption goBack = new DiaOption("GoBack".Translate());
                    deniedNode.options.Add(goBack);
                    choice.link = deniedNode;
                    goBack.link = confirmNode;
                }
                else if (tk.permitRequiredForTrading != null &&
                         !map.mapPawns.FreeColonists.Any(p =>
                             p.royalty != null && p.royalty.HasPermit(tk.permitRequiredForTrading, faction)))
                {
                    choice.Disable("TradeCaravanRequestDeniedDueTitle".Translate(
                        negotiator.Named("NEGOTIATOR"),
                        tk.permitRequiredForTrading.label.Named("TITLE"),
                        faction.Named("FACTION")));
                }
                else
                {
                    choice.action = () =>
                    {
                        IncidentParms parms = new IncidentParms { target = map, faction = faction, traderKind = tk, forced = true };
                        Find.Storyteller.incidentQueue.Add(incident, Find.TickManager.TicksGame + delayTicks, parms, 240000);
                        faction.lastTraderRequestTick = Find.TickManager.TicksGame;
                        Faction.OfPlayer.TryAffectGoodwillWith(faction, -cost, reason: HistoryEventDefOf.RequestedTrader);
                    };
                    choice.resolveTree = true;
                }
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
                        TraderKindDef tk = mapper.traderKind;
                        DiaOption choice = new DiaOption(tk.LabelCap);

                        if (tk.TitleRequiredToTrade != null &&
                            (negotiator.royalty == null ||
                             tk.TitleRequiredToTrade.seniority > negotiator.GetCurrentTitleSeniorityIn(faction)))
                        {
                            DiaNode deniedNode = new DiaNode("TradeCaravanRequestDeniedDueTitle".Translate(
                                negotiator.Named("NEGOTIATOR"),
                                tk.TitleRequiredToTrade.GetLabelCapFor(negotiator).Named("TITLE"),
                                faction.Named("FACTION")));
                            DiaOption goBack = new DiaOption("GoBack".Translate());
                            deniedNode.options.Add(goBack);
                            choice.link = deniedNode;
                            goBack.link = confirmNode;
                        }
                        else if (tk.permitRequiredForTrading != null &&
                                 !map.mapPawns.FreeColonists.Any(p =>
                                     p.royalty != null && p.royalty.HasPermit(tk.permitRequiredForTrading, faction)))
                        {
                            choice.Disable("TradeCaravanRequestDeniedDueTitle".Translate(
                                negotiator.Named("NEGOTIATOR"),
                                tk.permitRequiredForTrading.label.Named("TITLE"),
                                faction.Named("FACTION")));
                        }
                        else
                        {
                            TraderKindDef captured = tk;
                            choice.action = () =>
                            {
                                IncidentParms parms = new IncidentParms { target = map, faction = faction, traderKind = captured, forced = true };
                                Find.Storyteller.incidentQueue.Add(incident, Find.TickManager.TicksGame + delayTicks, parms, 240000);
                                faction.lastTraderRequestTick = Find.TickManager.TicksGame;
                                Faction.OfPlayer.TryAffectGoodwillWith(faction, -cost, reason: HistoryEventDefOf.RequestedTrader);
                            };
                            choice.resolveTree = true;
                        }
                        confirmNode.options.Add(choice);
                    }
                }
            }

            if (confirmNode.options.Count == 0) return null;

            confirmNode.options.Add(new DiaOption("GoBack".Translate()) { linkLateBind = () => FactionDialogMaker.FactionDialogFor(negotiator, faction) });
            opt.link = confirmNode;
            return opt;
        }
    }
}


