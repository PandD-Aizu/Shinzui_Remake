using System;
using System.Collections.Generic;
using Shinzui.Application.CustomSpatialAudio;
using Shinzui.Application.DTOs.Tunnel;

namespace Shinzui.Infrastructure.CustomSpatialAudio
{
    /// <summary>
    /// Converts the generator's metre-based, axis-aligned floor plan to convex acoustic cells.
    /// Long tunnels are split at <=75 m; only shared physical faces become portals. Warp pair IDs
    /// intentionally never participate. Coordinates are snapped to millimetres to join DTO seams.
    /// </summary>
    public sealed class GeneratedAcousticWorld
    {
        static readonly AcousticBandValues Transmission = new AcousticBandValues(1, 1, 1);
        static readonly AcousticWall Concrete = new AcousticWall(new AcousticBandValues(.12f, .22f, .35f));
        readonly PortalAcousticRoom[] rooms;
        readonly AcousticPortal[] portals;
        IReadOnlyList<AcousticDoorState> doors = Array.Empty<AcousticDoorState>();
        public PortalAcousticGraph Graph { get; private set; }
        public int Revision { get; private set; }
        public static RoomAcousticResponse Silence => new RoomAcousticResponse(default, default, default,
            default, default, default, default, new AcousticBandValues(.05f, .05f, .05f), 0);

        public GeneratedAcousticWorld(TunnelMapDto map)
        {
            if (map?.Dimensions == null) throw new ArgumentException("A generated floor plan is required.", nameof(map));
            var cells = new List<PortalAcousticRoom>();
            var d = map.Dimensions;
            foreach (var node in map.Tunnels)
                AddBox(cells, node.PositionX, node.PositionY, node.PositionZ, d.TunnelWidth, d.TunnelHeight, d.TunnelLength);
            foreach (var c in map.NormalCorridors)
                AddPassage(cells, c.CenterX, c.CenterY, c.CenterZ, c.DirX, c.DirZ, d.CorridorWidth, d.TunnelHeight, c.Length);
            foreach (var r in map.SmallRooms)
            {
                AddPassage(cells, r.CenterX, r.CenterY, r.CenterZ, r.DirX, r.DirZ, r.Width, d.TunnelHeight, r.RoomLength);
                float offset = (r.RoomLength + r.PassageLength) * .5f;
                foreach (int sign in new[] { -1, 1 })
                    AddPassage(cells, r.CenterX + sign * offset * r.DirX, r.CenterY,
                        r.CenterZ + sign * offset * r.DirZ, r.DirX, r.DirZ, d.CorridorWidth, d.TunnelHeight, r.PassageLength);
            }
            foreach (var w in map.WarpCorridors)
                AddPassage(cells, w.CenterX, w.CenterY, w.CenterZ, w.DirX, w.DirZ, d.CorridorWidth, d.TunnelHeight, w.Length);

            // Independently calculated endpoints can straddle a millimetre rounding boundary.
            rooms = WeldFaces(cells);
            var links = new List<AcousticPortal>();
            for (int a = 0; a < rooms.Length; a++)
            for (int b = a + 1; b < rooms.Length; b++)
                if (TrySharedFace(rooms[a].Bounds, rooms[b].Bounds, out var centre))
                    links.Add(new AcousticPortal(links.Count, a, b, centre, 1, Transmission));
            portals = links.ToArray();
            RebuildGraph();
        }

        public int FindRoom(AcousticVector3 point)
        {
            for (int i = 0; i < rooms.Length; i++) if (rooms[i].Bounds.Contains(point)) return i;
            return -1;
        }

        public void SetDoors(IReadOnlyList<AcousticDoorState> value) => doors = value ?? Array.Empty<AcousticDoorState>();

        public bool SetPortalOpening(int id, float opening)
        {
            if (id < 0 || id >= portals.Length) return false;
            var p = portals[id];
            if (p.OpeningFraction == opening) return false;
            portals[id] = new AcousticPortal(p.Id, p.RoomAId, p.RoomBId, p.Centre, opening, p.Transmission);
            RebuildGraph();
            return true;
        }

