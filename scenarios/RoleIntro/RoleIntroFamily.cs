namespace RoleIntroKit
{
    /// <summary>
    /// Faction family. One system, subtly different per family: the rule motif around the code line, the chip
    /// shape, the code separator and the entrance. Colours come from the caller's data, not from here.
    /// </summary>
    public enum RoleIntroFamily
    {
        /// <summary>Foundation MTF (Epsilon-11, Nu-7, Delta-4, Omega-1, O-11, Alpha-1) and staff appointments.</summary>
        Foundation,

        /// <summary>SCPs, conversions (SCP-001-A) and SCP-like Tutorial roles (SCP-999).</summary>
        Scp,

        ChaosInsurgency,

        /// <summary>Global Occult Coalition.</summary>
        Goc,

        SerpentsHand,

        /// <summary>Marshall, Carter &amp; Dark.</summary>
        Mcd,

        /// <summary>Event and minigame roles.</summary>
        Event,
    }

    internal enum RuleMotif
    {
        /// <summary>One 2 px rule.</summary>
        Solid,

        /// <summary>A rule closed by a short upright tick at its inner end, like a containment bracket.</summary>
        Bracket,

        /// <summary>Three slanted strokes at the outer end, then a thin rule.</summary>
        Slash,

        /// <summary>A hollow square node at the outer end of a thin rule.</summary>
        Node,

        /// <summary>A hollow diamond outside, a filled diamond inside: the Serpent's ◇──◆ creed rule.</summary>
        Diamond,

        /// <summary>Two hairlines, letterhead style.</summary>
        Hairline,

        /// <summary>A row of short dashes.</summary>
        Dash,
    }

    internal enum ChipShape
    {
        Square,
        Outline,
        Pill,
    }

    internal enum Entrance
    {
        /// <summary>The native entrance: lead and title pop from scale 0 to 100 %, no fade.</summary>
        Punch,

        /// <summary>Scale 115 % down to 100 %: heavier, the thing lands.</summary>
        Slam,

        /// <summary>Punch with a short sideways jolt on the title.</summary>
        Jolt,

        /// <summary>Fades up from 14 px below.</summary>
        Rise,

        /// <summary>Plain fade in place.</summary>
        Fade,

        /// <summary>Punch past 100 % and back.</summary>
        Bounce,
    }

    /// <summary>Card colour bar variants.</summary>
    internal enum BarMotif
    {
        Solid,

        /// <summary>Bar with short ticks top and bottom ("[").</summary>
        Bracket,

        /// <summary>Bar split into three segments.</summary>
        Split,

        /// <summary>Bar with a hollow node on top.</summary>
        Node,

        /// <summary>Thin bar with a diamond on top.</summary>
        Diamond,

        /// <summary>Two hairline bars.</summary>
        Double,

        /// <summary>Dashed bar.</summary>
        Dash,
    }

    internal sealed class FamilyStyle
    {
        public RuleMotif Rule;
        public BarMotif Bar;
        public ChipShape Chip;
        public string Separator = "  ·  ";
        public float CodeTracking = 3f;
        public Entrance Entrance;

        /// <summary>Wraps the title. {0} is the title. Plain by default: native titles carry no brackets.</summary>
        public string TitleFormat = "{0}";

        public static FamilyStyle For(RoleIntroFamily f)
        {
            switch (f)
            {
                case RoleIntroFamily.Scp:
                    return new FamilyStyle { Rule = RuleMotif.Bracket, Bar = BarMotif.Bracket, Chip = ChipShape.Outline, Entrance = Entrance.Slam };
                case RoleIntroFamily.ChaosInsurgency:
                    return new FamilyStyle { Rule = RuleMotif.Slash, Bar = BarMotif.Split, Chip = ChipShape.Square, Separator = "  //  ", Entrance = Entrance.Jolt };
                case RoleIntroFamily.Goc:
                    return new FamilyStyle { Rule = RuleMotif.Node, Bar = BarMotif.Node, Chip = ChipShape.Outline, Separator = "  |  ", Entrance = Entrance.Punch };
                case RoleIntroFamily.SerpentsHand:
                    return new FamilyStyle { Rule = RuleMotif.Diamond, Bar = BarMotif.Diamond, Chip = ChipShape.Outline, Entrance = Entrance.Rise };
                case RoleIntroFamily.Mcd:
                    return new FamilyStyle { Rule = RuleMotif.Hairline, Bar = BarMotif.Double, Chip = ChipShape.Outline, CodeTracking = 5f, Entrance = Entrance.Fade };
                case RoleIntroFamily.Event:
                    return new FamilyStyle { Rule = RuleMotif.Dash, Bar = BarMotif.Dash, Chip = ChipShape.Pill, Entrance = Entrance.Bounce };
                default:
                    return new FamilyStyle { Rule = RuleMotif.Solid, Bar = BarMotif.Solid, Chip = ChipShape.Square, Entrance = Entrance.Punch };
            }
        }

        public string JoinCode(RoleIntroData d) =>
            string.IsNullOrEmpty(d.CodeSuffix) ? d.Code : string.IsNullOrEmpty(d.Code) ? d.CodeSuffix : d.Code + Separator + d.CodeSuffix;
    }
}
