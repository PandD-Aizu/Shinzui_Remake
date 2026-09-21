using System;

namespace Shinzui.Application.CustomSpatialAudio
{
    /// <summary>Metres in a shared Cartesian coordinate system; no engine coordinate dependency.</summary>
    public readonly struct AcousticVector3
    {
        public readonly float X, Y, Z;

        public AcousticVector3(float x, float y, float z)
        {
            if (!AcousticValueValidation.IsFinite(x) || !AcousticValueValidation.IsFinite(y) ||
                !AcousticValueValidation.IsFinite(z))
                throw new ArgumentOutOfRangeException(nameof(x), "Coordinates must be finite.");
            X = x;
            Y = y;
            Z = z;
        }

        public bool IsFinite => AcousticValueValidation.IsFinite(X) &&
            AcousticValueValidation.IsFinite(Y) && AcousticValueValidation.IsFinite(Z);
    }

    /// <summary>Three nonnegative, finite band values. Units are specified by the containing DTO.</summary>
    public readonly struct AcousticBandValues
    {
        public readonly float Low, Mid, High;

        public AcousticBandValues(float low, float mid, float high)
        {
            if (!AcousticValueValidation.IsNonnegativeFinite(low) ||
                !AcousticValueValidation.IsNonnegativeFinite(mid) ||
                !AcousticValueValidation.IsNonnegativeFinite(high))
                throw new ArgumentOutOfRangeException(nameof(low), "Band values must be finite and nonnegative.");
            Low = low;
            Mid = mid;
            High = high;
        }

        public bool IsNonnegativeFinite => AcousticValueValidation.IsNonnegativeFinite(Low) &&
            AcousticValueValidation.IsNonnegativeFinite(Mid) &&
            AcousticValueValidation.IsNonnegativeFinite(High);
    }

    /// <summary>
    /// Absorption is an energy fraction per band. OpeningFraction is the open area fraction
    /// of this wall, modelled as diffuse energy escape, not as a positioned portal or diffraction.
    /// </summary>
    public readonly struct AcousticWall
    {
        public readonly AcousticBandValues Absorption;
        public readonly float OpeningFraction;

        public AcousticWall(AcousticBandValues absorption, float openingFraction = 0f)
        {
            if (!absorption.IsNonnegativeFinite || absorption.Low > 1f || absorption.Mid > 1f ||
                absorption.High > 1f)
                throw new ArgumentOutOfRangeException(nameof(absorption), "Absorption must be in [0, 1].");
            if (!AcousticValueValidation.IsUnitInterval(openingFraction))
                throw new ArgumentOutOfRangeException(nameof(openingFraction));
            Absorption = absorption;
            OpeningFraction = openingFraction;
        }

        public bool IsValid => Absorption.IsNonnegativeFinite && Absorption.Low <= 1f &&
            Absorption.Mid <= 1f && Absorption.High <= 1f &&
            AcousticValueValidation.IsUnitInterval(OpeningFraction);
    }

