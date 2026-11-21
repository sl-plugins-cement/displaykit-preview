using System;
using UnityEngine;

namespace RoleIntroKit
{
    /// <summary>Which presentation a role change gets.</summary>
    public enum RoleIntroKind
    {
        /// <summary>
        /// A player spawning into a new role (squad arrival, custom role, event role, conversion such as SCP-001-A):
        /// mirrors the native SCP:SL intro screen, centred, with its timing and fades.
        /// </summary>
        FullIntro,

        /// <summary>
        /// An upgrade or smaller change inside the current role (SCP Tiers upgrade, sub-role or kit change,
        /// appointment such as facility manager): the lower-left card.
        /// </summary>
        UpgradeCard,
    }

    /// <summary>
    /// What one role intro shows. Plain values only, so any plugin can build it from its own config
    /// (ReinforcementsSystem from its force and sub-role, SCP Tiers from the tier, an event from its team).
    ///
    /// Text rules (see docs/plugin-conventions.md, player voice): <see cref="Code"/> and <see cref="Badge"/> hold
    /// Latin names and codes only; the other lines hold Chinese. Each goes into its own text element, so a client
    /// that cannot draw Chinese loses only the Chinese elements (see <see cref="CjkMode"/>).
    /// </summary>
    public sealed class RoleIntroData
    {
        public RoleIntroKind Kind { get; set; } = RoleIntroKind.FullIntro;

        /// <summary>Faction family: picks the motif (rules, chip shape, code separator, entrance). Colours stay data.</summary>
        public RoleIntroFamily Family { get; set; } = RoleIntroFamily.Foundation;

        /// <summary>Latin identifier: <c>MTF NU-7</c>, <c>SCP-049</c>. Always drawable. Parts joined by the family separator.</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Optional second Latin code part: <c>HAMMER DOWN</c>, <c>TIER III</c>.</summary>
        public string CodeSuffix { get; set; }

        /// <summary>Faction line in Chinese: <c>机动特遣队 Nu-7「落锤」</c>. Null hides it.</summary>
        public string Faction { get; set; }

        /// <summary>
        /// Full intro only: the force's own name (<c>蛇之手</c>, <c>落锤</c>, <c>冠陨</c>). When set it is the hero line in
        /// <see cref="FactionColor"/>, and <see cref="Title"/> (the sub-role) takes the faction line's slot in
        /// <see cref="Color"/>. Leave it null for roles that are their own identity (SCPs, event teams).
        /// </summary>
        public string Brand { get; set; }

        /// <summary>Full intro lead-in above the role name: the native intro's "YOU ARE".</summary>
        public string Lead { get; set; } = "你 是";

        /// <summary>The role or sub-role name, the largest line, drawn in <see cref="Color"/>: <c>落锤士兵</c>.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Readout voice: what the player should do now. The one-line brief.</summary>
        public string Objective { get; set; } = string.Empty;

        /// <summary>Optional meta voice line (「你感到……」), drawn dimmer and italic above the objective.</summary>
        public string Flavor { get; set; }

        /// <summary>Full intro only: the help line in the native "Press F1 for more information" slot. Rich text
        /// allowed (<c>&lt;color=#C8CB02&gt;.info&lt;/color&gt;</c>). Null hides it.</summary>
        public string Help { get; set; }

        /// <summary>Optional short chip: <c>增援</c>, <c>SAFE</c>, <c>活动</c>. Null hides the chip.</summary>
        public string Badge { get; set; }

        /// <summary>Role or sub-role accent: the title (full intro), the bar and chip (card).</summary>
        public Color Color { get; set; } = Color.white;

        /// <summary>Faction colour for rules, code and faction line. Null uses <see cref="Color"/>.</summary>
        public Color? FactionColor { get; set; }

        public RoleIntroTiming Timing { get; set; } = RoleIntroTiming.Default;

        internal Color FactionTint => FactionColor ?? Color;
    }

    /// <summary>
    /// Seconds for each phase. Defaults for <see cref="RoleIntroKind.FullIntro"/> are measured from the native
    /// intro (see README, "Native reference"); the card keeps its own fade.
    /// </summary>
    public sealed class RoleIntroTiming
    {
        public static RoleIntroTiming Default => new RoleIntroTiming();

        /// <summary>Wait before anything appears, e.g. for a spawn fade or a cinematic's last beat.</summary>
        public float Delay { get; set; }

        // ---- Full intro: the native Regular-speed intro (zh_Hans 14.2.9 capture, see its README): title in by
        // 0.3 s, description 1.1-1.4 s, help 2.1-2.45 s, dims from 5.6 s to 0.72 by 7 s, fades out about 14 s ----

        /// <summary>Lead and title entrance (native: in by 0.3 s).</summary>
        public float Punch { get; set; } = 0.3f;

        public float DetailAt { get; set; } = 1.1f;

        public float HelpAt { get; set; } = 2.1f;

        /// <summary>Fade-in of the detail and help lines (native about 0.3-0.35 s).</summary>
        public float LineFade { get; set; } = 0.35f;

        public float DimAt { get; set; } = 5.6f;

        public float DimFade { get; set; } = 1.4f;

        /// <summary>Opacity the whole intro rests at after <see cref="DimAt"/> (native about 0.72).</summary>
        public float DimTo { get; set; } = 0.72f;

