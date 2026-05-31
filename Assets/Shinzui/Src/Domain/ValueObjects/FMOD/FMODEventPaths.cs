// THIS FILE IS AUTO-GENERATED. DO NOT EDIT MANUALLY.
// Generated at: 2026-06-01 03:44:44

using FMODUnity;

namespace Shinzui.Domain.ValueObjects.FMOD
{
    public readonly struct FMODEventPath
    {
        public EventReference Reference { get; }
        private FMODEventPath(string path) => Reference = RuntimeManager.PathToEventReference(path);

        public static readonly FMODEventPath TEST = new ("event:/Test");
    }
}
