using SmashTools;
using UnityEngine;
using Vehicles;

namespace VehicleRaid
{
    public class CompProperties_VehicleHover : VehicleCompProperties
    {
        public FlightType flightType = FlightType.Hover;
        public int maxTicks;
        public int maxTicksVertical;
        public int maxTicksPropeller;

        public float hoverAltitude = 4f;
        public float hoverShadowOffset = 1.2f;
        public float hoverBobAmount = 0.12f;
        public float hoverBobSpeed = 2.0f;

        public float hoverMoveSpeed = 3f;

        public float hoverRotationSpeed = 90f;

        public BezierCurve rotationCurve;
        public BezierCurve rotationVerticalCurve;
        public BezierCurve angularVelocityPropeller;

        public BezierCurve xPositionCurve;
        public BezierCurve xPositionVerticalCurve;
        public BezierCurve zPositionCurve;
        public BezierCurve zPositionVerticalCurve;

        public BezierCurve shadowAlphaPropellerCurve;

        public FleckData fleckDataVertical;
        public FleckData fleckDataPropeller;

        public int landingMaxTicks = 600;
        public BezierCurve landingRotationCurve;
        public BezierCurve landingForwardCurve;
        public BezierCurve landingAltitudeCurve;

        public int runwayClearCells = 15;

        public CompProperties_VehicleHover()
        {
            this.compClass = typeof(CompVehicleHover);
        }
    }
}
