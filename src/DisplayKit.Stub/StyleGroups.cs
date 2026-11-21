using DisplayKit.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace DisplayKit.Elements
{
    public sealed class BackgroundData : StyleData
    {
        internal BackgroundData(StyleStore s) : base(s) { }

        public StyleColor Color { get => GetColor("backgroundColor"); set => Store.Set("backgroundColor", value); }
    }

    public sealed class FlexData : StyleData
    {
        internal FlexData(StyleStore s) : base(s) { }

        public StyleFloat Grow { get => GetFloat("flexGrow"); set => Store.Set("flexGrow", value); }
        public StyleFloat Shrink { get => GetFloat("flexShrink"); set => Store.Set("flexShrink", value); }
        public StyleLength Basis { get => GetLength("flexBasis"); set => Store.Set("flexBasis", value); }
        public StyleEnum<FlexDirection> Direction { get => GetEnum<FlexDirection>("flexDirection"); set => SetEnum("flexDirection", value); }
        public StyleEnum<Wrap> Wrap { get => GetEnum<Wrap>("flexWrap"); set => SetEnum("flexWrap", value); }
    }

    public sealed class AlignData : StyleData
    {
        internal AlignData(StyleStore s) : base(s) { }

        public StyleEnum<Align> AlignItems { get => GetEnum<Align>("alignItems"); set => SetEnum("alignItems", value); }
        public StyleEnum<Justify> JustifyContent { get => GetEnum<Justify>("justifyContent"); set => SetEnum("justifyContent", value); }
        public StyleEnum<Align> AlignSelf { get => GetEnum<Align>("alignSelf"); set => SetEnum("alignSelf", value); }
        public StyleEnum<Align> AlignContent { get => GetEnum<Align>("alignContent"); set => SetEnum("alignContent", value); }
    }

    public sealed class SizeData : StyleData
    {
        internal SizeData(StyleStore s) : base(s) { }

        public StyleLength Width { get => GetLength("width"); set => Store.Set("width", value); }
        public StyleLength Height { get => GetLength("height"); set => Store.Set("height", value); }
        public StyleLength MinWidth { get => GetLength("minWidth"); set => Store.Set("minWidth", value); }
        public StyleLength MinHeight { get => GetLength("minHeight"); set => Store.Set("minHeight", value); }
        public StyleLength MaxWidth { get => GetLength("maxWidth"); set => Store.Set("maxWidth", value); }
        public StyleLength MaxHeight { get => GetLength("maxHeight"); set => Store.Set("maxHeight", value); }
    }

    public sealed class SpacingData : StyleData
    {
        internal SpacingData(StyleStore s) : base(s) { }

        public StyleLength MarginTop { get => GetLength("marginTop"); set => Store.Set("marginTop", value); }
        public StyleLength MarginBottom { get => GetLength("marginBottom"); set => Store.Set("marginBottom", value); }
        public StyleLength MarginLeft { get => GetLength("marginLeft"); set => Store.Set("marginLeft", value); }
        public StyleLength MarginRight { get => GetLength("marginRight"); set => Store.Set("marginRight", value); }
        public StyleLength PaddingTop { get => GetLength("paddingTop"); set => Store.Set("paddingTop", value); }
        public StyleLength PaddingBottom { get => GetLength("paddingBottom"); set => Store.Set("paddingBottom", value); }
        public StyleLength PaddingLeft { get => GetLength("paddingLeft"); set => Store.Set("paddingLeft", value); }
        public StyleLength PaddingRight { get => GetLength("paddingRight"); set => Store.Set("paddingRight", value); }

        // Guess: used by the wiki's Element Reference examples (`Spacing.Padding = 15f`) but missing from the
        // Style Properties table. Sets all four sides like Border.Width does.
        public StyleLength Padding
        {
            get => PaddingTop;
            set { PaddingTop = value; PaddingBottom = value; PaddingLeft = value; PaddingRight = value; }
        }
    }

    public sealed class BorderData : StyleData
    {
        internal BorderData(StyleStore s) : base(s) { }

        public StyleColor Color
        {
            get => TopColor;
            set { TopColor = value; BottomColor = value; LeftColor = value; RightColor = value; }
        }

        public StyleColor TopColor { get => GetColor("borderTopColor"); set => Store.Set("borderTopColor", value); }
        public StyleColor BottomColor { get => GetColor("borderBottomColor"); set => Store.Set("borderBottomColor", value); }
        public StyleColor LeftColor { get => GetColor("borderLeftColor"); set => Store.Set("borderLeftColor", value); }
        public StyleColor RightColor { get => GetColor("borderRightColor"); set => Store.Set("borderRightColor", value); }

        public StyleFloat Width
        {
            get => TopWidth;
            set { TopWidth = value; BottomWidth = value; LeftWidth = value; RightWidth = value; }
        }

        public StyleFloat TopWidth { get => GetFloat("borderTopWidth"); set => Store.Set("borderTopWidth", value); }
        public StyleFloat BottomWidth { get => GetFloat("borderBottomWidth"); set => Store.Set("borderBottomWidth", value); }
        public StyleFloat LeftWidth { get => GetFloat("borderLeftWidth"); set => Store.Set("borderLeftWidth", value); }
        public StyleFloat RightWidth { get => GetFloat("borderRightWidth"); set => Store.Set("borderRightWidth", value); }

        public StyleLength Radius
        {
            get => TopLeftRadius;
            set { TopLeftRadius = value; TopRightRadius = value; BottomLeftRadius = value; BottomRightRadius = value; }
        }

        public StyleLength TopLeftRadius { get => GetLength("borderTopLeftRadius"); set => Store.Set("borderTopLeftRadius", value); }
        public StyleLength TopRightRadius { get => GetLength("borderTopRightRadius"); set => Store.Set("borderTopRightRadius", value); }
        public StyleLength BottomLeftRadius { get => GetLength("borderBottomLeftRadius"); set => Store.Set("borderBottomLeftRadius", value); }
        public StyleLength BottomRightRadius { get => GetLength("borderBottomRightRadius"); set => Store.Set("borderBottomRightRadius", value); }
    }

    public sealed class PositionData : StyleData
    {
        internal PositionData(StyleStore s) : base(s) { }

        public StyleEnum<UnityEngine.UIElements.Position> Position
        {
            get => GetEnum<UnityEngine.UIElements.Position>("position");
            set => SetEnum("position", value);
        }

        public StyleLength Top { get => GetLength("top"); set => Store.Set("top", value); }
        public StyleLength Bottom { get => GetLength("bottom"); set => Store.Set("bottom", value); }
        public StyleLength Left { get => GetLength("left"); set => Store.Set("left", value); }
        public StyleLength Right { get => GetLength("right"); set => Store.Set("right", value); }
    }

    public sealed class TransformData : StyleData
    {
        internal TransformData(StyleStore s) : base(s) { }

        public StyleTranslate Translate { get => Store.Values.ContainsKey("translate") ? Store.Get<StyleTranslate>("translate") : new StyleTranslate(StyleKeyword.Null); set => Store.Set("translate", value); }
        public StyleScale Scale { get => Store.Values.ContainsKey("scale") ? Store.Get<StyleScale>("scale") : new StyleScale(StyleKeyword.Null); set => Store.Set("scale", value); }
        public StyleRotate Rotate { get => Store.Values.ContainsKey("rotate") ? Store.Get<StyleRotate>("rotate") : new StyleRotate(StyleKeyword.Null); set => Store.Set("rotate", value); }
        public StyleTransformOrigin TransformOrigin { get => Store.Values.ContainsKey("transformOrigin") ? Store.Get<StyleTransformOrigin>("transformOrigin") : new StyleTransformOrigin(StyleKeyword.Null); set => Store.Set("transformOrigin", value); }
    }

    public sealed class DisplayData : StyleData
    {
        internal DisplayData(StyleStore s) : base(s) { }

        public StyleEnum<DisplayStyle> Display { get => GetEnum<DisplayStyle>("display"); set => SetEnum("display", value); }
        public StyleEnum<Visibility> Visibility { get => GetEnum<Visibility>("visibility"); set => SetEnum("visibility", value); }
        public StyleFloat Opacity { get => GetFloat("opacity"); set => Store.Set("opacity", value); }
        public StyleEnum<Overflow> Overflow { get => GetEnum<Overflow>("overflow"); set => SetEnum("overflow", value); }
    }

    /// <summary>
    /// Text styling. UI Toolkit inherits these properties, so setting them on a canvas or container sets the
    /// default for the text below it (the wiki's `canvas.Text.Color = Color.white` pattern).
    /// </summary>
    public sealed class TextData : StyleData
    {
        internal TextData(StyleStore s) : base(s) { }

        public FontType Font { get => Store.Get<FontType>("font"); set => Store.Set("font", value); }
        public StyleEnum<FontStyle> FontStyle { get => GetEnum<FontStyle>("unityFontStyleAndWeight"); set => SetEnum("unityFontStyleAndWeight", value); }
        public StyleLength FontSize { get => GetLength("fontSize"); set => Store.Set("fontSize", value); }
        public StyleColor Color { get => GetColor("color"); set => Store.Set("color", value); }
        public StyleEnum<TextAnchor> Align { get => GetEnum<TextAnchor>("unityTextAlign"); set => SetEnum("unityTextAlign", value); }
        public StyleEnum<WhiteSpace> Wrap { get => GetEnum<WhiteSpace>("whiteSpace"); set => SetEnum("whiteSpace", value); }
        public StyleEnum<TextOverflow> Overflow { get => GetEnum<TextOverflow>("textOverflow"); set => SetEnum("textOverflow", value); }
        public StyleLength LetterSpacing { get => GetLength("letterSpacing"); set => Store.Set("letterSpacing", value); }
        public StyleLength WordSpacing { get => GetLength("wordSpacing"); set => Store.Set("wordSpacing", value); }
        public StyleLength ParagraphSpacing { get => GetLength("unityParagraphSpacing"); set => Store.Set("unityParagraphSpacing", value); }
        public StyleFloat OutlineWidth { get => GetFloat("unityTextOutlineWidth"); set => Store.Set("unityTextOutlineWidth", value); }
        public StyleColor OutlineColor { get => GetColor("unityTextOutlineColor"); set => Store.Set("unityTextOutlineColor", value); }
        public StyleTextShadow TextShadow { get => Store.Values.ContainsKey("textShadow") ? Store.Get<StyleTextShadow>("textShadow") : new StyleTextShadow(StyleKeyword.Null); set => Store.Set("textShadow", value); }
    }
}
