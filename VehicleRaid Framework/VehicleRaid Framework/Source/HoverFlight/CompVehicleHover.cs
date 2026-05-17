using SmashTools;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Vehicles;

namespace VehicleRaid
{
    public enum HoverState
    {
        Grounded,
        TakingOff,
        Hovering,
        Landing,
        Crashing
    }

    public class CompVehicleHover : VehicleComp
    {
        public HoverState State = HoverState.Grounded;
        public int ticksInState = 0;
        public float currentAltitude = 0f;
        public float bobbingOffset = 0f;

        public Vector2 realPos;
        public Vector2 targetPos;
        public bool hasTarget = false;
        public float flyAngle = 0f;
        public float currentFlyAngle = 0f;
        public float moveSpeed = 0f;

        private LocalTargetInfo facingTarget = LocalTargetInfo.Invalid;
        private bool isFacingTarget = false;
        
        public LocalTargetInfo attackTarget = LocalTargetInfo.Invalid;
        public bool isAttackingTarget = false;
        private int turretUpdateTicks = 0;

        private int ticksWithoutPilot = 0;
        private float crashStartAltitude = 0f;
        private const int CrashDelayTicks = 180;
        private const int CrashDurationTicks = 90;

        public CompProperties_VehicleHover Props => (CompProperties_VehicleHover)props;

        public bool IsAirborne => State == HoverState.Hovering || State == HoverState.TakingOff || State == HoverState.Landing || State == HoverState.Crashing;

        private Command_Action takeoffCommand;
        private Command_Action landCommand;
        private Command_Action faceTargetCommand;
        private Command_Action attackTargetCommand;
        private Command_Action cancelTargetCommand;
        private Command_Action jumpCommand;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                realPos = new Vector2(Vehicle.Position.x + 0.5f, Vehicle.Position.z + 0.5f);
                targetPos = realPos;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref State, "hoverState", HoverState.Grounded);
            Scribe_Values.Look(ref ticksInState, "ticksInState", 0);
            Scribe_Values.Look(ref currentAltitude, "currentAltitude", 0f);
            Scribe_Values.Look(ref realPos, "realPos");
            Scribe_Values.Look(ref targetPos, "targetPos");
            Scribe_Values.Look(ref hasTarget, "hasTarget");
            Scribe_Values.Look(ref flyAngle, "flyAngle");
            Scribe_Values.Look(ref currentFlyAngle, "currentFlyAngle", 0f);
            Scribe_Values.Look(ref isFacingTarget, "isFacingTarget", false);
            Scribe_TargetInfo.Look(ref facingTarget, "facingTarget");
            Scribe_Values.Look(ref isAttackingTarget, "isAttackingTarget", false);
            Scribe_TargetInfo.Look(ref attackTarget, "attackTarget");
            Scribe_Values.Look(ref turretUpdateTicks, "turretUpdateTicks", 0);
            Scribe_Values.Look(ref ticksWithoutPilot, "ticksWithoutPilot", 0);
            Scribe_Values.Look(ref crashStartAltitude, "crashStartAltitude", 0f);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (this.Vehicle.Faction != Faction.OfPlayer) yield break;
            if (!this.Vehicle.Spawned || this.Vehicle.Map == null) yield break;

