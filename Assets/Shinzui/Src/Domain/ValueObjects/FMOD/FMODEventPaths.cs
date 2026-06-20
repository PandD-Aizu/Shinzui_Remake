// THIS FILE IS AUTO-GENERATED. DO NOT EDIT MANUALLY.
// Generated at: 2026-06-20 18:46:35

using FMODUnity;

namespace Shinzui.Domain.ValueObjects.FMOD
{
    public readonly struct FMODEventPath
    {
        public EventReference Reference { get; }
        private FMODEventPath(string path) => Reference = RuntimeManager.PathToEventReference(path);

        public static readonly FMODEventPath TEST = new ("event:/Test");
        public static readonly FMODEventPath FLASH_LIGHT_BUTTON_SE = new ("event:/FlashLightButtonSE");
        public static readonly FMODEventPath SNAPSHOT_REVERBERATION_TUNNEL = new ("snapshot:/Reverberation/Tunnel");
        public static readonly FMODEventPath STONE_BREAKING_SE = new ("event:/StoneBreakingSE");
    }
}