        public RoomAcousticResponse Calculate(AcousticVector3 source, AcousticVector3 listener, AcousticVector3 right)
        {
            int a = FindRoom(source), b = FindRoom(listener);
            // Unknown/outside geometry is silent, never a bounding-box shortcut through a wall.
            if (a < 0 || b < 0) return Silence;
            if (a == b) return Attenuate(RectangularRoomAcoustics.Calculate(Graph.GetRoom(a).Bounds, source, listener, right),
                DoorGain(source, listener, null));
            var path = PortalRoomAcoustics.Calculate(Graph, a, source, b, listener, right);
            return path.Tap.DelaySeconds <= Dsp.SpatialDspParameters.MaximumPathDelaySeconds ?
                Attenuate(path.ToRoomResponse(), DoorGain(source, listener, path)) : Silence;
        }

        float DoorGain(AcousticVector3 source, AcousticVector3 listener, PortalAcousticPath path)
        {
            float gain = 1;
            foreach (var door in doors)
            {
                var previous = source; bool crosses = false;
                if (path != null)
                    for (int i = 0; i < path.PortalCount; i++)
                    {
                        var next = portals[path.GetPortalId(i)].Centre;
                        crosses |= door.Crosses(previous, next); previous = next;
                    }
                crosses |= door.Crosses(previous, listener);
                if (crosses) gain *= (float)Math.Sqrt(door.OpeningFraction);
            }
            return gain;
        }
        static RoomAcousticResponse Attenuate(RoomAcousticResponse r, float gain)
        {
            if (gain == 1) return r;
            return new RoomAcousticResponse(Scale(r.Direct, gain), Scale(r.GetPath(1), gain), Scale(r.GetPath(2), gain),
                Scale(r.GetPath(3), gain), Scale(r.GetPath(4), gain), Scale(r.GetPath(5), gain), Scale(r.GetPath(6), gain),
                r.Rt60Seconds, new AcousticBandValues(r.LateBandGain.Low * gain, r.LateBandGain.Mid * gain, r.LateBandGain.High * gain));
        }
        static AcousticPathTap Scale(AcousticPathTap p, float gain) => new AcousticPathTap(p.DelaySeconds, p.DistanceMetres,
            new AcousticBandValues(p.Amplitude.Low * gain, p.Amplitude.Mid * gain, p.Amplitude.High * gain), p.Direction, p.Pan);

        void RebuildGraph()
        {
            var adjusted = new PortalAcousticRoom[rooms.Length];
            for (int i = 0; i < rooms.Length; i++)
            {
                var box = rooms[i].Bounds;
                var openings = new float[6];
                foreach (var p in portals)
                {
                    if (p.RoomAId != i && p.RoomBId != i) continue;
                    var other = rooms[p.RoomAId == i ? p.RoomBId : p.RoomAId].Bounds;
                    int face = p.Centre.X == box.Min.X ? 0 : p.Centre.X == box.Max.X ? 1 :
                        p.Centre.Z == box.Min.Z ? 4 : 5;
                    float sharedWidth = face < 2 ? Overlap(box.Min.Z, box.Max.Z, other.Min.Z, other.Max.Z) :
                        Overlap(box.Min.X, box.Max.X, other.Min.X, other.Max.X);
                    float faceWidth = face < 2 ? box.Max.Z - box.Min.Z : box.Max.X - box.Min.X;
                    openings[face] += p.OpeningFraction * sharedWidth / faceWidth;
                }
                var walls = new AcousticWall[6];
                for (int f = 0; f < 6; f++) walls[f] = new AcousticWall(Concrete.Absorption, Math.Min(1, openings[f]));
                adjusted[i] = new PortalAcousticRoom(i, new RectangularAcousticRoom(box.Min, box.Max,
                    walls[0], walls[1], walls[2], walls[3], walls[4], walls[5]));
            }
            Graph = new PortalAcousticGraph(adjusted, portals);
            Revision++;
        }

