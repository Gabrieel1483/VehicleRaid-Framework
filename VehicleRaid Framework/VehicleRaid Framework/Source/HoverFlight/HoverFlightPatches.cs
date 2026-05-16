using HarmonyLib;
using Vehicles;
using Vehicles.Rendering;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace VehicleRaid
{
    [StaticConstructorOnStartup]
    internal static class HoverDrawUtils
    {
        private static readonly MaterialPropertyBlock shadowPropertyBlock = new MaterialPropertyBlock();
        public const float HoverShadowAlpha = 0.6f;

        public static float GetT(CompVehicleHover hoverComp)
        {
            var props = hoverComp.Props;
            if (hoverComp.State == HoverState.Hovering) return 1f;
            if (hoverComp.State == HoverState.TakingOff)
                return Mathf.Clamp01((float)hoverComp.ticksInState / props.maxTicks);
            return 1f - Mathf.Clamp01((float)hoverComp.ticksInState / props.maxTicks);
        }

        public static void DrawShadow(VehiclePawn vehicle, CompVehicleHover hoverComp,
            CompProperties_VehicleHover props, Vector3 vehicleDrawPos)
        {
            if (vehicle.CompVehicleLauncher == null) return;
            string shadowPath = vehicle.CompVehicleLauncher.Props.shadow;
            if (string.IsNullOrEmpty(shadowPath)) return;

            DynamicShadowData shadowData = DynamicShadowData.CreateFrom(vehicle);
            if (shadowData.Invalid) return;

            float t = GetT(hoverComp);
            float alpha;
            if (hoverComp.State == HoverState.Hovering)
                alpha = HoverShadowAlpha;
            else if (props.shadowAlphaPropellerCurve != null)
                alpha = props.shadowAlphaPropellerCurve.Evaluate(t);
            else
                alpha = shadowData.alpha;

            Material mat = MaterialPool.MatFrom(shadowPath, ShaderDatabase.Transparent);
            float shadowOffset = props.hoverShadowOffset * t;
            Vector3 shadowPos = vehicleDrawPos;
            shadowPos.z -= shadowOffset;
            shadowPos.y = Altitudes.AltitudeFor((AltitudeLayer)13);
            Color shadowColor = Color.white;
            shadowColor.a = alpha;
            float scaleFactor = 1f + shadowOffset * 0.08f;
            Vector3 scale = new Vector3(shadowData.width * scaleFactor, 1f, shadowData.height * scaleFactor);
            shadowPropertyBlock.SetColor(ShaderPropertyIDs.Color, shadowColor);
            Matrix4x4 matrix = Matrix4x4.TRS(shadowPos, vehicle.Rotation.AsQuat, scale);
            Graphics.DrawMesh(MeshPool.plane10Back, matrix, mat, 0, null, 0, shadowPropertyBlock);
        }
    }

    [HarmonyPatch(typeof(DynamicDrawManager), "DrawDynamicThings")]
    public static class VehicleHover_DrawDynamicThings_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Map ___map)
        {
            if (___map == null) return;

            foreach (var pawn in ___map.mapPawns.AllPawnsSpawned)
            {
                if (!(pawn is VehiclePawn vehicle)) continue;
                var hoverComp = vehicle.GetComp<CompVehicleHover>();
                if (hoverComp == null || hoverComp.State == HoverState.Grounded) continue;
                if (!vehicle.Position.InBounds(___map)) continue;

                CellRect viewRect = Find.CameraDriver.CurrentViewRect.ExpandedBy(2);
                if (!viewRect.Contains(vehicle.Position)) continue;

                if (!___map.fogGrid.IsFogged(vehicle.Position)) continue;

                try { vehicle.DynamicDrawPhase(DrawPhase.Draw); }
                catch { }
            }
        }
    }

    [HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.DynamicDrawPhaseAt))]
    public static class VehicleHover_DynamicDrawPhaseAt_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(VehiclePawn __instance, DrawPhase phase, ref Vector3 drawLoc, bool flip)
        {
            if (phase != (DrawPhase)2) return;
            var hoverComp = __instance.GetComp<CompVehicleHover>();
            if (hoverComp == null || hoverComp.State == HoverState.Grounded) return;

            drawLoc.x = hoverComp.realPos.x;
            drawLoc.z = hoverComp.realPos.y + hoverComp.currentAltitude;
            drawLoc.y = AltitudeLayer.MetaOverlays.AltitudeFor();

            Vector3 vehiclePos = new Vector3(hoverComp.realPos.x, 0f, hoverComp.realPos.y + hoverComp.currentAltitude);
            HoverDrawUtils.DrawShadow(__instance, hoverComp, hoverComp.Props, vehiclePos);
        }
    }

    [HarmonyPatch(typeof(VehicleDrawTracker), nameof(VehicleDrawTracker.DrawPos), MethodType.Getter)]
    public static class VehicleHover_DrawTrackerDrawPos_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(VehicleDrawTracker __instance, ref Vector3 __result)
        {
            var vehicle = __instance.vehicle;
            if (vehicle == null) return;
            var hoverComp = vehicle.GetComp<CompVehicleHover>();
            if (hoverComp == null || hoverComp.State == HoverState.Grounded) return;

            __result = new Vector3(hoverComp.realPos.x, vehicle.def.Altitude, hoverComp.realPos.y + hoverComp.currentAltitude);
        }
    }

    [HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionBracketFor))]
    public static class VehicleHover_SelectionBracket_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(object obj, Material overrideMat)
        {
            VehiclePawn vehicle = obj as VehiclePawn;
            if (vehicle == null)
            {
                if (obj is VehicleBuilding vb) vehicle = vb.vehicle;
                if (vehicle == null) return true;
            }

            var hoverComp = vehicle.GetComp<CompVehicleHover>();
            if (hoverComp == null || hoverComp.State == HoverState.Grounded) return true;

            Vector3[] bracketLocs = new Vector3[4];
            float angle = vehicle.Angle + vehicle.Transform.rotation;
            Vector3 drawPos = vehicle.DrawTracker.DrawPos;
            drawPos.y = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays);

            IntVec2 rotatedSize = vehicle.RotatedSize;
            Ext_Pawn.CalculateSelectionBracketPositionsWorldForMultiCellPawns<object>(
                bracketLocs, obj, drawPos, rotatedSize.ToVector2(),
                SelectionDrawer.SelectTimes, Vector2.one, angle);

            int num = Mathf.CeilToInt(angle);
            for (int i = 0; i < 4; i++)
            {
                Quaternion q = Quaternion.AngleAxis((float)num, Vector3.up);
                Material mat = overrideMat != null ? overrideMat : MaterialPresets.SelectionBracketMat;
                Graphics.DrawMesh(MeshPool.plane10, bracketLocs[i], q, mat, 0);
                num -= 90;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ThingSelectionUtility), nameof(ThingSelectionUtility.SelectableByMapClick))]
    public static class VehicleHover_SelectableByMapClick_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Thing t, ref bool __result)
        {
            if (!__result) return;
            if (!(t is VehiclePawn vehicle)) return;

            var hoverComp = vehicle.GetComp<CompVehicleHover>();
            if (hoverComp == null || hoverComp.State == HoverState.Grounded) return;

            Vector3 mousePos = UI.MouseMapPosition();
            Vector3 drawPos = vehicle.DrawTracker.DrawPos;

            float dx = drawPos.x - mousePos.x;
            float dz = drawPos.z - mousePos.z;
            float distSq = dx * dx + dz * dz;

            float halfW = vehicle.RotatedSize.x * 0.5f;
            float halfH = vehicle.RotatedSize.z * 0.5f;
            float radiusSq = halfW * halfW + halfH * halfH;

            if (distSq > radiusSq)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(Selector), "HandleMapClicks")]
    public static class VehicleHover_HandleMapClicks_Patch
    {
        private static readonly FieldInfo selectedField = AccessTools.Field(typeof(Selector), "selected");

        [HarmonyPrefix]
        public static bool Prefix(Selector __instance)
        {
            if (Event.current.type != EventType.MouseDown || Event.current.button != 1)
                return true;

            if (!(selectedField.GetValue(__instance) is List<object> selected) || selected.Count == 0)
                return true;

            bool hasHover = false;
            foreach (var obj in selected)
            {
                if (obj is VehiclePawn v && v.GetComp<CompVehicleHover>()?.State == HoverState.Hovering)
                {
                    hasHover = true;
                    break;
                }
            }
            if (!hasHover) return true;

            Map map = Find.CurrentMap;
            Vector3 mousePos = UI.MouseMapPosition();
            IntVec3 cell = IntVec3.FromVector3(mousePos);
            if (!cell.InBounds(map)) { Event.current.Use(); return false; }

            foreach (var obj in selected)
            {
                if (!(obj is VehiclePawn vehicle)) continue;
                var hoverComp = vehicle.GetComp<CompVehicleHover>();
                if (hoverComp == null || hoverComp.State != HoverState.Hovering) continue;
                if (vehicle.Faction != Faction.OfPlayer) continue;

                hoverComp.SetTarget(mousePos);
                FleckMaker.Static(cell, map, FleckDefOf.FeedbackGoto);
            }

            Event.current.Use();
            return false;
        }
    }

    [HarmonyPatch(typeof(VehicleIgnitionController), "GetGizmos")]
    public static class VehicleHover_IgnitionGizmo_Patch
    {
        private static readonly FieldInfo vehicleField = AccessTools.Field(typeof(VehicleIgnitionController), "vehicle");

        [HarmonyPostfix]
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, VehicleIgnitionController __instance)
        {
            VehiclePawn vehicle = vehicleField?.GetValue(__instance) as VehiclePawn;
            CompVehicleHover hoverComp = vehicle?.GetComp<CompVehicleHover>();

            foreach (Gizmo gizmo in __result)
            {
                if (hoverComp != null && hoverComp.IsAirborne && gizmo is Command_Toggle toggle && toggle.isActive())
                    ((Gizmo)toggle).Disable("No se puede apagar el motor mientras el vehículo está en vuelo.");
                yield return gizmo;
            }
        }
    }

    [HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.DisembarkPawn))]
    public static class VehicleHover_DisembarkPawn_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(VehiclePawn __instance, Pawn pawn)
        {
            var hoverComp = __instance.GetComp<CompVehicleHover>();
            if (hoverComp == null || !hoverComp.IsAirborne) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(GenGrid), nameof(GenGrid.StandableBy))]
    public static class VehicleHover_StandableBy_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(IntVec3 c, Map map, Pawn pawn, ref bool __result)
        {
            if (__result) return;
            if (!(pawn is VehiclePawn vehicle)) return;
            var hoverComp = vehicle.GetComp<CompVehicleHover>();
            if (hoverComp == null || hoverComp.State != HoverState.Hovering) return;
            if (GenGrid.InBounds(c, map))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.CanMove), MethodType.Getter)]
    public static class VehicleHover_CanMove_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(VehiclePawn __instance, ref bool __result)
        {
            var hoverComp = __instance.GetComp<CompVehicleHover>();
            if (hoverComp == null) return;
            if (hoverComp.State == HoverState.Hovering)
                __result = true;
        }
    }

    [HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.CanMoveFinal), MethodType.Getter)]
    public static class VehicleHover_CanMoveFinal_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(VehiclePawn __instance, ref bool __result)
        {
            if (!__instance.Spawned || __instance.Map == null) return;
            var hoverComp = __instance.GetComp<CompVehicleHover>();
            if (hoverComp == null) return;
            if (hoverComp.State == HoverState.Hovering)
                __result = false;
            else if (hoverComp.State == HoverState.Grounded)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(CompVehicleLauncher), nameof(CompVehicleLauncher.CanLaunchWithCargoCapacity))]
    public static class VehicleHover_CanLaunchWithCargoCapacity_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CompVehicleLauncher __instance, ref string disableReason, ref bool __result)
        {
            var hoverComp = __instance.Vehicle.GetComp<CompVehicleHover>();
            if (hoverComp == null) return;

            if (!__result && disableReason != null)
            {
                string translated = TranslatorFormattedStringExtensions.Translate("VF_CannotLaunchImmobile", __instance.Vehicle.LabelShort).ToString();
                if (disableReason == translated)
                {
                    disableReason = null;
                    __result = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(VehiclePath), nameof(VehiclePath.DrawPath))]
    public static class VehicleHover_DrawPath_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(VehiclePath __instance, VehiclePawn vehicle)
        {
            if (vehicle == null) return true;
            var hoverComp = vehicle.GetComp<CompVehicleHover>();
            if (hoverComp == null || hoverComp.State == HoverState.Grounded) return true;

            if (!__instance.Found || __instance.Finished) return false;

            float y = Altitudes.AltitudeFor((AltitudeLayer)18);
            for (int i = 0; i < __instance.NodesLeft - 1; i++)
            {
                Vector3 a = __instance.Peek(i).ToVector3Shifted();
                a.y = y;
                Vector3 b = __instance.Peek(i + 1).ToVector3Shifted();
                b.y = y;
                GenDraw.DrawLineBetween(a, b);
            }

            Vector3 drawPos = vehicle.DrawTracker.DrawPos;
            drawPos.y = y;
            Vector3 firstNode = __instance.Peek(0).ToVector3Shifted();
            firstNode.y = y;
            if ((drawPos - firstNode).sqrMagnitude > 0.01f)
                GenDraw.DrawLineBetween(drawPos, firstNode);

            return false;
        }
    }

    [HarmonyPatch(typeof(VehicleTurret), "TurretLocation", MethodType.Getter)]
    public static class VehicleHover_TurretLocation_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(VehicleTurret __instance, ref Vector3 __result)
        {
            var vehicle = __instance.vehicle;
            if (vehicle == null) return;
            var hoverComp = vehicle.GetComp<CompVehicleHover>();
            if (hoverComp == null || hoverComp.State == HoverState.Grounded) return;

            Vector3 thingDrawPos = ((Thing)vehicle).DrawPos;
            Vector3 hoverDrawPos = vehicle.DrawTracker.DrawPos;
            Vector3 offset = hoverDrawPos - thingDrawPos;
            __result += offset;
        }
    }

    [HarmonyPatch(typeof(VehicleTurret), "DrawTargeter")]
    public static class VehicleHover_TurretDrawTargeter_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(VehicleTurret __instance)
        {
            var vehicle = __instance.vehicle;
            if (vehicle == null) return true;

            var hoverComp = vehicle.GetComp<CompVehicleHover>();
            if (hoverComp == null || !hoverComp.IsAirborne) return true;

            if (!__instance.GizmoHighlighted && TurretTargeter.Turret != __instance) return true;

            if (Mathf.Approximately(__instance.restrictedTheta, 0f)) return true;

            if (__instance.attachedTo != null) return true;

            float rotation = vehicle.FullRotation.AsAngle + vehicle.Transform.rotation;

            VehicleTurret.DrawAngleLines(
                __instance.TurretLocation,
                __instance.angleRestricted,
                __instance.MinRange,
                __instance.MaxRange,
                __instance.restrictedTheta,
                rotation
            );

            return false;
        }
    }

    [HarmonyPatch(typeof(VehicleTurret), "AngleBetween")]
    public static class VehicleHover_TurretAngleBetween_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(VehicleTurret __instance, Vector3 position, ref bool __result)
        {
            var vehicle = __instance.vehicle;
            if (vehicle == null) return true;

            var hoverComp = vehicle.GetComp<CompVehicleHover>();
            if (hoverComp == null || !hoverComp.IsAirborne) return true;

            if (__instance.angleRestricted == Vector2.zero) { __result = true; return false; }

            float baseRotation = __instance.attachedTo != null
                ? __instance.attachedTo.TurretRotation
                : vehicle.Rotation.AsAngle + vehicle.Angle + vehicle.Transform.rotation;

            float minAngle = (__instance.angleRestricted.x + baseRotation).ClampAngle();
            float maxAngle = (__instance.angleRestricted.y + baseRotation).ClampAngle();
            float targetAngle = Vector3Utility.AngleFlat(position - __instance.TurretLocation);

            float span = maxAngle - minAngle < 0f ? maxAngle - minAngle + 360f : maxAngle - minAngle;
            float diff = targetAngle - minAngle < 0f ? targetAngle - minAngle + 360f : targetAngle - minAngle;

            __result = diff < span;
            return false;
        }
    }
}