using System;

namespace Shinzui.Application.Title
{
    /// <summary>Credit timing independent of Unity frames, coroutines and UI visibility.</summary>
    public sealed class CreditPlayback
    {
        private readonly CreditContent _content;
        private float _startDelay;
        private float _endDelay;
        private bool _reachedEnd;

        public bool IsRunning { get; private set; }
        public float Position { get; private set; }

        public CreditPlayback(TitleScreenOptions options) => _content = options.Credits;

        public void Begin()
        {
            Position = 0f;
            _startDelay = _content.DelayBeforeStart;
            _endDelay = _content.DelayAfterEnd;
            _reachedEnd = false;
            IsRunning = true;
        }

        public void Stop() => IsRunning = false;

        /// <returns>True once, after scrolling and the end delay have both finished.</returns>
        public bool Tick(float deltaTime, float maxScrollPosition)
        {
            if (!IsRunning) return false;
            var remaining = Math.Max(0f, deltaTime);
            var wait = Math.Min(_startDelay, remaining);
            _startDelay -= wait;
            remaining -= wait;
            if (_startDelay > 0f) return false;

            if (!_reachedEnd)
            {
                var distance = Math.Max(0f, maxScrollPosition - Position);
                if (distance > 0f)
                {
                    if (_content.ScrollSpeed <= 0f) return false;
                    var scrollTime = Math.Min(remaining, distance / _content.ScrollSpeed);
                    Position += _content.ScrollSpeed * scrollTime;
                    remaining -= scrollTime;
                    if (Position < maxScrollPosition) return false;
                }
                _reachedEnd = true;
            }

            _endDelay -= remaining;
            if (_endDelay > 0f) return false;
            IsRunning = false;
            return true;
        }
    }
}
