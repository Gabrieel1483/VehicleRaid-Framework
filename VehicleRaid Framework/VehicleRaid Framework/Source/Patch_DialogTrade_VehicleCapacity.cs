using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Vehicles;
using System.Collections.Generic;
using System.Linq;

namespace VehicleRaidFramework
{
    [HarmonyPatch(typeof(Dialog_Trade), "DoWindowContents")]
    public static class Patch_DialogTrade_WeightLimit
    {
        private static readonly Color LowCapacityColor = Color.yellow;
        private static readonly Color OverCapacityColor = Color.red;

        [HarmonyPostfix]
        public static void Postfix(Rect inRect)
        {
            List<VehiclePawn> vehicles = GetNPCTradeVehicles();
            if (vehicles == null || vehicles.Count == 0) return;

            float maxCap = 0;
            foreach (var v in vehicles)
            {
                float cap = 0;
                if (v.def is VehicleDef vDef)
                {
                    var vStats = Traverse.Create(vDef).Field("vehicleStats").GetValue() as System.Collections.IEnumerable;
                    if (vStats != null)
                    {
                        foreach (var mod in vStats)
                        {
                            var travMod = Traverse.Create(mod);
                            var statObj = travMod.Field("stat").GetValue() ?? travMod.Field("statDef").GetValue();
                            if (statObj != null)
                            {
                                string defName = Traverse.Create(statObj).Field("defName").GetValue<string>();
                                if (defName == "CargoCapacity")
                                {
                                    cap = travMod.Field("value").GetValue<float>();
                                    break;
                                }
                            }
                        }
                    }
                }

                if (cap <= 0)
                {
                    var comp = v.AllComps.FirstOrDefault(c => c.GetType().Name.Contains("VehicleInventory"));
                    if (comp != null)
                    {
                        var prop = comp.GetType().GetProperty("MaxCapacity", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                ?? comp.GetType().GetProperty("Capacity", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (prop != null)
                        {
                            var val = prop.GetValue(comp);
                            if (val is float f) cap = f;
                            else if (val is int i) cap = i;
                        }
                    }
                }
                
                if (cap <= 0)
                {
                    StatDef cargoStat = DefDatabase<StatDef>.GetNamed("CargoCapacity", false) 
                                     ?? DefDatabase<StatDef>.GetNamed("Vehicle_CargoCapacity", false);
                    cap = v.GetStatValue(cargoStat ?? StatDefOf.CarryingCapacity);
                }
                maxCap += cap;
            }

            float curWeight = 0;
            foreach (var v in vehicles)
            {
                curWeight += v.inventory.innerContainer.Sum(t => t.stackCount * t.def.BaseMass);
            }

            float tradeDelta = 0;
            if (TradeSession.deal != null)
            {
                foreach (Tradeable t in TradeSession.deal.AllTradeables)
                {
                    if (t.CountToTransfer != 0)
                    {
                        tradeDelta += -t.CountToTransfer * t.AnyThing.def.BaseMass;
                    }
                }
            }

            float finalWeight = curWeight + tradeDelta;

            float anchorX = inRect.width - 180f;
            Rect weightRect = new Rect(anchorX - 100f, 60f, 200f, 30f);
            Text.Anchor = TextAnchor.UpperCenter;
            Text.Font = GameFont.Small;
            GUI.color = (finalWeight > maxCap) ? OverCapacityColor : (finalWeight > maxCap * 0.9f) ? LowCapacityColor : Color.white;
            
            string label = $"{finalWeight.ToString("F1")} / {maxCap.ToString("F1")} kg";
            Widgets.Label(weightRect, label);
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public static List<VehiclePawn> GetNPCTradeVehicles()
        {
            if (TradeSession.trader == null) return null;
            if (TradeSession.trader is Pawn traderPawn)
            {
                Lord lord = traderPawn.GetLord();
                if (lord == null) return null;
                
                if (lord.LordJob is LordJob_VehicleTrade land) return land.tradeableVehicles;
                if (lord.LordJob is LordJob_HelicopterTrade heli) return heli.tradeVehicles;
            }
            
            return null;
        }
    }

    [HarmonyPatch(typeof(TradeDeal), "TryExecute")]
    public static class Patch_TradeDeal_TryExecuteWeight
    {
        [HarmonyPrefix]
        public static bool Prefix(out bool actuallyTraded, ref bool __result)
        {
            actuallyTraded = false;

            List<VehiclePawn> vehicles = Patch_DialogTrade_WeightLimit.GetNPCTradeVehicles();
            if (vehicles == null || vehicles.Count == 0) return true;

            float maxCap = 0;
            foreach (var v in vehicles)
            {
                float cap = 0;
                if (v.def is VehicleDef vDef)
                {
                    var vStats = Traverse.Create(vDef).Field("vehicleStats").GetValue() as System.Collections.IEnumerable;
                    if (vStats != null)
                    {
                        foreach (var mod in vStats)
                        {
                            var travMod = Traverse.Create(mod);
                            var statObj = travMod.Field("stat").GetValue() ?? travMod.Field("statDef").GetValue();
                            if (statObj != null)
                            {
                                string defName = Traverse.Create(statObj).Field("defName").GetValue<string>();
                                if (defName == "CargoCapacity")
                                {
                                    cap = travMod.Field("value").GetValue<float>();
                                    break;
                                }
                            }
                        }
                    }
                }

                if (cap <= 0)
                {
                    var comp = v.AllComps.FirstOrDefault(c => c.GetType().Name.Contains("VehicleInventory"));
                    if (comp != null)
                    {
                        var prop = comp.GetType().GetProperty("MaxCapacity", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (prop != null) cap = (float)prop.GetValue(comp);
                    }
                }
                maxCap += cap;
            }

            float curWeight = vehicles.Sum(v => v.inventory.innerContainer.Sum(t => t.stackCount * t.def.BaseMass));
            
            float tradeDelta = 0;
            if (TradeSession.deal != null)
            {
                foreach (Tradeable t in TradeSession.deal.AllTradeables)
                {
                    tradeDelta += -t.CountToTransfer * t.AnyThing.def.BaseMass;
                }
            }

            if (curWeight + tradeDelta > maxCap)
            {
                Messages.Message("VRF_TraderOverweight".Translate(), MessageTypeDefOf.RejectInput, false);
                __result = false;
                return false;
            }

            return true;
        }
    }
}
