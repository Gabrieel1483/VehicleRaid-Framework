using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;
using Vehicles;

namespace VehicleRaidFramework
{
    [HarmonyPatch(typeof(TraderCaravanUtility), nameof(TraderCaravanUtility.GetTraderCaravanRole))]
    public static class Patch_TraderCaravanRole
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn p, ref TraderCaravanRole __result)
        {
            if (p is VehiclePawn v)
            {
                Lord lord = p.GetLord();
                if (lord != null)
                {
                    if (lord.LordJob is LordJob_VehicleTrade tradeJob)
                    {
                        __result = tradeJob.tradeableVehicles.Contains(v) ? TraderCaravanRole.Carrier : TraderCaravanRole.Guard;
                    }
                    else if (lord.LordJob is LordJob_HelicopterTrade heliJob)
                    {
                        __result = heliJob.tradeVehicles.Contains(v) ? TraderCaravanRole.Carrier : TraderCaravanRole.Guard;
                    }
                    else
                    {
                        __result = TraderCaravanRole.Carrier;
                    }
                }
                else
                {
                    __result = TraderCaravanRole.Carrier;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_TraderTracker), "get_Goods")]
    public static class Patch_VehicleTrader_Inventory
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_TraderTracker __instance, ref IEnumerable<Thing> __result)
        {
            var originalResult = __result;
            __result = FilterGoods(__instance, originalResult);
        }

        private static IEnumerable<Thing> FilterGoods(Pawn_TraderTracker tracker, IEnumerable<Thing> originalGoods)
        {
            foreach (Thing thing in originalGoods)
            {
                Pawn owner = GetPawnContaining(thing);
                if (owner != null && owner.inventory != null && owner.inventory.NotForSale(thing))
                {
                    continue; 
                }

                if (owner is VehiclePawn v)
                {
                    Lord lord = v.GetLord();
                    if (lord != null)
                    {
                        if (lord.LordJob is LordJob_VehicleTrade tradeJob)
                        {
                            if (!tradeJob.tradeableVehicles.Contains(v)) continue;
                        }
                        else if (lord.LordJob is LordJob_HelicopterTrade heliJob)
                        {
                            if (!heliJob.tradeVehicles.Contains(v)) continue;
                        }
                    }
                }
                
                yield return thing;
            }
        }

        private static Pawn GetPawnContaining(Thing t)
        {
            IThingHolder holder = t.ParentHolder;
            while (holder != null)
            {
                if (holder is Pawn p) return p;
                holder = holder.ParentHolder;
            }
            return null;
        }
    }
}


