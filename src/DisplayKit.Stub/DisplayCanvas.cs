using System;
using System.Collections.Generic;
using DisplayKit.Enums;
using Mirror;
using UnityEngine.UIElements;

namespace DisplayKit.Elements
{
    /// <summary>Per-observer state of a spawned canvas.</summary>
    public sealed class ObserverStatus
    {
        public bool IsVisible { get; set; }

        public bool IsConstructed { get; set; }
    }

    /// <summary>Root container; one UIDocument per canvas on the client.</summary>
    public sealed class DisplayCanvas : Element<VisualElement>
    {
        internal int PendingStructureChanges;
        internal bool Destroyed;

        private DisplayCanvas()
        {
            Root = this;
        }

        public int? SortOrder { get; set; }

        public CanvasVisibility DefaultVisibility { get; set; } = CanvasVisibility.Visible;

        public Dictionary<int, ObserverStatus> Observers { get; } = new Dictionary<int, ObserverStatus>();

        public Dictionary<int, IDisplayElement> IdToElement { get; } = new Dictionary<int, IDisplayElement>();

        public bool IsGloballySpawned { get; private set; }

        public RecyclableIdGenerator IdGenerator { get; } = new RecyclableIdGenerator();

        public event Action<NetworkConnectionToClient> OnPlayerCanvasConstructed;

        internal override string Kind => "canvas";

        public static DisplayCanvas Create()
        {
            DisplayCanvas canvas = new DisplayCanvas();
            canvas.Id = DisplayKitManager.CanvasIds.Next();
            canvas.IdToElement[0] = canvas;
            DisplayKitManager.ServerCanvases[canvas.Id.Value] = canvas;
            return canvas;
        }

        // ---- Spawning ----

        public void Spawn()
        {
            ThrowIfDestroyed();
            IsGloballySpawned = true;
            foreach (PreviewPlayerRecord player in PreviewWorld.Players)
                AddObserver(player.ConnectionId);
        }

        public void Spawn(int connectionId)
        {
            ThrowIfDestroyed();
            PreviewWorld.EnsurePlayer(connectionId);
            AddObserver(connectionId);
        }

        public void Spawn(ReferenceHub hub) => Spawn(PreviewWorld.ConnectionIdOf(hub));

        // ---- Visibility ----

        public void Show() => SetVisibility(true);

        public void Hide() => SetVisibility(false);

        public void Show(int connectionId) => SetVisibility(connectionId, true);

        public void Hide(int connectionId) => SetVisibility(connectionId, false);

        public void Show(ReferenceHub hub) => SetVisibility(PreviewWorld.ConnectionIdOf(hub), true);

        public void Hide(ReferenceHub hub) => SetVisibility(PreviewWorld.ConnectionIdOf(hub), false);

        public void SetVisibility(bool visible)
        {
            foreach (ObserverStatus status in Observers.Values)
                status.IsVisible = visible;
            PendingStructureChanges++;
        }

        public void SetVisibility(int connectionId, bool visible)
        {
            if (Observers.TryGetValue(connectionId, out ObserverStatus status))
            {
                status.IsVisible = visible;
                PendingStructureChanges++;
            }
        }

        // ---- Lifecycle ----

        public void Destroy()
        {
            if (Destroyed)
                return;
            Destroyed = true;
            Observers.Clear();
            DisplayKitManager.ServerCanvases.Remove(Id.Value);
            DisplayKitManager.CanvasIds.Return(Id.Value);
        }

        /// <summary>The canvas is the root; removing it destroys it.</summary>
        public override void Remove() => Destroy();

        internal void AddObserver(int connectionId)
        {
            if (Observers.ContainsKey(connectionId))
                return;

            Observers[connectionId] = new ObserverStatus
            {
                IsVisible = DefaultVisibility == CanvasVisibility.Visible,
                IsConstructed = true,
            };
            PendingStructureChanges++;

            NetworkConnectionToClient connection = PreviewWorld.ConnectionOf(connectionId);
            if (connection != null)
                OnPlayerCanvasConstructed?.Invoke(connection);
        }

        private void ThrowIfDestroyed()
        {
            if (Destroyed)
                throw new InvalidOperationException("DisplayCanvas was destroyed; create a new canvas instead of reusing it.");
        }
    }
}
