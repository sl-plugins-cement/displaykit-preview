using System;
using System.Collections.Generic;
using DisplayKit.Elements;
using DisplayKit.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace RoleIntroKit
{
    /// <summary>
    /// One player's role intro: a per-player DisplayKit canvas that plays its entrance, holds, fades out and
    /// destroys itself. The caller drives it with <see cref="Tick"/> (seconds since <see cref="Create"/>); time is
    /// snapped to <see cref="RoleIntroTiming.StepRate"/> and a property is written only when its value changes,
    /// so a whole intro costs a few dozen small writes and nothing while it holds.
    ///
    /// No plugin or LabAPI dependency: it takes the viewer's <see cref="ReferenceHub"/> and a
    /// <see cref="RoleIntroData"/>. <see cref="RoleIntroDirector"/> owns the one-intro-per-player lifecycle.
    /// </summary>
    public sealed class RoleIntro
    {
        private readonly RoleIntroData _data;
        private readonly RoleIntroLayout _layout;
        private DisplayCanvas _canvas;

        private RoleIntro(DisplayCanvas canvas, RoleIntroData data, RoleIntroLayout layout, List<RoleIntroLine> fallback)
        {
            _canvas = canvas;
            _data = data;
            _layout = layout;
            FallbackLines = fallback;
        }

        public RoleIntroData Data => _data;

        /// <summary>Delay plus the whole presentation, fade-out included.</summary>
        public float Duration => _data.Timing.Delay + _data.Timing.Length(_data);

        /// <summary>
        /// Chinese lines left out under <see cref="CjkMode.Omit"/>, in display order, tagged with their role so the
        /// hint can size them. Empty when everything is drawn. The plugin shows them as one hint while
        /// <see cref="FallbackVisible"/> is true: centred at the intro's place for a full intro, left-aligned just
        /// above the card for an upgrade card.
        /// </summary>
        public IReadOnlyList<RoleIntroLine> FallbackLines { get; }

        /// <summary>True while the intro is settled (hints cannot fade, so they show only between the entrance and
        /// the fade-out).</summary>
        public bool FallbackVisible { get; private set; }

        public bool IsFinished => _canvas == null;

        public static RoleIntro Create(ReferenceHub viewer, RoleIntroData data, RoleIntroOptions options = null)
        {
            options ??= new RoleIntroOptions();
            data.Timing ??= RoleIntroTiming.Default;

            DisplayCanvas canvas = DisplayCanvas.Create();
            canvas.SortOrder = options.SortOrder;
            canvas.Flex.Grow = 1f;
            canvas.Text.Color = Color.white;

            List<RoleIntroLine> fallback = new List<RoleIntroLine>();
            FamilyStyle style = FamilyStyle.For(data.Family);
            RoleIntroLayout layout = data.Kind == RoleIntroKind.FullIntro ? new NativeLayout() : (RoleIntroLayout)new CardLayout();
            layout.Init(data, style);
            layout.Build(canvas, new TextGate(options.Cjk, fallback));
            layout.Apply(-1f);

            RoleIntro intro = new RoleIntro(canvas, data, layout, fallback);
            canvas.Spawn(viewer);
            return intro;
        }

        /// <summary>Advances the intro to <paramref name="elapsed"/> seconds. Returns false once it has destroyed its canvas.</summary>
        public bool Tick(float elapsed)
        {
            if (_canvas == null)
                return false;

            RoleIntroTiming t = _data.Timing;
            float local = elapsed - t.Delay;
            if (local >= t.Length(_data))
            {
                Destroy();
                return false;
            }

            // Snap to whole steps so ticks between steps write nothing.
            float rate = Mathf.Max(1f, t.StepRate);
            float stepped = local < 0f ? -1f : Mathf.Floor(local * rate + 0.0001f) / rate;
            (float show, float hide) = t.HintWindow(_data);
            FallbackVisible = FallbackLines.Count > 0 && local >= show && local < hide;
            _layout.Apply(stepped);
            return true;
        }

        /// <summary>Removes the canvas now (role changed again, player left, round restarted).</summary>
        public void Destroy()
        {
            FallbackVisible = false;
            _canvas?.Destroy();
            _canvas = null;
        }
    }

    /// <summary>Decides whether a line becomes a DisplayKit element or a fallback hint line.</summary>
    internal sealed class TextGate
    {
        private readonly CjkMode _mode;
        private readonly List<RoleIntroLine> _fallback;

        public TextGate(CjkMode mode, List<RoleIntroLine> fallback)
        {
            _mode = mode;
            _fallback = fallback;
        }

        /// <summary>Adds a text element, or records the line for the hint and returns null.</summary>
        public DisplayText Add(IDisplayElement parent, string text, RoleIntroLineKind kind)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            if (_mode == CjkMode.Omit && RoleIntroText.HasCjk(text))
            {
                _fallback.Add(new RoleIntroLine(kind, text));
                return null;
            }

            return parent.AddText(text);
        }
    }

    /// <summary>One presentation: builds the tree and maps stepped local time to style writes.</summary>
    internal abstract class RoleIntroLayout
    {
        private readonly Dictionary<(object, string), float> _last = new Dictionary<(object, string), float>();

        protected static readonly Color Ink = new Color(0.90f, 0.93f, 0.95f, 1f);
        protected static readonly Color InkDim = new Color(0.72f, 0.77f, 0.81f, 1f);
        protected static readonly Color Outline = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        protected static readonly TextShadow Shadow = new TextShadow { color = new Color(0f, 0f, 0f, 0.85f), offset = new Vector2(0f, 2f), blurRadius = 6f };

        protected RoleIntroData D;
        protected FamilyStyle S;
        protected RoleIntroTiming T;
        protected float Hold;

        public void Init(RoleIntroData data, FamilyStyle style)
        {
            D = data;
            S = style;
            T = data.Timing;
            Hold = T.ResolveHold(data);
        }

        public abstract void Build(DisplayCanvas canvas, TextGate gate);

        /// <param name="t">Stepped seconds since the delay ended; negative before the start.</param>
        public abstract void Apply(float t);

        protected static float Clamp01(float x) => Mathf.Clamp01(x);

        protected static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

        protected static float EaseIn(float x) => x * x;

        /// <summary>Ease-out with a small overshoot (peaks near 106 %).</summary>
        protected static float BackOut(float x)
        {
            const float c = 1.2f;
            float y = x - 1f;
            return 1f + (c + 1f) * y * y * y + c * y * y;
        }

        protected static float Phase(float t, float start, float length) => Clamp01((t - start) / Mathf.Max(length, 0.0001f));

        // ---- Write helpers: skip unchanged values (each DisplayKit property set is a network write) ----

        protected void Opacity<TE>(Element<TE> e, float v) where TE : VisualElement
        {
            if (e != null && Changed(e, "o", v)) e.Display.Opacity = v;
        }

        protected void Move<TE>(Element<TE> e, float x, float y) where TE : VisualElement
        {
            if (e != null && Changed(e, "t", x * 10000f + y)) e.Transform.Translate = new Translate(new Length(x), new Length(y));
        }

        protected void Scale<TE>(Element<TE> e, float x, float y) where TE : VisualElement
        {
            if (e != null && Changed(e, "s", x * 1000f + y)) e.Transform.Scale = new Scale(new Vector2(x, y));
        }

        private bool Changed(object e, string prop, float v)
        {
            if (_last.TryGetValue((e, prop), out float old) && Mathf.Abs(old - v) < 0.0005f)
                return false;
            _last[(e, prop)] = v;
            return true;
        }

        // ---- Shared pieces ----

        protected static void Style(DisplayText t, FontType font, float size, Color color, bool bold = false, bool italic = false, float outline = 0f)
        {
            if (t == null)
                return;
            t.Text.Font = font;
            t.Text.FontSize = size;
            t.Text.Color = color;
            t.Text.TextShadow = Shadow;
            if (outline > 0f)
            {
                t.Text.OutlineWidth = outline;
                t.Text.OutlineColor = Outline;
            }

            // Chinese lines: the bold/italic flag is what a CJK fallback font can synthesise (the Roboto face choice
            // does not reach the fallback). Open question for the beta.
            if (bold || italic)
                t.Text.FontStyle = bold && italic ? FontStyle.BoldAndItalic : bold ? FontStyle.Bold : FontStyle.Italic;
        }

        protected static DisplayElement Box(IDisplayElement parent, float w, float h, Color c)
        {
            DisplayElement e = parent.AddElement();
            e.Size.Width = w;
            e.Size.Height = h;
            e.Flex.Shrink = 0f;
            e.Background.Color = c;
            return e;
        }

        protected static DisplayElement Hollow(IDisplayElement parent, float size, Color c, float width = 1.5f)
        {
            DisplayElement e = parent.AddElement();
            e.Size.Width = size;
            e.Size.Height = size;
            e.Flex.Shrink = 0f;
            e.Border.Width = width;
            e.Border.Color = c;
            return e;
        }

        protected static void Diamond(DisplayElement e) => e.Transform.Rotate = new StyleRotate(new UnityEngine.UIElements.Rotate(new Angle(45f)));

        protected static DisplayElement Chip(IDisplayElement parent, string text, Color color, ChipShape shape, TextGate gate, float size)
        {
            if (string.IsNullOrEmpty(text) || gate == null)
                return null;
            DisplayElement chip = parent.AddElement();
            chip.Spacing.PaddingLeft = 8f;
            chip.Spacing.PaddingRight = 8f;
            chip.Align.AlignSelf = Align.Center;
            chip.Flex.Shrink = 0f;
            Color ink;
            switch (shape)
            {
                case ChipShape.Outline:
                    chip.Border.Width = 1.5f;
                    chip.Border.Color = RoleIntroText.WithAlpha(color, 0.95f);
                    chip.Background.Color = new Color(0f, 0f, 0f, 0.35f);
                    ink = RoleIntroText.Light(color, 0.3f);
                    break;
                case ChipShape.Pill:
                    chip.Background.Color = RoleIntroText.WithAlpha(color, 0.92f);
                    chip.Border.Radius = 12f;
                    chip.Spacing.PaddingLeft = 12f;
                    chip.Spacing.PaddingRight = 12f;
                    ink = Readable(color);
                    break;
                default:
                    chip.Background.Color = RoleIntroText.WithAlpha(color, 0.92f);
                    ink = Readable(color);
                    break;
            }

            DisplayText t = gate.Add(chip, text, RoleIntroLineKind.Badge);
            if (t == null)
            {
                chip.Remove();
                return null;
            }

            t.Text.Font = FontType.RobotoBold;
            t.Text.FontSize = size;
            t.Text.Color = ink;
            t.Text.LetterSpacing = 1.5f;
            return chip;
        }

        /// <summary>Near-black text on light chips, white on dark ones.</summary>
        protected static Color Readable(Color bg)
        {
            float l = 0.2126f * bg.r + 0.7152f * bg.g + 0.0722f * bg.b;
            return l > 0.5f ? new Color(0.04f, 0.05f, 0.06f, 1f) : Color.white;
        }

        /// <summary>
        /// A horizontal family rule beside the code line. Children are listed outer end first; the right-hand rule
        /// runs reversed so both read outward from the code. Grows outward from its inner end.
        /// </summary>
        protected static DisplayElement Rule(IDisplayElement parent, RuleMotif motif, Color c, bool left)
        {
            DisplayElement r = parent.AddElement();
            r.Flex.Direction = left ? FlexDirection.Row : FlexDirection.RowReverse;
            r.Align.AlignItems = Align.Center;
            r.Flex.Shrink = 0f;
            r.Transform.TransformOrigin = new TransformOrigin(Length.Percent(left ? 100f : 0f), Length.Percent(50f));
            Color line = RoleIntroText.WithAlpha(c, 0.9f);
            switch (motif)
            {
                case RuleMotif.Bracket:
                    Box(r, 110f, 2f, line);
                    Box(r, 2f, 14f, line);
                    break;
                case RuleMotif.Slash:
                    for (int i = 0; i < 3; i++)
                    {
                        DisplayElement s = Box(r, 3f, 14f, line);
                        s.Transform.Rotate = new StyleRotate(new UnityEngine.UIElements.Rotate(new Angle(28f)));
                        s.Spacing.MarginLeft = 3f;
                        s.Spacing.MarginRight = 3f;
                    }

                    Box(r, 92f, 1.5f, line).Spacing.MarginLeft = left ? 6f : 0f;
                    break;
                case RuleMotif.Node:
                    Hollow(r, 8f, line);
                    Box(r, 110f, 1.5f, line);
                    break;
                case RuleMotif.Diamond:
                {
                    DisplayElement outer = Hollow(r, 7f, line, 1.2f);
                    Diamond(outer);
                    DisplayElement bar = Box(r, 96f, 1f, line);
                    bar.Spacing.MarginLeft = 6f;
                    bar.Spacing.MarginRight = 6f;
                    Diamond(Box(r, 7f, 7f, line));
                    break;
                }

                case RuleMotif.Hairline:
                {
                    DisplayElement col = r.AddElement();
                    Box(col, 130f, 1f, line);
                    Box(col, 130f, 1f, line).Spacing.MarginTop = 3f;
                    break;
                }

                case RuleMotif.Dash:
                    for (int i = 0; i < 6; i++)
                        Box(r, 12f, 2f, line).Spacing.MarginLeft = i == 0 ? 0f : 5f;
                    break;
                default:
                    Box(r, 120f, 2f, line);
                    break;
            }

            return r;
        }

        /// <summary>The card's vertical colour bar for a family. Stretches to the card's height; grows from the bottom.</summary>
        protected static DisplayElement Bar(IDisplayElement parent, BarMotif motif, Color c)
        {
            DisplayElement bar = parent.AddElement();
            bar.Flex.Shrink = 0f;
            bar.Align.AlignItems = Align.Center;
            bar.Transform.TransformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(100f));
            switch (motif)
            {
                case BarMotif.Bracket:
                    bar.Size.Width = 12f;
                    bar.Border.LeftWidth = 4f;
                    bar.Border.TopWidth = 2f;
                    bar.Border.BottomWidth = 2f;
                    bar.Border.Color = c;
                    break;
                case BarMotif.Split:
                    bar.Size.Width = 5f;
                    for (int i = 0; i < 3; i++)
                    {
                        DisplayElement seg = Box(bar, 5f, 0f, c);
                        seg.Size.Height = StyleKeyword.Auto;
                        seg.Flex.Grow = i == 1 ? 2f : 1f;
                        seg.Spacing.MarginTop = i == 0 ? 0f : 4f;
                    }

                    break;
                case BarMotif.Node:
                    bar.Size.Width = 9f;
                    Hollow(bar, 9f, c, 2f);
                    Grow(Box(bar, 3f, 0f, c)).Spacing.MarginTop = 3f;
                    break;
                case BarMotif.Diamond:
                {
                    bar.Size.Width = 9f;
                    DisplayElement d = Box(bar, 7f, 7f, c);
                    Diamond(d);
                    d.Spacing.MarginTop = 2f;
                    Grow(Box(bar, 2f, 0f, c)).Spacing.MarginTop = 5f;
                    break;
                }

                case BarMotif.Double:
                    bar.Flex.Direction = FlexDirection.Row;
                    bar.Align.AlignItems = Align.Stretch;
                    Box(bar, 2f, 0f, c).Size.Height = StyleKeyword.Auto;
                    DisplayElement second = Box(bar, 2f, 0f, c);
                    second.Size.Height = StyleKeyword.Auto;
                    second.Spacing.MarginLeft = 3f;
                    break;
                case BarMotif.Dash:
                    bar.Size.Width = 5f;
                    for (int i = 0; i < 5; i++)
                    {
                        DisplayElement seg = Grow(Box(bar, 5f, 0f, c));
                        seg.Spacing.MarginTop = i == 0 ? 0f : 5f;
                    }

                    break;
                default:
                    bar.Size.Width = 5f;
                    bar.Background.Color = c;
                    break;
            }

            return bar;
        }

        private static DisplayElement Grow(DisplayElement e)
        {
            e.Size.Height = StyleKeyword.Auto;
            e.Flex.Grow = 1f;
            return e;
        }

        /// <summary>An element covering its parent, used to group anchored bands for one animation.</summary>
        protected static DisplayElement Fill(IDisplayElement parent)
        {
            DisplayElement fill = parent.AddElement();
            fill.Position.Position = Position.Absolute;
            fill.Position.Left = 0f;
            fill.Position.Right = 0f;
            fill.Position.Top = 0f;
            fill.Position.Bottom = 0f;
            return fill;
        }

        protected static DisplayElement Band(IDisplayElement parent, float topPercent)
        {
            DisplayElement row = parent.AddElement();
            row.Position.Position = Position.Absolute;
            row.Position.Top = Length.Percent(topPercent);
            row.Position.Left = 0f;
            row.Position.Right = 0f;
            row.Align.AlignItems = Align.Center;
            return row;
        }
    }

    /// <summary>
    /// Full intro: the native intro screen's composition with our content. Centred, no backing panel, outlined
    /// text, at the native anchors (fractions of screen height, measured from a 14.2 client):
    /// lead "你是" in the YOU ARE slot, the role name in its colour in the title slot, the family code row and
    /// the brief in the description slot, and the help line in the "Press F1" slot.
    /// Timing follows the native sequence: title pops in, details at 0.8 s, help at 1.8 s, everything dims to
    /// half at 2.1 s, fade out at the hold.
    /// </summary>
    internal sealed class NativeLayout : RoleIntroLayout
    {
        private DisplayElement _root;
        private DisplayElement _head;
        private DisplayElement _titleRow;
        private DisplayElement _detail;
        private DisplayElement _ruleL;
        private DisplayElement _ruleR;
        private DisplayElement _help;

        // Native title at 1080p (zh_Hans): about 150 px Noto Sans CJK SC Regular, 143 px glyph band, and no wider
        // than the native 80 % of the screen. A longer title wraps to a second line at full size, as the native
        // intro wraps long English names (CHAOS INSURGENCY / RIFLEMAN); only a single word too wide for one line
        // shrinks, down to 96 px.
        private const float TitleSize = 150f;
        private const float TitleMinSize = 96f;
        private const float TitleTracking = 0f;

        // Widest title line at 1080p. Assumes DisplayKit scales with the screen like the native UI; at constant
        // pixel size a 1280-wide screen clips the longest names.
        private const float TitleMaxWidth = 1536f;

        // One title line's row: the 143 px glyph band plus a 27 px gap to a wrapped line above it.
        private const float TitleLinePitch = 170f;

        // The last title line's glyphs end on the native y 534. CJK glyphs sit about 10 px below the centre of their
        // line box, so the 170 px row is centred 10 px above the native name centre (y 462): its bottom edge sits
        // 543 px above the bottom of a 1080 px screen, which keeps the 51 px native gap above the description clear
        // for the code row. Extra lines stack upward and push 你 是 up with them.
        private const float TitleBottomPercent = 543f / 1080f * 100f;

        /// <summary>
        /// The title's lines at <see cref="TitleSize"/>: one line when it fits <see cref="TitleMaxWidth"/>, otherwise
        /// the brand over the sub-role (蛇之手 / 奇术师), or two halves split at the space nearest the middle.
        /// </summary>
        private static string[] WrapTitle(string text, string brand, string role)
        {
            if (EstimateEms(text, TitleTracking / TitleSize) * TitleSize <= TitleMaxWidth)
                return new[] { text };
            if (!string.IsNullOrEmpty(brand) && !string.IsNullOrEmpty(role))
                return new[] { brand, role };

            int best = -1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ' && (best < 0 || Mathf.Abs(i - text.Length / 2f) < Mathf.Abs(best - text.Length / 2f)))
                    best = i;
            }

            return best < 0 ? new[] { text } : new[] { text.Substring(0, best), text.Substring(best + 1) };
        }

        /// <summary>Largest size, up to the native 150 px, at which every line fits <see cref="TitleMaxWidth"/>.</summary>
        private static float FitTitleSize(string[] lines)
        {
            float em = 0f;
            foreach (string line in lines)
                em = Mathf.Max(em, EstimateEms(line, TitleTracking / TitleSize));
            return em <= 0f ? TitleSize : Mathf.Clamp(TitleMaxWidth / em, TitleMinSize, TitleSize);
        }

        /// <summary>Approximate advance in ems for Roboto Bold with the CJK fallback, tracking included.</summary>
        private static float EstimateEms(string s, float trackingEm)
        {
            float w = 0f;
            foreach (char c in s ?? string.Empty)
                w += (RoleIntroText.HasCjk(c.ToString()) ? 1f : c == ' ' ? 0.26f : char.IsUpper(c) ? 0.64f : 0.55f) + trackingEm;
            return w;
        }

        public override void Build(DisplayCanvas canvas, TextGate gate)
        {
            _root = canvas.AddElement();
            _root.Position.Position = Position.Absolute;
            _root.Position.Left = 0f;
            _root.Position.Right = 0f;
            _root.Position.Top = 0f;
            _root.Position.Bottom = 0f;

            // Every block sits on its own native anchor (zh_Hans client, 1080p; see the capture reference
            // .references/scpsl-client-native-intros-14.2.9-zh_Hans/README.md), so a longer title or description
            // never pushes the others.
            _head = Fill(_root);
            _head.Transform.TransformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(38f));

            // Native class-name form: one string, one size, in the role colour: faction, a space, then the rank
            // (九尾狐 指挥官, 混沌分裂者 步枪手). Ours: the force's brand, a space, the sub-role (蛇之手 奇术师,
            // Nu-7 落锤 队长). SCPs are their designation (SCP-049-2).
            bool branded = !string.IsNullOrEmpty(D.Brand);
            string brand = branded ? RoleIntroText.Brand(D.Brand) : null;
            string titleText = branded
                ? brand + (string.IsNullOrEmpty(D.Title) ? string.Empty : " " + D.Title)
                : string.Format(S.TitleFormat, D.Title);
            string[] lines = WrapTitle(titleText, brand, branded ? D.Title : null);
            float heroSize = FitTitleSize(lines);
            float rise = (lines.Length - 1) * TitleLinePitch / 1080f * 100f;

            // Native 你 是: white, regular weight, about 56 px, glyphs at y 303-354 (centre 30.4 %); a wrapped title
            // lifts it by one line.
            DisplayText lead = gate.Add(Band(_head, 27.4f - rise), D.Lead, RoleIntroLineKind.Lead);
            Style(lead, FontType.RobotoRegular, 56f, Color.white, outline: 1.5f);

            // Native role name: regular weight, about 150 px, glyphs at y 392-534 (centre 42.8 %), in the role
            // colour. The column is bottom-anchored so its last line keeps that centre however many lines it has.
            DisplayElement titleBand = _head.AddElement();
            titleBand.Position.Position = Position.Absolute;
            titleBand.Position.Left = 0f;
            titleBand.Position.Right = 0f;
            titleBand.Position.Bottom = Length.Percent(TitleBottomPercent);
            titleBand.Align.AlignItems = Align.Center;
            _titleRow = titleBand.AddElement();
            _titleRow.Flex.Direction = FlexDirection.Column;
            _titleRow.Align.AlignItems = Align.Center;
            _titleRow.Transform.TransformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(50f));

            // Chinese renders in Noto Sans CJK SC Regular natively. A Latin-only title (SCP-049-2) uses the game's
            // heavy display face natively; Roboto Bold is the closest DisplayKit font.
            FontType face = RoleIntroText.HasCjk(titleText) ? FontType.RobotoRegular : FontType.RobotoBold;
            foreach (string line in lines)
            {
                DisplayElement slot = _titleRow.AddElement();
                slot.Size.Height = TitleLinePitch;
                slot.Align.JustifyContent = Justify.Center;
                slot.Align.AlignItems = Align.Center;
                DisplayText title = gate.Add(slot, line, RoleIntroLineKind.Title);
                Style(title, face, heroSize, D.Color, bold: face == FontType.RobotoBold, outline: 2f);
                if (title != null)
                    title.Text.LetterSpacing = TitleTracking;
            }

            _detail = Fill(_root);

            // The family code row fills the native 51 px gap between the name (ends y 534) and the description
            // (starts y 585); the native intro leaves it empty, so it stays small.
            DisplayElement row = Band(_detail, 50.4f);
            row = row.AddElement();
            row.Flex.Direction = FlexDirection.Row;
            row.Align.AlignItems = Align.Center;
            Color tint = D.FactionTint;
            _ruleL = Rule(row, S.Rule, tint, left: true);
            DisplayElement mid = row.AddElement();
            mid.Flex.Direction = FlexDirection.Row;
            mid.Align.AlignItems = Align.Center;
            mid.Spacing.MarginLeft = 16f;
            mid.Spacing.MarginRight = 16f;
            DisplayElement chip = Chip(mid, D.Badge, D.Color, S.Chip, gate, 13f);
            if (chip != null)
                chip.Spacing.MarginRight = 14f;
            DisplayText code = gate.Add(mid, S.JoinCode(D), RoleIntroLineKind.Code);
            Style(code, FontType.RobotoMonoMedium, 16f, RoleIntroText.Light(tint, 0.45f), outline: 1f);
            if (code != null)
                code.Text.LetterSpacing = S.CodeTracking;
            // The title already names the faction, so a branded intro shows only the Latin callsign here.
            string second = branded ? null : D.Faction;
            DisplayElement dot = null;
            if (code != null && !string.IsNullOrEmpty(second))
            {
                dot = Box(mid, 5f, 5f, RoleIntroText.WithAlpha(tint, 0.9f));
                dot.Spacing.MarginLeft = 12f;
                dot.Spacing.MarginRight = 8f;
            }

            DisplayText faction = gate.Add(mid, second, RoleIntroLineKind.Faction);
            Style(faction, FontType.RobotoRegular, 20f, RoleIntroText.Light(tint, 0.4f), outline: 1f);
            if (faction == null)
                dot?.Remove();
            _ruleR = Rule(row, S.Rule, tint, left: false);

            // Native description: white, regular weight, 28 px, 33 px line pitch, first line top at y 585 (54.2 %),
            // lines centred and top-anchored, so six lines still clear the help line.
            DisplayElement body = Band(_detail, 53.8f);
            DisplayText flavor = gate.Add(body, D.Flavor, RoleIntroLineKind.Flavor);
            Style(flavor, FontType.RobotoItalic, 26f, InkDim, italic: true, outline: 1f);
            DisplayText obj = gate.Add(body, D.Objective, RoleIntroLineKind.Objective);
            Style(obj, FontType.RobotoRegular, 28f, Color.white, outline: 1.2f);
            if (obj != null)
            {
                obj.Size.MaxWidth = 1100f;
                obj.Text.Align = TextAnchor.MiddleCenter;
            }

            // Native help: white, regular, about 48 px, glyphs at y 793-838 (centre 75.5 %); the caller's rich text
            // draws the key in bold #FFFF00, as native draws F1.
            _help = Band(_root, 72.9f);
            Style(gate.Add(_help, D.Help, RoleIntroLineKind.Help), FontType.RobotoRegular, 48f, Color.white, outline: 1.5f);
        }

        public override void Apply(float t)
        {
            if (t < 0f)
            {
                Opacity(_root, 0f);
                return;
            }

            // Whole intro: dims to DimTo, then fades out.
            float dim = Mathf.Lerp(1f, T.DimTo, Phase(t, T.DimAt, T.DimFade));
            float vanish = Phase(t, Hold, T.FadeOut);
            Opacity(_root, dim * (1f - vanish));

            // Title entrance by family.
            switch (S.Entrance)
            {
                case Entrance.Slam:
                {
                    float e = EaseOut(Phase(t, 0f, T.Punch));
                    Opacity(_head, Mathf.Min(1f, 0.35f + e));
                    float s = 1.15f - 0.15f * e;
                    Scale(_titleRow, s, s);
                    break;
                }

                case Entrance.Jolt:
                {
                    // Native pop with a sideways jolt on the title.
                    float e = EaseOut(Phase(t, 0f, T.Punch));
                    Opacity(_head, 1f);
                    Scale(_head, e, e);
                    Move(_titleRow, -18f * (1f - e), 0f);
                    break;
                }

                case Entrance.Rise:
                {
                    float e = EaseOut(Phase(t, 0f, 0.5f));
                    Opacity(_head, e);
                    Move(_head, 0f, 14f * (1f - e));
                    break;
                }

                case Entrance.Fade:
                    Opacity(_head, EaseOut(Phase(t, 0f, 0.5f)));
                    break;
                case Entrance.Bounce:
                {
                    float e = BackOut(Phase(t, 0f, T.Punch + 0.1f));
                    Opacity(_head, 1f);
                    Scale(_head, e, e);
                    break;
                }

                default:
                {
                    // Native: the Info block (YOU ARE + name) starts at scale 0 and pops to full size, no fade.
                    float e = EaseOut(Phase(t, 0f, T.Punch));
                    Opacity(_head, 1f);
                    Scale(_head, e, e);
                    break;
                }
            }

            float detail = Phase(t, T.DetailAt, T.LineFade);
            Opacity(_detail, detail);
            float grow = EaseOut(detail);
            Scale(_ruleL, grow, 1f);
            Scale(_ruleR, grow, 1f);
            Opacity(_help, Phase(t, T.HelpAt, T.LineFade));
        }
    }

    /// <summary>
    /// Upgrade card: lower-left card with the family's colour bar (left 5.5 %, bottom 17 %), away from the HSM HUD
    /// and the centre lanes. Badge and code, then the title, flavor and objective.
    /// </summary>
    internal sealed class CardLayout : RoleIntroLayout
    {
        private DisplayElement _card;
        private DisplayElement _bar;

        public override void Build(DisplayCanvas canvas, TextGate gate)
        {
            _card = canvas.AddElement();
            _card.Position.Position = Position.Absolute;
            _card.Position.Left = Length.Percent(5.5f);
            _card.Position.Bottom = Length.Percent(17f);
            _card.Flex.Direction = FlexDirection.Row;
            _card.Size.MaxWidth = 800f;

            _bar = Bar(_card, S.Bar, D.Color);

            DisplayElement body = _card.AddElement();
            body.Background.Color = new Color(0.03f, 0.05f, 0.07f, 0.64f);
            body.Spacing.PaddingLeft = 20f;
            body.Spacing.PaddingRight = 30f;
            body.Spacing.PaddingTop = 12f;
            body.Spacing.PaddingBottom = 16f;
            body.Spacing.MarginLeft = S.Bar == BarMotif.Solid ? 0f : 4f;
            body.Flex.Shrink = 1f;

            DisplayElement head = body.AddElement();
            head.Flex.Direction = FlexDirection.Row;
            head.Align.AlignItems = Align.Center;
            DisplayElement chip = Chip(head, D.Badge, D.Color, S.Chip, gate, 14f);
            if (chip != null)
                chip.Spacing.MarginRight = 12f;
            Color tint = D.FactionTint;
            DisplayText code = gate.Add(head, S.JoinCode(D), RoleIntroLineKind.Code);
            Style(code, FontType.RobotoMonoMedium, 17f, RoleIntroText.Light(tint, 0.45f));
            if (code != null)
                code.Text.LetterSpacing = S.CodeTracking;
            DisplayText faction = gate.Add(head, D.Faction, RoleIntroLineKind.Faction);
            Style(faction, FontType.RobotoMedium, 18f, RoleIntroText.Light(tint, 0.4f));
            if (faction != null)
                faction.Spacing.MarginLeft = code != null ? 14f : 0f;

            Style(gate.Add(body, string.Format(S.TitleFormat, D.Title), RoleIntroLineKind.Title), FontType.RobotoBold, 46f, Color.white, bold: true);
            Style(gate.Add(body, D.Flavor, RoleIntroLineKind.Flavor), FontType.RobotoItalic, 19f, InkDim, italic: true);
            Style(gate.Add(body, D.Objective, RoleIntroLineKind.Objective), FontType.RobotoMedium, 21f, Ink);
        }

        public override void Apply(float t)
        {
            if (t < 0f)
            {
                Opacity(_card, 0f);
                Scale(_bar, 1f, 0f);
                return;
            }

            float appear = Phase(t, 0f, T.FadeIn);
            float vanish = Phase(t, T.FadeIn + Hold, T.FadeOut);
            float a = EaseOut(appear);
            float o = a * (1f - vanish);
            float drift = -16f * EaseIn(vanish);
            switch (S.Entrance)
            {
                case Entrance.Rise:
                    Opacity(_card, o);
                    Move(_card, drift, 12f * (1f - a));
                    break;
                case Entrance.Fade:
                    Opacity(_card, o);
                    Move(_card, drift, 0f);
                    break;
                case Entrance.Jolt:
                    Opacity(_card, o);
                    Move(_card, -48f * (1f - BackOut(appear)) + drift, 0f);
                    break;
                default:
                    Opacity(_card, o);
                    Move(_card, -36f * (1f - a) + drift, 0f);
                    break;
            }

            // The bar leads: SCP's bracket closes first, the others grow with the card.
            float barSpeed = S.Entrance == Entrance.Slam ? 2.4f : 1.6f;
            Scale(_bar, 1f, Clamp01(appear * barSpeed));
        }
    }
}
