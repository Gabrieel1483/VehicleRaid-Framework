using System;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;
using HarmonyLib;
using Vehicles;

namespace VehicleRaidFramework
{
    [HarmonyPatch(typeof(LordToil_DefendBase), "UpdateAllDuties")]
    public static class Patch_LordToil_DefendBase_UpdateAllDuties
    {
        [HarmonyPostfix]
        public static void Postfix(LordToil_DefendBase __instance)
        {
            if (__instance.lord == null || __instance.lord.ownedPawns == null) return;

            foreach (Pawn pawn in __instance.lord.ownedPawns)
            {
                if (pawn is VehiclePawn)
                {
                    DutyDef vehicleDutyDef = VRF_DutyDefOf.VRF_VehicleDefendBase;
                    if (vehicleDutyDef != null)
                    {
                        pawn.mindState.duty = new PawnDuty(vehicleDutyDef, __instance.baseCenter);
                    }
                    else
                    {
                        pawn.mindState.duty = new PawnDuty(DutyDefOf.Idle);
                        pawn.jobs.StopAll();
                    }
                }
            }
        }
    }
}