            if (takeoffCommand == null)
            {
                takeoffCommand = new Command_Action
                {
                    defaultLabel = "Despegar",
                    defaultDesc = "El vehículo despega y gana altura.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/LaunchShip"),
                    action = StartTakeoff
                };
            }

            if (landCommand == null)
            {
                landCommand = new Command_Action
                {
                    defaultLabel = "Aterrizar",
                    defaultDesc = "El vehículo desciende y aterriza.",
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                    action = StartLanding
                };
            }

            if (faceTargetCommand == null)
            {
                faceTargetCommand = new Command_Action
                {
                    defaultLabel = "Mirar objetivo",
                    defaultDesc = "El vehículo apuntará frontalmente hacia el objetivo seleccionado.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack"),
                    action = StartFaceTarget
                };
            }

            if (attackTargetCommand == null)
            {
                attackTargetCommand = new Command_Action
                {
                    defaultLabel = "Atacar objetivo",
                    defaultDesc = "El vehículo apuntará frontalmente hacia el objetivo y las torretas lo atacarán automáticamente.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack"),
                    action = StartAttackTarget,
                    hotKey = KeyBindingDefOf.Misc1
                };
            }

            if (cancelTargetCommand == null)
            {
                cancelTargetCommand = new Command_Action
                {
                    defaultLabel = "Cancelar objetivo",
                    defaultDesc = "Cancela el objetivo actual.",
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                    action = CancelTarget
                };
            }

            if (jumpCommand == null)
            {
                jumpCommand = new Command_Action
                {
                    defaultLabel = "VRF_HoverJump".Translate(),
                    defaultDesc = "VRF_HoverJumpDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Jump"),
                    action = delegate
                    {
                        List<FloatMenuOption> list = new List<FloatMenuOption>();
                        foreach (Pawn p in Vehicle.AllPawnsAboard)
                        {
                            Pawn pawn = p;
                            list.Add(new FloatMenuOption(pawn.LabelShort, delegate
                            {
                                Vehicle.DisembarkPawn(pawn);
                            }));
                        }
                        if (list.Count > 0)
                        {
                            Find.WindowStack.Add(new FloatMenu(list));
                        }
                    }
                };
            }

            if (State == HoverState.Grounded || State == HoverState.Landing)
            {
                takeoffCommand.Disabled = false;
                takeoffCommand.disabledReason = null;
                
                if (!Vehicle.Drafted)
                    takeoffCommand.Disable("El motor debe estar encendido para despegar.");
                else if (!Vehicle.HasEnoughOperators || Vehicle.PawnCountToOperateLeft > 0)
                    takeoffCommand.Disable("VF_NotEnoughToOperate".Translate());
                else if (Ext_Vehicles.IsRoofed(Vehicle.Position, Vehicle.Map))
                    takeoffCommand.Disable("CommandLaunchGroupFailUnderRoof".Translate());
                
                yield return takeoffCommand;
            }
            else if (State == HoverState.Hovering || State == HoverState.TakingOff)
            {
                landCommand.Disabled = false;
                landCommand.disabledReason = null;
                IntVec3 landPos = new IntVec3(Mathf.RoundToInt(realPos.x - 0.5f), 0, Mathf.RoundToInt(realPos.y - 0.5f));
                if (landPos.InBounds(Vehicle.Map) && Ext_Vehicles.IsRoofed(landPos, Vehicle.Map))
                    landCommand.Disable("CommandLaunchGroupFailUnderRoof".Translate());
                yield return landCommand;

                if (isAttackingTarget || isFacingTarget)
                {
                    yield return cancelTargetCommand;
                }
                else
                {
                    faceTargetCommand.Disabled = !Vehicle.HasEnoughOperators;
                    if (!Vehicle.HasEnoughOperators)
                        faceTargetCommand.disabledReason = "El vehículo necesita un piloto.";
                    yield return faceTargetCommand;

                    attackTargetCommand.Disabled = !Vehicle.HasEnoughOperators;
                    if (!Vehicle.HasEnoughOperators)
                        attackTargetCommand.disabledReason = "El vehículo necesita un piloto.";
                    yield return attackTargetCommand;
                }

                yield return jumpCommand;
            }
        }

        private void StartFaceTarget()
        {
            TargetingParameters parms = new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = true,
                canTargetLocations = true,
                canTargetAnimals = true,
                canTargetHumans = true,
                canTargetMechs = true
            };

            Find.Targeter.BeginTargeting(parms, target =>
            {
                facingTarget = target;
                isFacingTarget = true;
            }, null, null, null, null, null, true);
        }

        private void StartAttackTarget()
        {
            TargetingParameters parms = new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = true,
                canTargetLocations = false,
                canTargetAnimals = true,
                canTargetHumans = true,
                canTargetMechs = true,
                validator = (TargetInfo targ) =>
                {
                    if (!targ.HasThing) return false;
                    Thing thing = targ.Thing;
                    if (thing == Vehicle) return false;
                    if (thing.Faction == Vehicle.Faction) return false;
                    if (thing is Pawn pawn && pawn.Dead) return false;
                    return true;
                }
            };

            Find.Targeter.BeginTargeting(parms, target =>
            {
                attackTarget = target;
                isAttackingTarget = true;
                facingTarget = target;
                isFacingTarget = true;
                turretUpdateTicks = 0;
            }, null, null, null, null, null, true);
        }

        private void CancelTarget()
        {
            if (isAttackingTarget)
            {
                ClearTurretTargets();
            }
            isAttackingTarget = false;
            attackTarget = LocalTargetInfo.Invalid;
            isFacingTarget = false;
            facingTarget = LocalTargetInfo.Invalid;
        }

        private void AssignTurretTargets()
        {
            if (!attackTarget.IsValid || !attackTarget.HasThing) return;
            
            var turretComp = Vehicle.CompVehicleTurrets;
            if (turretComp == null || turretComp.turrets == null) return;
            
            foreach (var turret in turretComp.turrets)
            {
                if (turret == null) continue;
                if (!turret.IsManned) continue;
                if (!turret.InRange(attackTarget)) continue;
                
                if (turret.targetInfo != attackTarget)
                {
                    turret.SetTarget(attackTarget);
                }
            }
        }

        private void ClearTurretTargets()
        {
            if (!attackTarget.IsValid) return;
            
            var turretComp = Vehicle.CompVehicleTurrets;
            if (turretComp == null || turretComp.turrets == null) return;
            
            foreach (var turret in turretComp.turrets)
            {
                if (turret == null) continue;
                if (turret.targetInfo == attackTarget)
                {
                    turret.SetTarget(LocalTargetInfo.Invalid);
                }
            }
        }

        public void SetTarget(Vector3 worldPos)
        {
            targetPos = new Vector2(worldPos.x, worldPos.z);
            hasTarget = true;
        }

        private void StartTakeoff()
        {
            if (State != HoverState.Grounded && State != HoverState.Landing) return;

            if (State == HoverState.Grounded)
            {
                ticksInState = 0;
                realPos = new Vector2(Vehicle.Position.x + 0.5f, Vehicle.Position.z + 0.5f);
                targetPos = realPos;
            }
            else
                ticksInState = Mathf.Max(0, Props.maxTicks - ticksInState);

            State = HoverState.TakingOff;
            UpdatePropellerSpeed();
        }

        private void StartLanding()
        {
            if (State != HoverState.Hovering && State != HoverState.TakingOff) return;

            CancelTarget();
            hasTarget = false;
            moveSpeed = 0f;

            SnapRotationToCardinal();

            if (State == HoverState.Hovering)
                ticksInState = 0;
            else
                ticksInState = Mathf.Max(0, Props.maxTicks - ticksInState);

            State = HoverState.Landing;
        }

        private void SnapRotationToCardinal()
        {
            float a = ((currentFlyAngle % 360f) + 360f) % 360f;

            Rot4 rot4;
            if (a >= 315f || a < 45f)
                rot4 = Rot4.North;
            else if (a >= 45f && a < 135f)
                rot4 = Rot4.East;
            else if (a >= 135f && a < 225f)
                rot4 = Rot4.South;
            else
                rot4 = Rot4.West;

            flyAngle = currentFlyAngle;
            Vehicle.Angle = 0f;
            Vehicle.Transform.rotation = 0f;
            Vehicle.FullRotation = (Rot8)rot4;
            Vehicle.Rotation = rot4;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!Vehicle.Spawned || Vehicle.Map == null) return;

            if (State == HoverState.TakingOff)
            {
                ticksInState++;
                UpdateAltitude();
                TickMotes();

                if (ticksInState >= Props.maxTicks)
                {
                    ticksInState = Props.maxTicks;
                    State = HoverState.Hovering;
                }
                UpdatePropellerSpeed();
            }
            else if (State == HoverState.Landing)
            {
                ticksInState++;
                UpdateAltitude();
                TickMotes();
                UpdatePropellerSpeed();

                if (ticksInState >= Props.maxTicks)
                {
                    ticksInState = 0;
                    currentAltitude = 0f;
                    State = HoverState.Grounded;
                    SnapPositionToGrid();
                    UpdatePropellerSpeed();
                }
            }
            else if (State == HoverState.Hovering)
            {
                bobbingOffset = Props.hoverBobAmount * Mathf.Sin(Find.TickManager.TicksGame * Props.hoverBobSpeed * Mathf.PI / 60f);
                currentAltitude = bobbingOffset;
                UpdatePropellerSpeed();
                TickFacingTarget();
                TickHoverMovement();
                TickAttackTarget();

                if (!Vehicle.HasEnoughOperators)
                {
                    ticksWithoutPilot++;
                    if (ticksWithoutPilot % 30 == 0 && Vehicle.Spawned)
                        FleckMaker.ThrowSmoke(new Vector3(realPos.x, 0f, realPos.y), Vehicle.Map, 1f);
                    if (ticksWithoutPilot >= CrashDelayTicks)
                        StartCrashing();
                }
                else
                {
                    ticksWithoutPilot = 0;
                }
            }
            else if (State == HoverState.Crashing)
            {
                ticksInState++;
                float t = Mathf.Clamp01((float)ticksInState / CrashDurationTicks);
                Vehicle.Angle = Mathf.Lerp(0f, 45f, t);

                float speed = Props.hoverMoveSpeed * 0.4f / 60f;
                float rad = currentFlyAngle * Mathf.Deg2Rad;
                realPos += new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * speed * (1f - t * 0.5f);
                UpdateGridPosition();

                currentAltitude = Mathf.Lerp(crashStartAltitude, -0.3f, t);

                if (ticksInState % 4 == 0 && Vehicle.Spawned)
                {
                    FleckMaker.ThrowSmoke(new Vector3(realPos.x, 0f, realPos.y), Vehicle.Map, 2f);
                    if (t > 0.5f)
                        FleckMaker.ThrowMicroSparks(new Vector3(realPos.x, 0f, realPos.y), Vehicle.Map);
                }

                if (ticksInState >= CrashDurationTicks)
                    CrashImpact();
            }
        }

        private void TickAttackTarget()
        {
            if (!isAttackingTarget) return;

            if (attackTarget.HasThing && (attackTarget.Thing == null || attackTarget.Thing.Destroyed || !attackTarget.Thing.Spawned))
            {
                CancelTarget();
                return;
            }

            if (Find.TickManager.TicksGame % 15 != 0) return;

            AssignTurretTargets();
        }

        private void TickFacingTarget()
        {
            if (!isFacingTarget) return;

            Vector3 targetWorldPos;

            if (facingTarget.HasThing)
            {
                if (facingTarget.Thing == null || facingTarget.Thing.Destroyed || !facingTarget.Thing.Spawned)
                {
                    isFacingTarget = false;
                    facingTarget = LocalTargetInfo.Invalid;
                    if (isAttackingTarget)
                    {
                        CancelTarget();
                    }
                    return;
                }
                targetWorldPos = facingTarget.Thing.DrawPos;
            }
            else
            {
                targetWorldPos = facingTarget.Cell.ToVector3Shifted();
            }

            float dx = targetWorldPos.x - realPos.x;
            float dz = targetWorldPos.z - realPos.y;

            if (Mathf.Abs(dx) < 0.1f && Mathf.Abs(dz) < 0.1f) return;

            float angle = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            flyAngle = angle;
            RotateTowardsAngle(flyAngle);
        }

        private void TickHoverMovement()
        {
            if (!hasTarget) return;

            Vector2 diff = targetPos - realPos;
            float dist = diff.magnitude;

            if (dist < 0.1f)
            {
                hasTarget = false;
                moveSpeed = 0f;
                SnapPositionToGrid();
                return;
            }

            float speed = Props.hoverMoveSpeed / 60f;
            moveSpeed = speed;

            if (!isFacingTarget)
            {
                flyAngle = Mathf.Atan2(diff.x, diff.y) * Mathf.Rad2Deg;
                RotateTowardsAngle(flyAngle);
            }

            if (dist <= speed)
            {
                realPos = targetPos;
                hasTarget = false;
                moveSpeed = 0f;
                SnapPositionToGrid();
            }
            else
            {
                Vector2 dir = diff.normalized;
                realPos += dir * speed;
                UpdateGridPosition();
            }
        }

        private void RotateTowardsAngle(float targetAngle)
        {
            float maxDelta = Props.hoverRotationSpeed / 60f;
            currentFlyAngle = MoveTowardsAngle(currentFlyAngle, targetAngle, maxDelta);
            ApplyRotationFromAngle(currentFlyAngle);
        }

        private static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float delta = Mathf.DeltaAngle(current, target);
            if (Mathf.Abs(delta) <= maxDelta)
                return target;
            return current + Mathf.Sign(delta) * maxDelta;
        }

        private void ApplyRotationFromAngle(float angle)
        {
            float a = ((angle % 360f) + 360f) % 360f;

            Rot4 cardinal;
            float visualOffset;

            if (a >= 315f || a < 45f)
            {
                cardinal = Rot4.North;
                visualOffset = a < 180f ? a : a - 360f;
            }
            else if (a >= 45f && a < 135f)
            {
                cardinal = Rot4.East;
                visualOffset = a - 90f;
            }
            else if (a >= 135f && a < 225f)
            {
                cardinal = Rot4.South;
                visualOffset = a - 180f;
            }
            else
            {
                cardinal = Rot4.West;
                visualOffset = a - 270f;
            }

            Rot8 rot8 = (Rot8)cardinal;
            if (Vehicle.FullRotation != rot8)
            {
                Vehicle.FullRotation = rot8;
                Vehicle.Rotation = cardinal;
            }

            Vehicle.Angle = 0f;
            Vehicle.Transform.rotation = visualOffset;
        }

        private void SnapPositionToGrid()
        {
            if (!Vehicle.Spawned || Vehicle.Map == null) return;
            IntVec3 newPos = new IntVec3(Mathf.RoundToInt(realPos.x - 0.5f), 0, Mathf.RoundToInt(realPos.y - 0.5f));
            if (newPos.InBounds(Vehicle.Map) && newPos != Vehicle.Position)
                Vehicle.Position = newPos;
        }

        private void UpdateGridPosition()
        {
            if (!Vehicle.Spawned || Vehicle.Map == null) return;
            IntVec3 newPos = new IntVec3((int)realPos.x, 0, (int)realPos.y);
            if (newPos.InBounds(Vehicle.Map) && newPos != Vehicle.Position)
                Vehicle.Position = newPos;
        }

        private void UpdateAltitude()
        {
            float t = Mathf.Clamp01((float)ticksInState / Props.maxTicks);
            if (State == HoverState.Landing)
                t = 1f - t;
            float targetBob = Props.hoverBobAmount * Mathf.Sin(Find.TickManager.TicksGame * Props.hoverBobSpeed * Mathf.PI / 60f);
            currentAltitude = Mathf.Lerp(0f, targetBob, t);
            bobbingOffset = currentAltitude;
        }

        private void UpdatePropellerSpeed()
        {
            if (Props.angularVelocityPropeller == null || Vehicle.DrawTracker?.overlayRenderer == null) return;

            float t;
            if (State == HoverState.Hovering)
                t = 1f;
            else if (State == HoverState.Grounded)
                t = 0f;
            else
            {
                t = Mathf.Clamp01((float)ticksInState / Props.maxTicksPropeller);
                if (State == HoverState.Landing) t = 1f - t;
            }

            Vehicle.DrawTracker.overlayRenderer.SetAcceleration(Props.angularVelocityPropeller.Evaluate(t));
        }

        private void TickMotes()
        {
            if (!VehicleMod.settings.main.aerialVehicleEffects) return;

            float t = Mathf.Clamp01((float)ticksInState / Props.maxTicks);
            if (State == HoverState.Landing) t = 1f - t;

            if (Props.fleckDataVertical != null) TryThrowFleck(Props.fleckDataVertical, t);
            if (Props.fleckDataPropeller != null) TryThrowFleck(Props.fleckDataPropeller, t);
        }

        private void StartCrashing()
        {
            State = HoverState.Crashing;
            ticksInState = 0;
            crashStartAltitude = currentAltitude;
            CancelTarget();
            hasTarget = false;
            moveSpeed = 0f;
            for (int i = Vehicle.AllPawnsAboard.Count - 1; i >= 0; i--)
                Vehicle.DisembarkPawn(Vehicle.AllPawnsAboard[i]);
        }

        private void CrashImpact()
        {
            if (!Vehicle.Spawned) return;

            Map map = Vehicle.Map;
            IntVec3 pos = Vehicle.Position;

            for (int i = 0; i < 5; i++)
                Vehicle.TakeDamage(new DamageInfo(DamageDefOf.Bomb, 30f));

            GenExplosion.DoExplosion(pos, map, 2.5f, DamageDefOf.Bomb, null, 15);
            GenExplosion.DoExplosion(pos, map, 1.5f, DamageDefOf.Flame, null);

            for (int i = 0; i < 8; i++)
            {
                IntVec3 cell = pos + GenRadial.RadialPattern[i];
                if (cell.InBounds(map))
                    FleckMaker.ThrowDustPuff(cell.ToVector3Shifted(), map, 2.5f);
            }

            State = HoverState.Grounded;
            currentAltitude = 0f;
            ticksInState = 0;
            ticksWithoutPilot = 0;
            Vehicle.Angle = 0f;
            Vehicle.Transform.rotation = 0f;
            Vehicle.ignition.Drafted = false;
            SnapPositionToGrid();
            UpdatePropellerSpeed();

            if (Vehicle.Faction == Faction.OfPlayer)
                Messages.Message("VRF_VehicleCrashed".Translate(Vehicle.LabelShort), Vehicle, MessageTypeDefOf.NegativeEvent);
        }

        private void TryThrowFleck(FleckData fleckData, float t)
        {
            float frequency = fleckData.frequency != null ? fleckData.frequency.Evaluate(t) : 0f;
            if (frequency <= 0) return;

            float particlesToSpawn = frequency / 60f;
            int count = Mathf.FloorToInt(particlesToSpawn);
            if (Rand.Value < particlesToSpawn - count) count++;

            for (int i = 0; i < count; i++)
            {
                float size = fleckData.size != null ? fleckData.size.Evaluate(t) : 1f;
                float? airTime = fleckData.airTime?.Evaluate(t);
                float? speed = fleckData.speed?.Evaluate(t);
                float? rotationRate = fleckData.rotationRate?.Evaluate(t);
                float angle = fleckData.angle.RandomInRange;

                Vector3 pos = new Vector3(realPos.x, 0f, realPos.y);
                if (fleckData.drawOffset != null)
                    pos = pos.PointFromAngle(fleckData.drawOffset.Evaluate(t), angle);

                pos += fleckData.originOffset;

                if (fleckData.originOffsetRange != null)
                {
                    Vector3 from = fleckData.originOffsetRange.from;
                    Vector3 to = fleckData.originOffsetRange.to;
                    pos += new Vector3(Rand.Range(from.x, to.x), Rand.Range(from.y, to.y), Rand.Range(from.z, to.z));
                }
                pos.y = Altitudes.AltitudeFor(fleckData.def.altitudeLayer);

                LaunchProtocol.ThrowFleck(fleckData.def, pos, Vehicle.Map, size, airTime, angle, speed, rotationRate);
            }
        }
    }
}
