using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DisplayKit.Elements;
using DisplayKit.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace DisplayKit.Preview
{
    /// <summary>
    /// Serializes the live canvases into the scene JSON both renderers read. Style keys are UI Toolkit
    /// <c>IStyle</c> property names; lengths are "12px" / "50%" / "auto" / "none", colours are [r,g,b,a] in 0..1,
    /// enums are their UI Toolkit names, and an explicit keyword is "initial" / "auto" / "none".
    /// </summary>
    internal sealed class SceneRecorder
    {
        private readonly List<string> _frames = new List<string>();
        private readonly Dictionary<int, string> _playerNames = new Dictionary<int, string>();

        public PreviewContext Context { get; set; }

        public int FrameCount => _frames.Count;

        public bool HasUncapturedChanges()
        {
            return DisplayKitManager.ServerCanvases.Values.Any(c => c.PendingStructureChanges > 0 || Walk(c).Any(e => StylesOf(e).PendingWrites > 0));
        }

        public void Capture(string label, float time)
        {
            if (Context.Players.Count == 0 && PreviewWorld.Players.Count == 0)
                Context.AddPlayer("Viewer");

            int writes = 0;
            Json j = new Json();
            j.BeginObject();
            j.Prop("index", _frames.Count);
            j.Prop("label", label ?? $"frame {_frames.Count}");
            j.Prop("time", time);
            j.Key("players").BeginArray();
            foreach (PreviewPlayerRecord p in PreviewWorld.Players)
            {
                _playerNames[p.ConnectionId] = p.Name;
                j.Value(p.ConnectionId);
            }
            j.EndArray();

            j.Key("canvases").BeginArray();
            foreach (DisplayCanvas canvas in DisplayKitManager.ServerCanvases.Values.OrderBy(c => c.Id))
            {
                writes += canvas.PendingStructureChanges;
                canvas.PendingStructureChanges = 0;

                j.BeginObject();
                j.Prop("id", canvas.Id.Value);
                j.Key("sortOrder");
                if (canvas.SortOrder.HasValue) j.Value(canvas.SortOrder.Value); else j.Null();
                j.Prop("global", canvas.IsGloballySpawned);
                j.Prop("defaultVisibility", canvas.DefaultVisibility.ToString());
                j.Key("observers").BeginArray();
                foreach (KeyValuePair<int, ObserverStatus> kv in canvas.Observers.OrderBy(k => k.Key))
                {
                    j.BeginObject();
                    j.Prop("id", kv.Key);
                    j.Prop("visible", kv.Value.IsVisible);
                    j.EndObject();
                }
                j.EndArray();
                j.Key("root");
                writes += WriteElement(j, canvas);
                j.EndObject();
            }
            j.EndArray();
            j.Prop("writes", writes);
            j.EndObject();
            _frames.Add(j.ToString());
        }

        public string Build(string scenario, string description, string sourceAssembly, string error)
        {
            Json j = new Json();
            j.BeginObject();
            j.Prop("format", "displaykit-preview/1");
            j.Prop("scenario", scenario);
            j.Prop("description", description);
            j.Prop("assembly", sourceAssembly);
            j.Prop("generatedAt", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            j.Key("error");
            if (error != null) j.Value(error); else j.Null();
            j.Key("playerNames").BeginObject();
            foreach (KeyValuePair<int, string> kv in _playerNames.OrderBy(k => k.Key))
                j.Prop(kv.Key.ToString(CultureInfo.InvariantCulture), kv.Value);
            j.EndObject();
            j.Key("frames").Raw("[\n" + string.Join(",\n", _frames) + "\n]");
            j.EndObject();
            return j.ToString();
        }

        private static IEnumerable<IDisplayElement> Walk(IDisplayElement root)
        {
            yield return root;
            foreach (IDisplayElement child in root.Children)
                foreach (IDisplayElement d in Walk(child))
                    yield return d;
        }

        private static int WriteElement(Json j, IDisplayElement element)
        {
            StyleStore styles = StylesOf(element);
            int writes = styles.PendingWrites;
            styles.PendingWrites = 0;

            j.BeginObject();
            j.Prop("id", element is DisplayCanvas ? 0 : element.Id ?? 0);
            j.Prop("type", KindOf(element));
            if (element is DisplayText text)
                j.Prop("content", text.Content);

            j.Key("style").BeginObject();
            foreach (KeyValuePair<string, object> kv in styles.Values)
            {
                j.Key(kv.Key);
                WriteStyleValue(j, kv.Value);
            }
            j.EndObject();

            j.Key("children").BeginArray();
            foreach (IDisplayElement child in element.Children)
                writes += WriteElement(j, child);
            j.EndArray();
            j.EndObject();
            return writes;
        }

        private static StyleStore StylesOf(IDisplayElement e)
        {
            switch (e)
            {
                case DisplayCanvas c: return c.Styles;
                case DisplayElement d: return d.Styles;
                case DisplayText t: return t.Styles;
                default: throw new InvalidOperationException("Unknown element type " + e.GetType());
            }
        }

        private static string KindOf(IDisplayElement e)
        {
            switch (e)
            {
                case DisplayCanvas _: return "canvas";
                case DisplayText _: return "text";
                default: return "element";
            }
        }

        private static string Keyword(StyleKeyword k)
        {
            switch (k)
            {
                case StyleKeyword.Auto: return "auto";
                case StyleKeyword.None: return "none";
                case StyleKeyword.Initial: return "initial";
                default: return null;
            }
        }

        private static string LengthText(Length l)
        {
            if (l.IsAuto()) return "auto";
            if (l.IsNone()) return "none";
            string v = l.value.ToString("0.###", CultureInfo.InvariantCulture);
            return l.unit == LengthUnit.Percent ? v + "%" : v + "px";
        }

        private static void WriteColor(Json j, Color c)
        {
            j.BeginArray().Value(c.r).Value(c.g).Value(c.b).Value(c.a).EndArray();
        }

        private static void WriteStyleValue(Json j, object value)
        {
            switch (value)
            {
                case StyleLength l:
                    j.Value(Keyword(l.keyword) ?? LengthText(l.value));
                    break;
                case StyleFloat f:
                    if (Keyword(f.keyword) is string fk) j.Value(fk); else j.Value(f.value);
                    break;
                case StyleColor c:
                    if (Keyword(c.keyword) is string ck) j.Value(ck); else WriteColor(j, c.value);
                    break;
                case IStyleValueKeyword e:
                    j.Value(Keyword(e.Keyword) ?? e.ValueName);
                    break;
                case FontType font:
                    j.Value(font.ToString());
                    break;
                case StyleTextShadow s:
                    if (Keyword(s.keyword) is string sk) { j.Value(sk); break; }
                    j.BeginObject();
                    j.Key("offset").BeginArray().Value(s.value.offset.x).Value(s.value.offset.y).EndArray();
                    j.Prop("blur", s.value.blurRadius);
                    j.Key("color"); WriteColor(j, s.value.color);
                    j.EndObject();
                    break;
                case StyleTranslate t:
                    if (Keyword(t.keyword) is string tk) { j.Value(tk); break; }
                    j.BeginObject().Prop("x", LengthText(t.value.x)).Prop("y", LengthText(t.value.y)).Prop("z", t.value.z).EndObject();
                    break;
                case StyleScale s:
                    if (Keyword(s.keyword) is string sck) { j.Value(sck); break; }
                    j.BeginArray().Value(s.value.value.x).Value(s.value.value.y).Value(s.value.value.z).EndArray();
                    break;
                case StyleRotate r:
                    if (Keyword(r.keyword) is string rk) { j.Value(rk); break; }
                    // Rotate.axis is internal in UI Toolkit; DisplayKit UI rotates in the screen plane (Z).
                    j.BeginObject().Prop("angle", r.value.angle.ToDegrees()).EndObject();
                    break;
                case StyleTransformOrigin o:
                    if (Keyword(o.keyword) is string ok) { j.Value(ok); break; }
                    j.BeginObject().Prop("x", LengthText(o.value.x)).Prop("y", LengthText(o.value.y)).Prop("z", o.value.z).EndObject();
                    break;
                default:
                    j.Value(value.ToString());
                    break;
            }
        }
    }

    /// <summary>Minimal JSON writer (no package dependency in the runner).</summary>
    internal sealed class Json
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly Stack<bool> _first = new Stack<bool>();
        private bool _afterKey;

        public Json BeginObject() { Sep(); _sb.Append('{'); _first.Push(true); return this; }
        public Json EndObject() { _sb.Append('}'); _first.Pop(); return this; }
        public Json BeginArray() { Sep(); _sb.Append('['); _first.Push(true); return this; }
        public Json EndArray() { _sb.Append(']'); _first.Pop(); return this; }

        public Json Key(string key)
        {
            Sep();
            WriteString(key);
            _sb.Append(':');
            _afterKey = true;
            return this;
        }

        public Json Value(string s) { Sep(); if (s == null) _sb.Append("null"); else WriteString(s); return this; }
        public Json Value(int v) { Sep(); _sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
        public Json Value(bool v) { Sep(); _sb.Append(v ? "true" : "false"); return this; }
        public Json Value(float v)
        {
            Sep();
            _sb.Append(float.IsNaN(v) || float.IsInfinity(v) ? "0" : Math.Round(v, 4).ToString("R", CultureInfo.InvariantCulture));
            return this;
        }
        public Json Null() { Sep(); _sb.Append("null"); return this; }
        public Json Raw(string raw) { Sep(); _sb.Append(raw); return this; }

        public Json Prop(string key, string v) => Key(key).Value(v);
        public Json Prop(string key, int v) => Key(key).Value(v);
        public Json Prop(string key, float v) => Key(key).Value(v);
        public Json Prop(string key, bool v) => Key(key).Value(v);

        public override string ToString() => _sb.ToString();

        private void Sep()
        {
            if (_afterKey) { _afterKey = false; return; }
            if (_first.Count == 0) return;
            if (_first.Peek()) { _first.Pop(); _first.Push(false); }
            else _sb.Append(',');
        }

        private void WriteString(string s)
        {
            _sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) _sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }
    }
}
