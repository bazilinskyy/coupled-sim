using System;
using UnityEngine;

namespace CoupledSim.Vehicles
{
    /// <summary>
    /// Pacejka's Magic Formula tyre curve, y(x) = D sin(C atan(B x - E (B x - atan(B x)))), with x the
    /// slip (longitudinal slip ratio, or lateral slip angle in radians) and y the friction coefficient.
    /// Unity's WheelCollider takes a simpler curve (rising to an extremum, falling to an asymptote);
    /// <see cref="ToWheelFriction"/> fits that curve to the Magic Formula: the peak at the same slip
    /// and height, the sliding value where the formula levels off.
    /// </summary>
    [Serializable]
    public struct MagicFormula
    {
        [Tooltip("Stiffness factor.")] public float B;
        [Tooltip("Shape factor (about 1.9 longitudinal, 1.3 lateral).")] public float C;
        [Tooltip("Peak friction coefficient.")] public float D;
        [Tooltip("Curvature factor (-1..1).")] public float E;

        // typical passenger-car tyres on dry asphalt (Pacejka, Tyre and Vehicle Dynamics, 2012)
        public static MagicFormula DryLongitudinal => new MagicFormula { B = 10f, C = 1.9f, D = 1.0f, E = 0.97f };
        public static MagicFormula DryLateral => new MagicFormula { B = 8f, C = 1.3f, D = 1.0f, E = -1.6f };

        public bool IsSet => B > 0f && C > 0f && D > 0f;

        public float Evaluate(float x)
        {
            float bx = B * x;
            return D * Mathf.Sin(C * Mathf.Atan(bx - E * (bx - Mathf.Atan(bx))));
        }

        /// <summary>Slip of the peak and the friction there, and the friction at <paramref name="slideSlip"/>.</summary>
        public void Fit(float maxSlip, out float peakSlip, out float peak, out float slideSlip, out float slide)
        {
            peakSlip = 0.1f; peak = 0f;
            const int n = 400;
            for (int i = 1; i <= n; i++)
            {
                float x = maxSlip * i / n, y = Evaluate(x);
                if (y > peak) { peak = y; peakSlip = x; }
            }
            slideSlip = Mathf.Min(maxSlip, peakSlip * 4f);
            slide = Mathf.Max(0.05f, Evaluate(slideSlip));
        }

        /// <summary>The WheelCollider friction curve closest to this formula (stiffness scales grip, e.g. on wet roads).</summary>
        public WheelFrictionCurve ToWheelFriction(WheelFrictionCurve c, float maxSlip, float stiffness)
        {
            Fit(maxSlip, out var ps, out var p, out var ss, out var s);
            c.extremumSlip = ps; c.extremumValue = p; c.asymptoteSlip = ss; c.asymptoteValue = s; c.stiffness = stiffness;
            return c;
        }
    }
}
