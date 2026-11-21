using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using DisplayKit.Elements;
using Mirror;

namespace DisplayKit
{
    /// <summary>Server-side registry of live canvases.</summary>
    public static class DisplayKitManager
    {
        public static Dictionary<int, DisplayCanvas> ServerCanvases { get; } = new Dictionary<int, DisplayCanvas>();

        internal static RecyclableIdGenerator CanvasIds { get; } = new RecyclableIdGenerator();
    }

    /// <summary>Hands out small integer IDs and reuses returned ones.</summary>
    public sealed class RecyclableIdGenerator
    {
        private readonly SortedSet<int> _free = new SortedSet<int>();
        private int _next = 1;

        public int Next()
        {
            if (_free.Count == 0)
                return _next++;
            int id = _free.Min;
            _free.Remove(id);
            return id;
        }

        public void Return(int id)
        {
            if (id > 0 && id < _next)
                _free.Add(id);
        }
    }
}

namespace DisplayKit.Elements
{
    internal sealed class PreviewPlayerRecord
    {
        public int ConnectionId;
        public string Name;
        public ReferenceHub Hub;
        public NetworkConnectionToClient Connection;
    }

    /// <summary>
    /// Preview-only world: the players that exist and the mapping from a fake <see cref="ReferenceHub"/> to its
    /// connection ID. Driven by DisplayKit.Preview's scenario context.
    /// </summary>
    internal static class PreviewWorld
    {
        private static readonly ConditionalWeakTable<ReferenceHub, PreviewPlayerRecord> HubToPlayer = new ConditionalWeakTable<ReferenceHub, PreviewPlayerRecord>();

        public static readonly List<PreviewPlayerRecord> Players = new List<PreviewPlayerRecord>();

        public static void Reset()
        {
            foreach (DisplayCanvas canvas in new List<DisplayCanvas>(DisplayKitManager.ServerCanvases.Values))
                canvas.Destroy();
            DisplayKitManager.ServerCanvases.Clear();
            Players.Clear();
        }

        public static PreviewPlayerRecord AddPlayer(int connectionId, string name)
        {
            PreviewPlayerRecord existing = Players.Find(p => p.ConnectionId == connectionId);
            if (existing != null)
            {
                if (name != null)
                    existing.Name = name;
                return existing;
            }

            PreviewPlayerRecord record = new PreviewPlayerRecord
            {
                ConnectionId = connectionId,
                Name = name ?? $"Player{connectionId}",
                Hub = TryCreate<ReferenceHub>(null),
                Connection = TryCreate<NetworkConnectionToClient>(c => SetField(c, "connectionId", connectionId)),
            };
            if (!ReferenceEquals(record.Hub, null))
                HubToPlayer.Add(record.Hub, record);
            Players.Add(record);

            // Late joiners receive every globally spawned canvas.
            foreach (DisplayCanvas canvas in DisplayKitManager.ServerCanvases.Values)
                if (canvas.IsGloballySpawned)
                    canvas.AddObserver(connectionId);

            return record;
        }

        public static void RemovePlayer(int connectionId)
        {
            Players.RemoveAll(p => p.ConnectionId == connectionId);
            foreach (DisplayCanvas canvas in DisplayKitManager.ServerCanvases.Values)
                if (canvas.Observers.Remove(connectionId))
                    canvas.PendingStructureChanges++;
        }

        public static void EnsurePlayer(int connectionId) => AddPlayer(connectionId, null);

        public static int ConnectionIdOf(ReferenceHub hub)
        {
            // ReferenceEquals: UnityEngine.Object's == calls native code, which does not exist outside Unity.
            if (ReferenceEquals(hub, null))
                throw new ArgumentNullException(nameof(hub));
            if (HubToPlayer.TryGetValue(hub, out PreviewPlayerRecord record))
                return record.ConnectionId;
            throw new InvalidOperationException("This ReferenceHub was not created by the preview. Use PreviewContext.AddPlayer() to get one.");
        }

        public static NetworkConnectionToClient ConnectionOf(int connectionId)
        {
            return Players.Find(p => p.ConnectionId == connectionId)?.Connection;
        }

        // Game objects cannot be constructed outside Unity; an uninitialized instance is enough to act as an identity key.
        private static T TryCreate<T>(Action<T> init) where T : class
        {
            try
            {
                T obj = (T)FormatterServices.GetUninitializedObject(typeof(T));
                init?.Invoke(obj);
                return obj;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void SetField(object target, string name, object value)
        {
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
            }
        }
    }
}
