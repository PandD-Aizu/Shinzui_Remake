using System;

namespace Shinzui.Application.CustomSpatialAudio
{
    /// <summary>Physical doorway aperture in world space. Warp triggers never implement this interface.</summary>
    public interface IAcousticDoorSource
    {
        bool TryGetAcousticDoor(out AcousticDoorState state);
    }
    public readonly struct AcousticDoorState
    {
        public readonly AcousticVector3 Centre, Normal;
        public readonly float Width, Height, OpeningFraction;
        public AcousticDoorState(AcousticVector3 centre, AcousticVector3 normal, float width, float height, float openingFraction)
        {
            if (width <= 0 || height <= 0 || !AcousticValueValidation.IsFinite(width) || !AcousticValueValidation.IsFinite(height) ||
                !AcousticValueValidation.IsUnitInterval(openingFraction)) throw new ArgumentException("Invalid doorway aperture.");
            Centre = centre; Normal = normal; Width = width; Height = height; OpeningFraction = openingFraction;
        }
        public bool Crosses(AcousticVector3 a, AcousticVector3 b)
        {
            float da = (a.X - Centre.X) * Normal.X + (a.Z - Centre.Z) * Normal.Z;
            float db = (b.X - Centre.X) * Normal.X + (b.Z - Centre.Z) * Normal.Z;
            if (da * db > 0 || Math.Abs(da - db) < .00001f) return false;
            float t = da / (da - db);
            float x = a.X + (b.X - a.X) * t - Centre.X, z = a.Z + (b.Z - a.Z) * t - Centre.Z;
            float y = a.Y + (b.Y - a.Y) * t - Centre.Y;
            return Math.Abs(-Normal.Z * x + Normal.X * z) <= Width / 2 && Math.Abs(y) <= Height / 2;
        }
    }
}