    /// <summary>
    /// Axis-aligned, unobstructed single-room prototype. Each edge is limited to 0.1..100 metres.
    /// Wall order is -X, +X, -Y, +Y, -Z, +Z. Open fractions do not describe aperture positions.
    /// </summary>
    public readonly struct RectangularAcousticRoom
    {
        public const float MinimumEdgeMetres = 0.1f;
        public const float MaximumEdgeMetres = 100f;
        public readonly AcousticVector3 Min, Max;
        public readonly AcousticWall NegativeX, PositiveX, NegativeY, PositiveY, NegativeZ, PositiveZ;

        public RectangularAcousticRoom(AcousticVector3 min, AcousticVector3 max, AcousticWall uniformWall)
            : this(min, max, uniformWall, uniformWall, uniformWall, uniformWall, uniformWall, uniformWall) { }

        public RectangularAcousticRoom(AcousticVector3 min, AcousticVector3 max,
            AcousticWall negativeX, AcousticWall positiveX, AcousticWall negativeY,
            AcousticWall positiveY, AcousticWall negativeZ, AcousticWall positiveZ)
        {
            Min = min;
            Max = max;
            NegativeX = negativeX;
            PositiveX = positiveX;
            NegativeY = negativeY;
            PositiveY = positiveY;
            NegativeZ = negativeZ;
            PositiveZ = positiveZ;
            if (!IsValid)
                throw new ArgumentException("Room requires finite bounds, edges in [0.1, 100] metres and valid walls.");
        }

        public bool IsValid => Min.IsFinite && Max.IsFinite &&
            ValidEdge((double)Max.X - Min.X) && ValidEdge((double)Max.Y - Min.Y) &&
            ValidEdge((double)Max.Z - Min.Z) && NegativeX.IsValid && PositiveX.IsValid &&
            NegativeY.IsValid && PositiveY.IsValid && NegativeZ.IsValid && PositiveZ.IsValid;

        public bool Contains(AcousticVector3 point) => point.IsFinite && point.X >= Min.X &&
            point.X <= Max.X && point.Y >= Min.Y && point.Y <= Max.Y && point.Z >= Min.Z && point.Z <= Max.Z;

        public AcousticWall GetWall(int index)
        {
            switch (index)
            {
                case 0: return NegativeX;
                case 1: return PositiveX;
                case 2: return NegativeY;
                case 3: return PositiveY;
                case 4: return NegativeZ;
                case 5: return PositiveZ;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static bool ValidEdge(double value) => value >= MinimumEdgeMetres && value <= MaximumEdgeMetres;
    }

    /// <summary>
    /// One path with absolute travel delay, linear band amplitudes, listener-to-apparent-source unit
    /// direction (zero for coincident positions), and equal-power stereo pan input. Pan is not HRTF.
    /// </summary>
    public readonly struct AcousticPathTap
    {
        public readonly float DelaySeconds, DistanceMetres, Pan;
        public readonly AcousticBandValues Amplitude;
        public readonly AcousticVector3 Direction;

        public AcousticPathTap(float delaySeconds, float distanceMetres, AcousticBandValues amplitude,
            AcousticVector3 direction, float pan)
        {
            if (!AcousticValueValidation.IsNonnegativeFinite(delaySeconds) ||
                !AcousticValueValidation.IsNonnegativeFinite(distanceMetres) ||
                !amplitude.IsNonnegativeFinite || !direction.IsFinite ||
                !AcousticValueValidation.IsFinite(pan) || pan < -1f || pan > 1f)
                throw new ArgumentException("Path values must be finite, distances/gains nonnegative and pan in [-1, 1].");
            DelaySeconds = delaySeconds;
            DistanceMetres = distanceMetres;
            Amplitude = amplitude;
            Direction = direction;
            Pan = pan;
        }
    }

    /// <summary>
    /// Immutable fixed-size main-thread result for publication to DSP. No mutable arrays or engine objects.
    /// RT60 is a bounded diffuse-field estimate; LateBandGain is a heuristic diffuse excitation scale.
    /// </summary>
    public readonly struct RoomAcousticResponse
    {
        public const int ReflectionCount = 6;
        public const int PathCount = 7;
        public readonly AcousticPathTap Direct;
        public readonly AcousticBandValues Rt60Seconds;
        public readonly AcousticBandValues LateBandGain;
        /// <summary>RMS of LateBandGain for diagnostics. DSP must use individual band gains.</summary>
        public readonly float LateGain;
        private readonly AcousticPathTap negativeX, positiveX, negativeY, positiveY, negativeZ, positiveZ;

        public RoomAcousticResponse(AcousticPathTap direct, AcousticPathTap negativeX,
            AcousticPathTap positiveX, AcousticPathTap negativeY, AcousticPathTap positiveY,
            AcousticPathTap negativeZ, AcousticPathTap positiveZ, AcousticBandValues rt60Seconds,
            float lateGain = 1f)
            : this(direct, negativeX, positiveX, negativeY, positiveY, negativeZ, positiveZ,
                rt60Seconds, new AcousticBandValues(lateGain, lateGain, lateGain)) { }

        public RoomAcousticResponse(AcousticPathTap direct, AcousticPathTap negativeX,
            AcousticPathTap positiveX, AcousticPathTap negativeY, AcousticPathTap positiveY,
            AcousticPathTap negativeZ, AcousticPathTap positiveZ, AcousticBandValues rt60Seconds,
            AcousticBandValues lateBandGain)
        {
            if (!rt60Seconds.IsNonnegativeFinite || rt60Seconds.Low <= 0f ||
                rt60Seconds.Mid <= 0f || rt60Seconds.High <= 0f)
                throw new ArgumentOutOfRangeException(nameof(rt60Seconds));
            if (!lateBandGain.IsNonnegativeFinite || lateBandGain.Low > 1f ||
                lateBandGain.Mid > 1f || lateBandGain.High > 1f)
                throw new ArgumentOutOfRangeException(nameof(lateBandGain));
            Direct = direct;
            this.negativeX = negativeX;
            this.positiveX = positiveX;
            this.negativeY = negativeY;
            this.positiveY = positiveY;
            this.negativeZ = negativeZ;
            this.positiveZ = positiveZ;
            Rt60Seconds = rt60Seconds;
            LateBandGain = lateBandGain;
            LateGain = (float)Math.Sqrt(((double)lateBandGain.Low * lateBandGain.Low +
                (double)lateBandGain.Mid * lateBandGain.Mid +
                (double)lateBandGain.High * lateBandGain.High) / 3.0);
        }

        public AcousticPathTap GetPath(int index)
        {
            if (index == 0) return Direct;
            return GetReflection(index - 1);
        }

        public AcousticPathTap GetReflection(int index)
        {
            switch (index)
            {
                case 0: return negativeX;
                case 1: return positiveX;
                case 2: return negativeY;
                case 3: return positiveY;
                case 4: return negativeZ;
                case 5: return positiveZ;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }

    internal static class AcousticValueValidation
    {
        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool IsNonnegativeFinite(float value) => IsFinite(value) && value >= 0f;
        internal static bool IsUnitInterval(float value) => IsFinite(value) && value >= 0f && value <= 1f;
    }
}
