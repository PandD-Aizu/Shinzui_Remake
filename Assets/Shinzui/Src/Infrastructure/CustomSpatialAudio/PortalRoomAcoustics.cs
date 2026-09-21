using System;
using Shinzui.Application.CustomSpatialAudio;

namespace Shinzui.Infrastructure.CustomSpatialAudio
{
    /// <summary>
    /// Bounded control-thread Dijkstra search through convex cells and portal centres. Each directed
    /// portal side is a state: keeping only one cost per room would discard alternative entry points
    /// and can choose the wrong route. Cost = geometric metres + lossWeight * -ln(mean transmission
    /// energy) at each portal. This is an explicit routing heuristic, not physical diffraction or a
    /// frequency-dependent loudest-path optimisation. All arrays/math stay off the audio callback.
    /// </summary>
    public static class PortalRoomAcoustics
    {
        public static PortalAcousticPath Calculate(PortalAcousticGraph graph, int sourceRoomId,
            AcousticVector3 source, int listenerRoomId, AcousticVector3 listener,
            AcousticVector3 listenerRight, float speedOfSound = 343f, float referenceDistance = 1f,
            float transmissionLossWeightMetres = 1f)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            int sourceRoom = graph.FindRoomIndex(sourceRoomId), listenerRoom = graph.FindRoomIndex(listenerRoomId);
            if (sourceRoom < 0 || !graph.GetRoom(sourceRoom).Bounds.Contains(source))
                throw new ArgumentOutOfRangeException(nameof(sourceRoomId), "Source must belong to an existing cell.");
            if (listenerRoom < 0 || !graph.GetRoom(listenerRoom).Bounds.Contains(listener))
                throw new ArgumentOutOfRangeException(nameof(listenerRoomId), "Listener must belong to an existing cell.");
            ValidateRange(speedOfSound, 100f, 1000f, nameof(speedOfSound));
            ValidateRange(referenceDistance, .01f, 100f, nameof(referenceDistance));
            ValidateRange(transmissionLossWeightMetres, 0f, 1000f, nameof(transmissionLossWeightMetres));
            double rightLength = Distance(listenerRight, default);
            if (!listenerRight.IsFinite || rightLength < .000001)
                throw new ArgumentException("Listener right axis must be finite and nonzero.", nameof(listenerRight));

            // Portal 2*i ends in room A, 2*i+1 in room B. Last state is the source.
            int start = graph.PortalCount * 2, stateCount = start + 1;
            var costs = new double[stateCount];
            var distances = new double[stateCount];
            var previous = new int[stateCount];
            var settled = new bool[stateCount];
            for (int i = 0; i < stateCount; i++) { costs[i] = double.PositiveInfinity; previous[i] = -1; }
            costs[start] = 0.0;
            double bestCost = double.PositiveInfinity, bestDistance = 0.0;
            int bestState = -1;
            for (int iteration = 0; iteration < stateCount; iteration++)
            {
                int current = -1;
                for (int i = 0; i < stateCount; i++)
                    if (!settled[i] && (current < 0 || costs[i] < costs[current])) current = i;
                if (current < 0 || double.IsPositiveInfinity(costs[current]) || costs[current] > bestCost) break;
                settled[current] = true;
                int roomId = StateRoom(graph, current, start, sourceRoomId);
                AcousticVector3 position = StatePosition(graph, current, start, source);
                if (roomId == listenerRoomId)
                {
                    double finalLength = Distance(position, listener);
                    double candidateCost = costs[current] + finalLength;
                    if (candidateCost < bestCost)
                    {
                        bestCost = candidateCost;
                        bestDistance = distances[current] + finalLength;
                        bestState = current;
                    }
                }
                for (int i = 0; i < graph.PortalCount; i++)
                {
                    AcousticPortal portal = graph.GetPortal(i);
                    if (!portal.IsOpen || (portal.RoomAId != roomId && portal.RoomBId != roomId)) continue;
                    int next = i * 2 + (portal.RoomAId == roomId ? 1 : 0);
                    if (settled[next]) continue;
                    double segment = Distance(position, portal.Centre);
                    double energy = portal.OpeningFraction *
                        ((double)portal.Transmission.Low + portal.Transmission.Mid + portal.Transmission.High) / 3.0;
                    double candidate = costs[current] + segment - transmissionLossWeightMetres * Math.Log(energy);
                    if (candidate >= costs[next]) continue;
                    costs[next] = candidate;
                    distances[next] = distances[current] + segment;
                    previous[next] = current;
                }
            }

            if (bestState < 0) return new PortalAcousticPath(false, default, Array.Empty<int>());
            int count = 0;
            for (int state = bestState; state != start; state = previous[state]) count++;
            var ids = new int[count];
            double low = 1.0, mid = 1.0, high = 1.0;
            for (int state = bestState, i = count - 1; state != start; state = previous[state], i--)
            {
                AcousticPortal portal = graph.GetPortal(state / 2);
                ids[i] = portal.Id;
                low *= Math.Sqrt((double)portal.OpeningFraction * portal.Transmission.Low);
                mid *= Math.Sqrt((double)portal.OpeningFraction * portal.Transmission.Mid);
                high *= Math.Sqrt((double)portal.OpeningFraction * portal.Transmission.High);
            }

            // When the listener is exactly on a portal, use the last nonzero incident segment.
            // This avoids an undefined pan jump while still reporting the full travel distance.
            AcousticVector3 apparent = StatePosition(graph, bestState, start, source);
            for (int state = bestState; Distance(apparent, listener) == 0.0 && state != start;)
            {
                state = previous[state];
                apparent = StatePosition(graph, state, start, source);
            }
            double x = (double)apparent.X - listener.X, y = (double)apparent.Y - listener.Y,
                z = (double)apparent.Z - listener.Z;
            double length = Math.Sqrt(x * x + y * y + z * z);
            double inverse = length > 0.0 ? 1.0 / length : 0.0;
            var direction = new AcousticVector3((float)(x * inverse), (float)(y * inverse), (float)(z * inverse));
            float pan = (float)Math.Max(-1.0, Math.Min(1.0,
                (x * listenerRight.X + y * listenerRight.Y + z * listenerRight.Z) * inverse / rightLength));
            double attenuation = referenceDistance / Math.Max(referenceDistance, bestDistance);
            var tap = new AcousticPathTap((float)(bestDistance / speedOfSound), (float)bestDistance,
                new AcousticBandValues((float)(low * attenuation), (float)(mid * attenuation),
                    (float)(high * attenuation)), direction, pan);
            return new PortalAcousticPath(true, tap, ids);
        }

        private static int StateRoom(PortalAcousticGraph graph, int state, int start, int sourceRoomId)
        {
            if (state == start) return sourceRoomId;
            AcousticPortal portal = graph.GetPortal(state / 2);
            return (state & 1) == 0 ? portal.RoomAId : portal.RoomBId;
        }

        private static AcousticVector3 StatePosition(PortalAcousticGraph graph, int state, int start,
            AcousticVector3 source) => state == start ? source : graph.GetPortal(state / 2).Centre;

        private static double Distance(AcousticVector3 a, AcousticVector3 b)
        {
            double x = (double)a.X - b.X, y = (double)a.Y - b.Y, z = (double)a.Z - b.Z;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        private static void ValidateRange(float value, float min, float max, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