        static void AddPassage(List<PortalAcousticRoom> cells, float x, float y, float z,
            float dx, float dz, float width, float height, float length)
        {
            bool alongX = Math.Abs(dx) > .99f && Math.Abs(dz) < .01f;
            bool alongZ = Math.Abs(dz) > .99f && Math.Abs(dx) < .01f;
            if (!alongX && !alongZ) throw new ArgumentException("Generated acoustics requires axis-aligned passages.");
            AddBox(cells, x, y, z, alongX ? length : width, height, alongX ? width : length);
        }

        static void AddBox(List<PortalAcousticRoom> cells, float x, float y, float z, float width, float height, float length)
        {
            int nx = Math.Max(1, (int)Math.Ceiling(width / 75f)), nz = Math.Max(1, (int)Math.Ceiling(length / 75f));
            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                var min = new AcousticVector3(Snap(x - width / 2 + width * ix / nx), Snap(y), Snap(z - length / 2 + length * iz / nz));
                var max = new AcousticVector3(Snap(x - width / 2 + width * (ix + 1) / nx), Snap(y + height), Snap(z - length / 2 + length * (iz + 1) / nz));
                cells.Add(new PortalAcousticRoom(cells.Count, new RectangularAcousticRoom(min, max, Concrete)));
            }
        }

        static bool TrySharedFace(RectangularAcousticRoom a, RectangularAcousticRoom b, out AcousticVector3 centre)
        {
            centre = default;
            float y0 = Math.Max(a.Min.Y, b.Min.Y), y1 = Math.Min(a.Max.Y, b.Max.Y);
            if (y1 <= y0) return false;
            if ((a.Max.X == b.Min.X || b.Max.X == a.Min.X) && Overlap(a.Min.Z, a.Max.Z, b.Min.Z, b.Max.Z) > .01f)
            {
                centre = new AcousticVector3(a.Max.X == b.Min.X ? a.Max.X : a.Min.X, (y0 + y1) / 2,
                    (Math.Max(a.Min.Z, b.Min.Z) + Math.Min(a.Max.Z, b.Max.Z)) / 2);
                return true;
            }
            if ((a.Max.Z == b.Min.Z || b.Max.Z == a.Min.Z) && Overlap(a.Min.X, a.Max.X, b.Min.X, b.Max.X) > .01f)
            {
                centre = new AcousticVector3((Math.Max(a.Min.X, b.Min.X) + Math.Min(a.Max.X, b.Max.X)) / 2,
                    (y0 + y1) / 2, a.Max.Z == b.Min.Z ? a.Max.Z : a.Min.Z);
                return true;
            }
            return false;
        }
        static float Overlap(float a0, float a1, float b0, float b1) => Math.Min(a1, b1) - Math.Max(a0, b0);
        static float Snap(float value) => (float)(Math.Round(value * 1000.0) / 1000.0);
        static PortalAcousticRoom[] WeldFaces(List<PortalAcousticRoom> cells)
        {
            var x = new List<float>(); var y = new List<float>(); var z = new List<float>();
            foreach (var cell in cells)
            {
                x.Add(cell.Bounds.Min.X); x.Add(cell.Bounds.Max.X);
                y.Add(cell.Bounds.Min.Y); y.Add(cell.Bounds.Max.Y);
                z.Add(cell.Bounds.Min.Z); z.Add(cell.Bounds.Max.Z);
            }
            var xs = Canonical(x); var ys = Canonical(y); var zs = Canonical(z);
            var result = new PortalAcousticRoom[cells.Count];
            for (int i = 0; i < result.Length; i++)
            {
                var b = cells[i].Bounds;
                result[i] = new PortalAcousticRoom(i, new RectangularAcousticRoom(
                    new AcousticVector3(xs[b.Min.X], ys[b.Min.Y], zs[b.Min.Z]),
                    new AcousticVector3(xs[b.Max.X], ys[b.Max.Y], zs[b.Max.Z]), Concrete));
            }
            return result;
        }
        static Dictionary<float, float> Canonical(List<float> values)
        {
            values.Sort(); var result = new Dictionary<float, float>(); float anchor = values[0];
            foreach (float value in values)
            { if (value - anchor > .0021f) anchor = value; result[value] = anchor; }
            return result;
        }
    }
}
