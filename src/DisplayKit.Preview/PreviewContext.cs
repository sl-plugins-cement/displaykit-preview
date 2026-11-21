using System;
using System.Collections.Generic;
using DisplayKit.Elements;

namespace DisplayKit.Preview
{
    /// <summary>Marks a <c>public static void Name(PreviewContext ctx)</c> method as a preview scenario.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DisplayKitScenarioAttribute : Attribute
    {
        public DisplayKitScenarioAttribute(string name = null) => Name = name;

        /// <summary>Scenario name used on the command line and in the output file name; defaults to the method name.</summary>
        public string Name { get; }

        public string Description { get; set; }
    }

    /// <summary>A preview player: its connection ID and a stand-in <see cref="ReferenceHub"/> for <c>Spawn(hub)</c>.</summary>
    public sealed class PreviewPlayer
    {
        internal PreviewPlayer(int connectionId, string name, ReferenceHub hub)
        {
            ConnectionId = connectionId;
            Name = name;
            Hub = hub;
        }

        public int ConnectionId { get; }

        public string Name { get; }

        /// <summary>
        /// Identity-only ReferenceHub: DisplayKit calls accept it, but reading game state from it throws.
        /// Plugin UI code should take the values it displays as arguments.
        /// </summary>
        public ReferenceHub Hub { get; }

        public static implicit operator ReferenceHub(PreviewPlayer player) => player.Hub;

        public static implicit operator int(PreviewPlayer player) => player.ConnectionId;
    }

    /// <summary>Drives one scenario: players joining/leaving and frame snapshots.</summary>
    public sealed class PreviewContext
    {
        private readonly List<PreviewPlayer> _players = new List<PreviewPlayer>();
        private int _nextId = 1;

        internal PreviewContext(SceneRecorder recorder) => Recorder = recorder;

        internal SceneRecorder Recorder { get; }

        public IReadOnlyList<PreviewPlayer> Players => _players;

        /// <summary>Simulated seconds since the scenario started; labels frames and is free for scenarios to advance.</summary>
        public float Time { get; set; }

        /// <summary>Adds a player. Globally spawned canvases reach it like a late joiner.</summary>
        public PreviewPlayer AddPlayer(string name = null)
        {
            int id = _nextId++;
            PreviewPlayerRecord record = PreviewWorld.AddPlayer(id, name);
            PreviewPlayer player = new PreviewPlayer(record.ConnectionId, record.Name, record.Hub);
            _players.Add(player);
            return player;
        }

        /// <summary>Removes the player from every canvas's observers (it does not destroy their canvases).</summary>
        public void RemovePlayer(PreviewPlayer player)
        {
            PreviewWorld.RemovePlayer(player.ConnectionId);
            _players.Remove(player);
        }

        /// <summary>Records the current state of every live canvas as a frame.</summary>
        public void Frame(string label = null) => Recorder.Capture(label, Time);

        /// <summary>Advances <see cref="Time"/> and records a frame.</summary>
        public void Frame(float advanceSeconds, string label)
        {
            Time += advanceSeconds;
            Frame(label);
        }
    }
}
