using System;
using System.Collections.Generic;

namespace RoleIntroKit
{
    /// <summary>
    /// At most one intro per player. The owning plugin calls <see cref="Play"/> after a role change it made with
    /// <c>ShowIntroScreen</c> off, <see cref="Cancel"/> on the next role change, death or leave,
    /// <see cref="Clear"/> on round restart, and <see cref="Update"/> every frame (or from a 10–20 Hz coroutine).
    ///
    /// Keyed by connection ID, not ReferenceHub: hub equality is UnityEngine.Object equality, which the preview's
    /// stand-in hubs cannot evaluate and which is meaningless after a disconnect anyway.
    /// </summary>
    public sealed class RoleIntroDirector
    {
        private sealed class Entry
        {
            public RoleIntro Intro;
            public float Start;
            public bool HintShown;
        }

        private readonly Dictionary<int, Entry> _active = new Dictionary<int, Entry>();
        private readonly List<int> _done = new List<int>();
        private float _clock;

        public RoleIntroOptions Options { get; set; } = new RoleIntroOptions();

        /// <summary>
        /// Under <see cref="CjkMode.Omit"/>: show these lines as a hint for this connection (HsmAdapter lane,
        /// stable key). Raised once the card has faded in.
        /// </summary>
        public event Action<int, IReadOnlyList<RoleIntroLine>> FallbackHintShown;

        /// <summary>Remove that hint. Raised when the fade-out starts or the intro is cancelled.</summary>
        public event Action<int> FallbackHintHidden;

        public bool IsPlaying(int connectionId) => _active.ContainsKey(connectionId);

        /// <summary>Replaces any intro the player is still watching and starts this one.</summary>
        public RoleIntro Play(ReferenceHub viewer, int connectionId, RoleIntroData data)
        {
            Cancel(connectionId);
            RoleIntro intro = RoleIntro.Create(viewer, data, Options);
            _active[connectionId] = new Entry { Intro = intro, Start = _clock };
            return intro;
        }

        public void Cancel(int connectionId)
        {
            if (!_active.TryGetValue(connectionId, out Entry e))
                return;
            _active.Remove(connectionId);
            if (e.HintShown)
                FallbackHintHidden?.Invoke(connectionId);
            e.Intro.Destroy();
        }

        public void Clear()
        {
            foreach (int id in new List<int>(_active.Keys))
                Cancel(id);
            _clock = 0f;
        }

        public void Update(float deltaTime)
        {
            _clock += deltaTime;
            _done.Clear();
            foreach (KeyValuePair<int, Entry> pair in _active)
            {
                Entry e = pair.Value;
                bool alive = e.Intro.Tick(_clock - e.Start);
                if (e.Intro.FallbackVisible != e.HintShown)
                {
                    e.HintShown = e.Intro.FallbackVisible;
                    if (e.HintShown) FallbackHintShown?.Invoke(pair.Key, e.Intro.FallbackLines);
                    else FallbackHintHidden?.Invoke(pair.Key);
                }

                if (!alive)
                    _done.Add(pair.Key);
            }

            foreach (int id in _done)
                _active.Remove(id);
        }
    }
}
