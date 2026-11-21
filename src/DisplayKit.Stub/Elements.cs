using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DisplayKit.Elements
{
    /// <summary>Non-generic view of any DisplayKit element.</summary>
    public interface IDisplayElement
    {
        int? Id { get; }
        DisplayCanvas Root { get; }
        IDisplayElement Parent { get; }
        List<IDisplayElement> Children { get; }
        VisualElement BaseElement { get; }
        bool IsLoaded { get; }
        bool HasChanges { get; }

        DisplayElement AddElement();
        DisplayText AddText(string text = "");
        void Remove();
    }

    /// <summary>
    /// Shared base of canvases, containers and text. <typeparamref name="T"/> is the UI Toolkit element the
    /// client builds; the server-side preview never constructs one.
    /// </summary>
    public abstract class Element<T> : IDisplayElement where T : VisualElement
    {
        internal readonly StyleStore Styles = new StyleStore();

        internal Element()
        {
            Background = new BackgroundData(Styles);
            Flex = new FlexData(Styles);
            Align = new AlignData(Styles);
            Size = new SizeData(Styles);
            Spacing = new SpacingData(Styles);
            Border = new BorderData(Styles);
            Position = new PositionData(Styles);
            Transform = new TransformData(Styles);
            Display = new DisplayData(Styles);
            Text = new TextData(Styles);
        }

        public int? Id { get; internal set; }
        public DisplayCanvas Root { get; internal set; }
        public IDisplayElement Parent { get; internal set; }
        public List<IDisplayElement> Children { get; } = new List<IDisplayElement>();

        /// <summary>Client-side UI Toolkit element. Always null in the preview (no Unity runtime).</summary>
        public T Base => null;

        public VisualElement BaseElement => Base;

        public bool IsLoaded => Root != null && Root.Observers.Count > 0;

        public bool HasChanges => Styles.PendingWrites > 0;

        public BackgroundData Background { get; }
        public FlexData Flex { get; }
        public AlignData Align { get; }
        public SizeData Size { get; }
        public SpacingData Spacing { get; }
        public BorderData Border { get; }
        public PositionData Position { get; }
        public TransformData Transform { get; }
        public DisplayData Display { get; }
        public TextData Text { get; }

        internal abstract string Kind { get; }

        internal bool Removed { get; private set; }

        public DisplayElement AddElement()
        {
            DisplayElement child = new DisplayElement();
            Attach(child);
            return child;
        }

        public DisplayText AddText(string text = "")
        {
            DisplayText child = new DisplayText { ContentValue = text ?? string.Empty };
            Attach(child);
            return child;
        }

        public virtual void Remove()
        {
            if (Removed)
                return;

            foreach (IDisplayElement child in Children.ToArray())
                child.Remove();

            Removed = true;
            if (Parent != null)
                Parent.Children.Remove(this);

            DisplayCanvas root = Root;
            if (root != null && Id.HasValue)
            {
                root.IdToElement.Remove(Id.Value);
                root.IdGenerator.Return(Id.Value);
                root.PendingStructureChanges++;
            }
        }

        private void Attach<TChild>(Element<TChild> child) where TChild : VisualElement
        {
            DisplayCanvas root = Root ?? this as DisplayCanvas;
            child.Root = root;
            child.Parent = this;
            Children.Add(child);
            if (root != null)
            {
                child.Id = root.IdGenerator.Next();
                root.IdToElement[child.Id.Value] = child;
                root.PendingStructureChanges++;
            }
        }
    }

    /// <summary>Container element.</summary>
    public sealed class DisplayElement : Element<VisualElement>
    {
        internal DisplayElement() { }

        internal override string Kind => "element";
    }

    /// <summary>Text element (a UI Toolkit Label on the client).</summary>
    public sealed class DisplayText : Element<Label>
    {
        internal string ContentValue = string.Empty;

        internal DisplayText() { }

        public string Content
        {
            get => ContentValue;
            set
            {
                ContentValue = value ?? string.Empty;
                Styles.PendingWrites++;
            }
        }

        internal override string Kind => "text";
    }
}
