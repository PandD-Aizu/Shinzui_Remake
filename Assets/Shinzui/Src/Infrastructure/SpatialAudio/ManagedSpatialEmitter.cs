using FMOD.Studio;
using FMODUnity;

namespace Shinzui.Infrastructure.SpatialAudio
{
    // Steam Audio's official bridge discovers StudioEventEmitter.EventInstance.
    // This subclass supplies a service-owned instance without automatic triggers or transform attachment.
    public sealed class ManagedSpatialEmitter : StudioEventEmitter
    {
        public void Bind(EventInstance value) { instance = value; }
        public void Unbind() { instance.clearHandle(); }
        protected override void Start() { }
        protected override void HandleGameEvent(EmitterGameEvent gameEvent) { }
        protected override void OnDestroy()
        {
            // Fallback for external scene/object destruction before the owner has disposed.
            if (instance.isValid()) { instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); instance.release(); instance.clearHandle(); }
        }
    }
}
