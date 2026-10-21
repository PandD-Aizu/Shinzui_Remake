using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.View
{
    /// <summary>Isolates virtual flashlight copies without moving the player's actual lights during HDRP culling</summary>
    public partial class TunnelGateView
    {
        private readonly Dictionary<Light, Light>[] _hdrpLightCopies = new Dictionary<Light, Light>[MaxRecursionDepth];
        private readonly int[] _lightSlots = { -1, -1 };
        private static readonly bool[] UsedLightSlots = new bool[8];
        private const int PortalLightMask = 0x0ff00000; // Project layers 20..27, reserved by the migration utility.

        /// <summary>Disable previous copies before evaluating current visibility and live sources</summary>
        private void DisableHdrpLightCopies()
        {
            foreach (var level in _hdrpLightCopies)
                if (level != null) foreach (var light in level.Values) if (light != null) light.enabled = false;
        }

        /// <summary>Synchronize camera-specific copies of local lights at the virtual camera offset</summary>
        /// <param name="camera">Scheduled portal camera</param>
        /// <param name="level">Recursion index</param>
        private void PrepareHdrpLightCopies(Camera camera, int level)
        {
            int slot = _lightSlots[level];
            if (slot < 0)
            {
                slot = System.Array.FindIndex(UsedLightSlots, used => !used);
                if (slot < 0) return;
                _lightSlots[level] = slot;
                UsedLightSlots[slot] = true;
            }
            int layer = LayerMask.NameToLayer("HdrpPortalLight" + slot);
            if (layer < 0) return;
            camera.cullingMask = (camera.cullingMask & ~PortalLightMask) | (1 << layer);
            var copies = _hdrpLightCopies[level] ??= new Dictionary<Light, Light>();
            foreach (var source in _candidateLights)
            {
                if (source == null || !source.isActiveAndEnabled || source.type == LightType.Directional) continue;
                if (!copies.TryGetValue(source, out var copy) || copy == null)
                {
                    var host = new GameObject("[HDRP Portal Light]");
                    host.transform.SetParent(transform, false);
                    host.layer = layer;
                    copy = host.AddComponent<Light>();
                    var hdCopy = host.AddComponent<HDAdditionalLightData>();
                    if (source.TryGetComponent<HDAdditionalLightData>(out var hdSource)) hdSource.CopyTo(hdCopy);
                    copies[source] = copy;
                }
                copy.gameObject.layer = layer;
                copy.type = source.type;
                copy.range = source.range;
                copy.spotAngle = source.spotAngle;
                copy.innerSpotAngle = source.innerSpotAngle;
                copy.color = source.color;
                copy.useColorTemperature = source.useColorTemperature;
                copy.colorTemperature = source.colorTemperature;
                copy.intensity = source.intensity;
                copy.shadows = source.shadows;
                copy.cookie = source.cookie;
                copy.cullingMask = source.cullingMask;
                copy.transform.SetPositionAndRotation(source.transform.position + (targetGate.transform.position - transform.position) * (level + 1), source.transform.rotation);
                copy.enabled = true;
            }
        }

        /// <summary>Destroy only the light copies owned by this gate</summary>
        private void ReleaseHdrpLightCopies()
        {
            for (int i = 0; i < _hdrpLightCopies.Length; i++)
            {
                if (_lightSlots[i] >= 0) UsedLightSlots[_lightSlots[i]] = false;
                _lightSlots[i] = -1;
                if (_hdrpLightCopies[i] == null) continue;
                foreach (var light in _hdrpLightCopies[i].Values) if (light != null) Destroy(light.gameObject);
                _hdrpLightCopies[i].Clear();
            }
        }

        /// <summary>Return light layers when a portal leaves the camera frustum</summary>
        private void ReleaseHdrpLightSlots()
        {
            for (int i = 0; i < _lightSlots.Length; i++)
            {
                if (_lightSlots[i] >= 0) UsedLightSlots[_lightSlots[i]] = false;
                _lightSlots[i] = -1;
            }
        }
    }
}
