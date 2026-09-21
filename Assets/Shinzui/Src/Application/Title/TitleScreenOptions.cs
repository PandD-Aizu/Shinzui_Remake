using System;

namespace Shinzui.Application.Title
{
    public sealed class CreditContent
    {
        public string Text { get; }
        public float ScrollSpeed { get; }
        public float DelayBeforeStart { get; }
        public float DelayAfterEnd { get; }

        public CreditContent(string text, float scrollSpeed, float delayBeforeStart, float delayAfterEnd)
        {
            Text = text ?? string.Empty;
            ScrollSpeed = Math.Max(0f, scrollSpeed);
            DelayBeforeStart = Math.Max(0f, delayBeforeStart);
            DelayAfterEnd = Math.Max(0f, delayAfterEnd);
        }
    }

    public sealed class TitleScreenOptions
    {
        public string GameSceneAddress { get; }
        public CreditContent Credits { get; }
        public bool LoopCredits { get; }
        public bool AllowSkip { get; }
        public bool AutoStartCredits { get; }
        public string NextSceneName { get; }
        public float CreditsFadeOutDuration { get; }

        public TitleScreenOptions(string gameSceneAddress, CreditContent credits, bool loopCredits,
            bool allowSkip, bool autoStartCredits, string nextSceneName, float creditsFadeOutDuration)
        {
            GameSceneAddress = gameSceneAddress;
            Credits = credits ?? throw new ArgumentNullException(nameof(credits));
            LoopCredits = loopCredits;
            AllowSkip = allowSkip;
            AutoStartCredits = autoStartCredits;
            NextSceneName = nextSceneName;
            CreditsFadeOutDuration = Math.Max(0f, creditsFadeOutDuration);
        }
    }
}
