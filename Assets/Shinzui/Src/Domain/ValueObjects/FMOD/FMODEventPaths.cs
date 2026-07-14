// THIS FILE IS AUTO-GENERATED. DO NOT EDIT MANUALLY.
// Generated at: 2026-07-15 00:58:02

using FMODUnity;

namespace Shinzui.Domain.ValueObjects.FMOD
{
    public readonly struct FMODEventPath
    {
        public EventReference Reference { get; }
        private FMODEventPath(string path) => Reference = RuntimeManager.PathToEventReference(path);

        public static readonly FMODEventPath TEST = new ("event:/Test");
        public static readonly FMODEventPath SNAPSHOT_REVERBERATION_TUNNEL = new ("snapshot:/Reverberation/Tunnel");
        public static readonly FMODEventPath SE_ON_ENEMY_FOUND = new ("event:/SE/OnEnemyFound");
        public static readonly FMODEventPath SE_ON_DEAD = new ("event:/SE/OnDead");
        public static readonly FMODEventPath SE_TINNITUS = new ("event:/SE/Tinnitus");
        public static readonly FMODEventPath BGM_TUNNEL_AMBIENT = new ("event:/BGM/TunnelAmbient");
        public static readonly FMODEventPath SE_FLASH_LIGHT_BUTTON_SE = new ("event:/SE/FlashLightButtonSE");
        public static readonly FMODEventPath SE_STONE_BREAKING_SE = new ("event:/SE/StoneBreakingSE");
    }
}