        // ---- Card ----

        public float FadeIn { get; set; } = 0.45f;

        /// <summary>Card: seconds at full opacity. Full intro: seconds from start until the fade-out (native about
        /// 13.4, ending near 14 s). Null sizes a card from the text and gives a full intro the native length.</summary>
        public float? Hold { get; set; }

        public float FadeOut { get; set; } = 0.6f;

        /// <summary>
        /// Server ticks per second while animating. Each tick is one network write per animated property, so keep
        /// it low: motion is stepped, not smooth, until DisplayKit exposes UI Toolkit transitions.
        /// </summary>
        public float StepRate { get; set; } = 12f;

        /// <summary>Card hold read from the text: 2.4 s plus 0.11 s a character, 3–6.5 s. Full intro: the native
        /// 13.4 s before its fade-out.</summary>
        public float ResolveHold(RoleIntroData data)
        {
            if (Hold.HasValue)
                return Hold.Value;

            int chars = Length(data.Title) + Length(data.Objective) + Length(data.Flavor) + Length(data.Faction) / 2;
            return data.Kind == RoleIntroKind.FullIntro
                ? 13.4f
                : Mathf.Clamp(2.4f + 0.11f * chars, 3f, 6.5f);
        }

        /// <summary>Seconds from <see cref="Delay"/> to the end of the fade-out.</summary>
        public float Length(RoleIntroData data) => data.Kind == RoleIntroKind.FullIntro
            ? ResolveHold(data) + FadeOut
            : FadeIn + ResolveHold(data) + FadeOut;

        /// <summary>When the Omit-mode hint can show (everything settled) and when it must go (fade-out starts).</summary>
        internal (float Show, float Hide) HintWindow(RoleIntroData data) => data.Kind == RoleIntroKind.FullIntro
            ? (DetailAt, ResolveHold(data))
            : (FadeIn, FadeIn + ResolveHold(data));

        private static int Length(string s) => string.IsNullOrEmpty(s) ? 0 : s.Length;
    }

    /// <summary>How the component treats Chinese text. Set once per server from what the 15.0 client can draw.</summary>
    public enum CjkMode
    {
        /// <summary>The client falls back to a CJK font: draw every line in DisplayKit.</summary>
        Draw,

        /// <summary>
        /// The client cannot draw Chinese in DisplayKit: leave out every element whose text contains CJK and hand
        /// those lines to the plugin as <see cref="RoleIntro.FallbackLines"/> for a hint (HsmAdapter lane).
        /// </summary>
        Omit,
    }

    public sealed class RoleIntroOptions
    {
        public CjkMode Cjk { get; set; } = CjkMode.Draw;

        /// <summary>Canvas sort order. See README: above HUD widgets (0–99) and toasts (100–199), below full-screen
        /// cinematic overlays (400+).</summary>
        public int SortOrder { get; set; } = 300;
    }

    public enum RoleIntroLineKind
    {
        Badge,
        Code,
        Lead,
        Title,
        Faction,
        Flavor,
        Objective,
        Help,
    }

    /// <summary>One line the component could not draw (see <see cref="CjkMode.Omit"/>).</summary>
    public readonly struct RoleIntroLine
    {
        public RoleIntroLine(RoleIntroLineKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public RoleIntroLineKind Kind { get; }

        public string Text { get; }
    }

    public static class RoleIntroText
    {
        /// <summary>True when the text holds a character no DisplayKit font has (CJK ideographs, kana, fullwidth forms).</summary>
        public static bool HasCjk(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            foreach (char c in s)
            {
                if ((c >= '⺀' && c <= '鿿') || (c >= '豈' && c <= '﫿') || (c >= '＀' && c <= '￯') || (c >= '　' && c <= '〿'))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A force's <c>display_name_cn</c> as an intro brand, in the native class-name format: the native title says
        /// 九尾狐 指挥官, plain words separated by spaces, so the generic MTF prefix and the 「」 go
        /// (<c>机动特遣队 Nu-7「落锤」</c> becomes <c>Nu-7 落锤</c>, <c>混沌分裂者「冠陨」</c> becomes <c>混沌分裂者 冠陨</c>).
        /// </summary>
        public static string Brand(string displayNameCn)
        {
            string s = (displayNameCn ?? string.Empty).Trim();
            foreach (string prefix in new[] { "机动特遣队", "MTF" })
            {
                if (s.StartsWith(prefix, StringComparison.Ordinal))
                    s = s.Substring(prefix.Length).TrimStart();
            }

            s = s.Replace("「", " ").Replace("」", " ");
            while (s.Contains("  "))
                s = s.Replace("  ", " ");
            return s.Trim();
        }

        public static Color FromHex(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length != 6 && hex.Length != 8)
                throw new ArgumentException("Expected #RRGGBB or #RRGGBBAA", nameof(hex));
            float Byte(int i) => Convert.ToInt32(hex.Substring(i, 2), 16) / 255f;
            return new Color(Byte(0), Byte(2), Byte(4), hex.Length == 8 ? Byte(6) : 1f);
        }

        /// <summary>The colour lifted toward white so coloured small text stays legible on dark and bright scenes.</summary>
        public static Color Light(Color c, float amount = 0.35f) => Color.Lerp(new Color(c.r, c.g, c.b, 1f), Color.white, amount);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
