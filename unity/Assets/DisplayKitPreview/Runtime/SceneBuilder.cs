using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace DisplayKitPreview
{
    /// <summary>
    /// Turns a scene-JSON element node into UI Toolkit elements. Style keys are IStyle property names, so most are
    /// applied by reflection on IStyle; fonts and composite values are converted explicitly.
    /// </summary>
    public static class SceneBuilder
    {
        /// <summary>FontType name to font asset; null keeps the theme font.</summary>
        public static Func<string, FontAsset> FontResolver;

        public static VisualElement Build(Dictionary<string, object> node, string path)
        {
            string type = (string)node["type"];
            VisualElement ve = type == "text" ? new Label(node.TryGetValue("content", out object c) ? (string)c : "") : new VisualElement();
            ve.name = path;

            if (node.TryGetValue("style", out object styleObj) && styleObj is Dictionary<string, object> style)
                foreach (KeyValuePair<string, object> kv in style)
                {
                    try
                    {
                        Apply(ve.style, kv.Key, kv.Value);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[dk] {path}: cannot apply {kv.Key}: {ex.Message}");
                    }
                }

            if (node.TryGetValue("children", out object childrenObj) && childrenObj is List<object> children)
                foreach (object child in children)
                {
                    Dictionary<string, object> childNode = (Dictionary<string, object>)child;
                    ve.Add(Build(childNode, path + "/" + Convert.ToInt32(childNode["id"], CultureInfo.InvariantCulture)));
                }

            return ve;
        }

        private static void Apply(IStyle style, string key, object v)
        {
            switch (key)
            {
                case "font":
                    FontAsset asset = FontResolver?.Invoke((string)v);
                    if (asset != null)
                        style.unityFontDefinition = FontDefinition.FromSDFFont(asset);
                    return;
                case "textShadow":
                    if (v is string ks) { style.textShadow = new StyleTextShadow(Keyword(ks)); return; }
                    Dictionary<string, object> sh = (Dictionary<string, object>)v;
                    List<object> off = (List<object>)sh["offset"];
                    style.textShadow = new StyleTextShadow(new TextShadow
                    {
                        offset = new Vector2(F(off[0]), F(off[1])),
                        blurRadius = F(sh["blur"]),
                        color = ToColor(sh["color"]),
                    });
                    return;
                case "translate":
                    if (v is string kt) { style.translate = new StyleTranslate(Keyword(kt)); return; }
                    Dictionary<string, object> t = (Dictionary<string, object>)v;
                    style.translate = new StyleTranslate(new Translate(ToLength((string)t["x"]), ToLength((string)t["y"]), F(t["z"])));
                    return;
                case "scale":
                    if (v is string kc) { style.scale = new StyleScale(Keyword(kc)); return; }
                    List<object> sc = (List<object>)v;
                    style.scale = new StyleScale(new Scale(new Vector3(F(sc[0]), F(sc[1]), F(sc[2]))));
                    return;
                case "rotate":
                    if (v is string kr) { style.rotate = new StyleRotate(Keyword(kr)); return; }
                    style.rotate = new StyleRotate(new Rotate(new Angle(F(((Dictionary<string, object>)v)["angle"]), AngleUnit.Degree)));
                    return;
                case "transformOrigin":
                    if (v is string ko) { style.transformOrigin = new StyleTransformOrigin(Keyword(ko)); return; }
                    Dictionary<string, object> o = (Dictionary<string, object>)v;
                    style.transformOrigin = new StyleTransformOrigin(new TransformOrigin(ToLength((string)o["x"]), ToLength((string)o["y"]), F(o["z"])));
                    return;
            }

            PropertyInfo prop = typeof(IStyle).GetProperty(key);
            if (prop == null)
                throw new ArgumentException("not an IStyle property");

            Type pt = prop.PropertyType;
            object value;
            if (pt == typeof(StyleLength))
                value = v is string s ? StyleLengthOf(s) : new StyleLength(F(v));
            else if (pt == typeof(StyleFloat))
                value = v is string sf ? new StyleFloat(Keyword(sf)) : new StyleFloat(F(v));
            else if (pt == typeof(StyleColor))
                value = v is string scol ? new StyleColor(Keyword(scol)) : new StyleColor(ToColor(v));
            else if (pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(StyleEnum<>))
            {
                string name = (string)v;
                StyleKeyword kw = Keyword(name, false);
                value = kw != StyleKeyword.Undefined
                    ? Activator.CreateInstance(pt, kw)
                    : Activator.CreateInstance(pt, Enum.Parse(pt.GetGenericArguments()[0], name));
            }
            else
                throw new ArgumentException("unsupported style type " + pt.Name);

            prop.SetValue(style, value);
        }

        private static float F(object o) => Convert.ToSingle(o, CultureInfo.InvariantCulture);

        private static Color ToColor(object o)
        {
            List<object> a = (List<object>)o;
            return new Color(F(a[0]), F(a[1]), F(a[2]), F(a[3]));
        }

        private static StyleKeyword Keyword(string s, bool required = true)
        {
            switch (s)
            {
                case "auto": return StyleKeyword.Auto;
                case "none": return StyleKeyword.None;
                case "initial": return StyleKeyword.Initial;
                default:
                    if (required) throw new ArgumentException("unknown keyword " + s);
                    return StyleKeyword.Undefined;
            }
        }

        private static StyleLength StyleLengthOf(string s)
        {
            if (s == "initial") return new StyleLength(StyleKeyword.Initial);
            return new StyleLength(ToLength(s));
        }

        private static Length ToLength(string s)
        {
            if (s == "auto") return Length.Auto();
            if (s == "none") return Length.None();
            if (s.EndsWith("%"))
                return new Length(float.Parse(s.Substring(0, s.Length - 1), CultureInfo.InvariantCulture), LengthUnit.Percent);
            return new Length(float.Parse(s.EndsWith("px") ? s.Substring(0, s.Length - 2) : s, CultureInfo.InvariantCulture), LengthUnit.Pixel);
        }
    }
}
