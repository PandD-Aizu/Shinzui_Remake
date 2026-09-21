using System;

namespace Shinzui.Application.CustomSpatialAudio
{
    /// <summary>A convex, unobstructed box cell. Split an L-shaped space into separate cells.</summary>
    public readonly struct PortalAcousticRoom
    {
        public readonly int Id;
        public readonly RectangularAcousticRoom Bounds;

        public PortalAcousticRoom(int id, RectangularAcousticRoom bounds)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (!bounds.IsValid) throw new ArgumentException("Room bounds must be valid.", nameof(bounds));
            Id = id;
            Bounds = bounds;
        }
    }

    /// <summary>
    /// Bidirectional, positioned opening between adjacent cells. Transmission is an energy fraction
    /// per band when fully open; amplitude uses sqrt(OpeningFraction * Transmission). Closed means
    /// no transmission in this prototype, not a simulated wall/door material or physical diffraction.
    /// Create a new graph snapshot when a door changes. Centre routing is an aperture approximation.
    /// </summary>
    public readonly struct AcousticPortal
    {
        public readonly int Id, RoomAId, RoomBId;
        public readonly AcousticVector3 Centre;
        public readonly float OpeningFraction;
        public readonly AcousticBandValues Transmission;

        public AcousticPortal(int id, int roomAId, int roomBId, AcousticVector3 centre,
            float openingFraction, AcousticBandValues transmission)
        {
            if (id < 0 || roomAId < 0 || roomBId < 0 || roomAId == roomBId)
                throw new ArgumentException("Portal and room IDs must be nonnegative; rooms must differ.");
            if (!centre.IsFinite) throw new ArgumentException("Portal centre must be finite.", nameof(centre));
            if (!AcousticValueValidation.IsUnitInterval(openingFraction))
                throw new ArgumentOutOfRangeException(nameof(openingFraction));
            if (!transmission.IsNonnegativeFinite || transmission.Low > 1f ||
                transmission.Mid > 1f || transmission.High > 1f)
                throw new ArgumentOutOfRangeException(nameof(transmission));
            Id = id;
            RoomAId = roomAId;
            RoomBId = roomBId;
            Centre = centre;
            OpeningFraction = openingFraction;
            Transmission = transmission;
        }

        public bool IsOpen => OpeningFraction > 0f &&
            (Transmission.Low > 0f || Transmission.Mid > 0f || Transmission.High > 0f);
    }

    /// <summary>
    /// Immutable, bounded control-thread snapshot with stable ID ordering. Cells may share faces,
    /// but their interiors cannot overlap. Every portal must lie inside a shared face. Consequently
    /// each segment joining points within a cell remains in free space. Warps are not portals.
    /// </summary>
    public sealed class PortalAcousticGraph
    {
        public const int MaximumRoomCount = 64;
        public const int MaximumPortalCount = 128;
        private readonly PortalAcousticRoom[] rooms;
        private readonly AcousticPortal[] portals;

        public int RoomCount => rooms.Length;
        public int PortalCount => portals.Length;

        public PortalAcousticGraph(PortalAcousticRoom[] rooms, AcousticPortal[] portals)
        {
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            if (portals == null) throw new ArgumentNullException(nameof(portals));
            if (rooms.Length == 0 || rooms.Length > MaximumRoomCount)
                throw new ArgumentOutOfRangeException(nameof(rooms));
            if (portals.Length > MaximumPortalCount) throw new ArgumentOutOfRangeException(nameof(portals));
            this.rooms = (PortalAcousticRoom[])rooms.Clone();
            this.portals = (AcousticPortal[])portals.Clone();
            Array.Sort(this.rooms, (a, b) => a.Id.CompareTo(b.Id));
            Array.Sort(this.portals, (a, b) => a.Id.CompareTo(b.Id));
            for (int i = 0; i < this.rooms.Length; i++)
            {
                PortalAcousticRoom room = this.rooms[i];
                if (room.Id < 0 || !room.Bounds.IsValid || (i > 0 && this.rooms[i - 1].Id == room.Id))
                    throw new ArgumentException("Room IDs must be unique and bounds valid.", nameof(rooms));
                for (int j = 0; j < i; j++)
                    if (InteriorsOverlap(room.Bounds, this.rooms[j].Bounds))
                        throw new ArgumentException($"Room interiors must not overlap: {room.Id} and {this.rooms[j].Id}.", nameof(rooms));
            }
            for (int i = 0; i < this.portals.Length; i++)
            {
                AcousticPortal portal = this.portals[i];
                int a = FindRoomIndex(portal.RoomAId), b = FindRoomIndex(portal.RoomBId);
                if (portal.Id < 0 || a < 0 || b < 0 || a == b ||
                    (i > 0 && this.portals[i - 1].Id == portal.Id) ||
                    !IsInsideSharedFace(this.rooms[a].Bounds, this.rooms[b].Bounds, portal.Centre))
                    throw new ArgumentException("Portals require unique IDs, distinct existing rooms and a centre inside their shared face.", nameof(portals));
            }
        }

        public PortalAcousticRoom GetRoom(int index) => rooms[index];
        public AcousticPortal GetPortal(int index) => portals[index];

        public int FindRoomIndex(int id)
        {
            int low = 0, high = rooms.Length - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                if (rooms[middle].Id == id) return middle;
                if (rooms[middle].Id < id) low = middle + 1;
                else high = middle - 1;
            }
            return -1;
        }

        private static bool InteriorsOverlap(RectangularAcousticRoom a, RectangularAcousticRoom b) =>
            Math.Max(a.Min.X, b.Min.X) < Math.Min(a.Max.X, b.Max.X) &&
            Math.Max(a.Min.Y, b.Min.Y) < Math.Min(a.Max.Y, b.Max.Y) &&
            Math.Max(a.Min.Z, b.Min.Z) < Math.Min(a.Max.Z, b.Max.Z);

        private static bool IsInsideSharedFace(RectangularAcousticRoom a, RectangularAcousticRoom b,
            AcousticVector3 p)
        {
            if (!a.Contains(p) || !b.Contains(p)) return false;
            bool insideX = p.X > Math.Max(a.Min.X, b.Min.X) && p.X < Math.Min(a.Max.X, b.Max.X);
            bool insideY = p.Y > Math.Max(a.Min.Y, b.Min.Y) && p.Y < Math.Min(a.Max.Y, b.Max.Y);
            bool insideZ = p.Z > Math.Max(a.Min.Z, b.Min.Z) && p.Z < Math.Min(a.Max.Z, b.Max.Z);
            return ((a.Max.X == b.Min.X || b.Max.X == a.Min.X) && insideY && insideZ) ||
                ((a.Max.Y == b.Min.Y || b.Max.Y == a.Min.Y) && insideX && insideZ) ||
                ((a.Max.Z == b.Min.Z || b.Max.Z == a.Min.Z) && insideX && insideY);
        }
    }

    /// <summary>
    /// One selected centre-routed path, not a diffraction solution or coupled reverberation model.
    /// A missing connection carries a silent zero-delay tap. Path delays are not clamped: the
    /// receiving DSP must enforce its delay budget (currently three seconds for the room prototype).
    /// </summary>
    public sealed class PortalAcousticPath
    {
        private readonly int[] portalIds;
        public bool IsReachable { get; }
        public AcousticPathTap Tap { get; }
        public int PortalCount => portalIds.Length;

        public PortalAcousticPath(bool isReachable, AcousticPathTap tap, int[] portalIds)
        {
            if (portalIds == null) throw new ArgumentNullException(nameof(portalIds));
            if (portalIds.Length > PortalAcousticGraph.MaximumPortalCount * 2)
                throw new ArgumentOutOfRangeException(nameof(portalIds));
            if (!isReachable && (portalIds.Length != 0 || tap.Amplitude.Low != 0f ||
                tap.Amplitude.Mid != 0f || tap.Amplitude.High != 0f))
                throw new ArgumentException("An unreachable result must be silent and have no portals.");
            this.portalIds = (int[])portalIds.Clone();
            for (int i = 0; i < this.portalIds.Length; i++)
                if (this.portalIds[i] < 0) throw new ArgumentOutOfRangeException(nameof(portalIds));
            IsReachable = isReachable;
            Tap = tap;
        }

        public int GetPortalId(int index) => portalIds[index];

        /// <summary>Publish only this routed tap. No unmodelled wall reflections or room tail are added.</summary>
        public RoomAcousticResponse ToRoomResponse() => new RoomAcousticResponse(Tap, default, default,
            default, default, default, default, new AcousticBandValues(.05f, .05f, .05f), default(AcousticBandValues));
    }
}
