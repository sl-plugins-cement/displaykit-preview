using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DisplayKit.Elements
{
    /// <summary>
    /// Preview-only: the style values a plugin assigned to one element, keyed by the UI Toolkit
    /// <c>IStyle</c> property name (camelCase) so both renderers map them one-to-one.
    /// </summary>
    internal sealed class StyleStore
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        /// <summary>Property assignments since the last recorded frame (each one is a network write in the real build).</summary>
        public int PendingWrites;

        public IReadOnlyDictionary<string, object> Values => _values;

        public T Get<T>(string key)
        {
            return _values.TryGetValue(key, out object value) && value is T typed ? typed : default;
        }

        public void Set(string key, object value)
        {
            PendingWrites++;
            if (IsUnset(value))
                _values.Remove(key);
            else
                _values[key] = value;
        }

        private static bool IsUnset(object value)
        {
            switch (value)
            {
                case null: return true;
                case StyleLength l: return l.keyword == StyleKeyword.Null;
                case StyleFloat f: return f.keyword == StyleKeyword.Null;
                case StyleColor c: return c.keyword == StyleKeyword.Null;
                case StyleTextShadow s: return s.keyword == StyleKeyword.Null;
                case StyleTranslate t: return t.keyword == StyleKeyword.Null;
                case StyleScale s: return s.keyword == StyleKeyword.Null;
                case StyleRotate r: return r.keyword == StyleKeyword.Null;
                case StyleTransformOrigin o: return o.keyword == StyleKeyword.Null;
                case IStyleValueKeyword k: return k.Keyword == StyleKeyword.Null;
                default: return false;
            }
        }
    }

    /// <summary>Boxed <c>StyleEnum&lt;T&gt;</c> with its keyword and value name, so the store stays non-generic.</summary>
    internal interface IStyleValueKeyword
    {
        StyleKeyword Keyword { get; }
        string ValueName { get; }
    }

    internal sealed class EnumValue<T> : IStyleValueKeyword where T : struct, System.IConvertible
    {
        public EnumValue(StyleEnum<T> value) => Value = value;
        public StyleEnum<T> Value { get; }
        public StyleKeyword Keyword => Value.keyword;
        public string ValueName => Value.value.ToString();
    }

    /// <summary>Base for the style groups (Background, Flex, ...).</summary>
    public abstract class StyleData
    {
        internal StyleData(StyleStore store) => Store = store;

        internal StyleStore Store { get; }

        internal StyleEnum<T> GetEnum<T>(string key) where T : struct, System.IConvertible
        {
            EnumValue<T> boxed = Store.Get<EnumValue<T>>(key);
            return boxed == null ? new StyleEnum<T>(StyleKeyword.Null) : boxed.Value;
        }

        internal void SetEnum<T>(string key, StyleEnum<T> value) where T : struct, System.IConvertible
        {
            Store.Set(key, new EnumValue<T>(value));
        }

        internal StyleLength GetLength(string key) => Store.Values.ContainsKey(key) ? Store.Get<StyleLength>(key) : new StyleLength(StyleKeyword.Null);

        internal StyleFloat GetFloat(string key) => Store.Values.ContainsKey(key) ? Store.Get<StyleFloat>(key) : new StyleFloat(StyleKeyword.Null);

        internal StyleColor GetColor(string key) => Store.Values.ContainsKey(key) ? Store.Get<StyleColor>(key) : new StyleColor(StyleKeyword.Null);
    }
}
