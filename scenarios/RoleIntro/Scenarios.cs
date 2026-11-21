using System;
using System.Collections.Generic;
using DisplayKit.Preview;

namespace RoleIntroKit
{
    /// <summary>
    /// Sample intros for every faction family, as a full intro and as an upgrade card. Names, briefs and colours
    /// come from ReinforcementsSystem's force defaults and announcement profiles where the force exists there;
    /// the rest is sample copy, not authored plugin text. Frame labels name the phase; "hold" frames are the ones
    /// to judge readability on.
    /// </summary>
    public static class Scenarios
    {
        private static UnityEngine.Color Hex(string h) => RoleIntroText.FromHex(h);

        /// <summary>
        /// Native help line, zh_Hans Facility.txt: 按下 &lt;color=yellow&gt;&lt;b&gt;F1&lt;/b&gt;&lt;/color&gt; 了解关于角色的更多信息.
        /// Same wording, with our console command in the key's place.
        /// </summary>
        private const string Help = "在控制台输入 <color=#FFFF00><b>.info</b></color> 了解关于角色的更多信息";

        // Native description vocabulary (zh_Hans Class_Descriptions.txt): goal sentences, one per line, team names in
        // their team colours, then mechanics on #FFFF00 lines.
        private const string Scp = "<color=#F00>SCP</color>";
        private const string Mtf = "<color=#0096FF>MTF特工</color>";
        private const string Guards = "<color=#5B6370>设施警卫</color>";
        private const string Ci = "<color=#008F1E>混沌分裂者</color>";
        private const string Foundation = "<color=#0096FF>基金会人员</color>";
        private const string ScpGoals = "消灭所有人类。\n阻止人类逃离设施。\n与其它" + Scp + "合作。";
        private static string Mechanic(string line) => "<color=#FFFF00>\n" + line + "</color>";

        // ---- Full intros: spawning into a new role ----
        // Title follows the native class name: the faction, a space, the rank (九尾狐 指挥官). SCPs are their
        // designation (SCP-049-2). Descriptions are sample copy in the native voice, not authored plugin text.

        public static readonly Dictionary<string, Func<RoleIntroData>> Full = new Dictionary<string, Func<RoleIntroData>>
        {
            // Nu7Defaults: display_name_cn, captain accent #66B4ED, announcement primary #3E86D6.
            ["mtf-nu7"] = () => new RoleIntroData
            {
                Family = RoleIntroFamily.Foundation, Code = "MTF NU-7", CodeSuffix = "HAMMER DOWN", Badge = "增援",
                Brand = "机动特遣队 Nu-7「落锤」", Title = "队长",
                Objective = "从敌对势力手中夺回设施。\n与" + Guards + "和其他" + Mtf + "一同合作。" + Mechanic("指挥小队推进，夺回站点。"),
                Color = Hex("#66B4ED"), FactionColor = Hex("#3E86D6"), Help = Help,
            },
            // Omega1Defaults: display_name_cn and gold.
            ["mtf-omega1"] = () => new RoleIntroData
            {
                Family = RoleIntroFamily.Foundation, Code = "OMEGA-1", CodeSuffix = "LAW'S LEFT HAND", Badge = "增援",
                Brand = "Omega-1「律法左手」", Title = "执行官",
                Objective = "保护举报人，保全证据。\n执行伦理委员会的命令。\n与其他" + Mtf + "一同合作。",
                Color = Hex("#E6BF66"), FactionColor = Hex("#E6BF66"), Help = Help,
            },
            // Daybreak flesh conversion: titled by designation, like SCP-049-2. The sun made it, so it spreads
            // the burn and dissolves at dusk (DaybreakHints' natural-flesh line and DaybreakService.Dusk).
            ["scp-001a"] = () => new RoleIntroData
            {
                Family = RoleIntroFamily.Scp, Code = "WHEN DAY BREAKS",
                Faction = "SCP项目", Title = "SCP-001-A",
                Objective = ScpGoals + Mechanic("靠近或攻击人类会让其日灼。\n日落时你会消散。"),
                Color = Hex("#EC2222"), FactionColor = Hex("#EC2222"), Help = Help,
            },
            // Tutorial-based custom role. Latin badge: stays drawable without CJK.
            ["scp-999"] = () => new RoleIntroData
            {
                Family = RoleIntroFamily.Scp, Code = "EUCLID", Badge = "SAFE",
                Title = "SCP-999",
                Objective = "治疗所有玩家。\n让敌对的" + Scp + "平静下来。" + Mechanic("靠近其他玩家即可为他们治疗。"),
                Color = Hex("#FF9A3C"), FactionColor = Hex("#FF9A3C"), Help = Help,
            },
            // ChaosDefaults: display_name_cn, gamma operative accent, ChaosPackage primary #3FD968.
            ["chaos"] = () => new RoleIntroData
            {
                Family = RoleIntroFamily.ChaosInsurgency, Code = "CI", CodeSuffix = "CROWNFALL", Badge = "增援",
                Brand = "混沌分裂者「冠陨」", Title = "伽玛行动官",
                Objective = "占领这座设施并消灭" + Foundation + "。\n与其他" + Ci + "一同合作。" + Mechanic("指挥突击组夺取设施。"),
                Color = Hex("#3FD968"), FactionColor = Hex("#3FD968"), Help = Help,
            },
            // GocDefaults: display_name_cn, GocPackage primary #2AA6FF.
            ["goc"] = () => new RoleIntroData
            {
                Family = RoleIntroFamily.Goc, Code = "GOC", CodeSuffix = "CMD", Badge = "增援",
                Brand = "全球超自然联盟", Title = "战术指挥官",
                Objective = "消灭设施内的异常。\n与其他<color=#2AA6FF>全球超自然联盟</color>成员一同合作。\n尽量避开其他阵营。" + Mechanic("协调火力与防线。"),
                Color = Hex("#33BFFF"), FactionColor = Hex("#2AA6FF"), Help = Help,
            },
            // SerpentsHandDefaults: display_name_cn, adept accent, violet primary.
            ["serpent"] = () => new RoleIntroData
            {
                Family = RoleIntroFamily.SerpentsHand, Code = "SERPENT'S HAND", CodeSuffix = "ADEPT",
                Brand = "蛇之手", Title = "奇术师",
                Objective = "解放被收容的" + Scp + "。\n与" + Scp + "和其他<color=#9D5BFF>蛇之手</color>成员一同合作。" + Mechanic("以意志凝形，攻守由心。"),
                Color = Hex("#9D5BFF"), FactionColor = Hex("#9D5BFF"), Help = Help,
            },
            // McdDefaults: display_name_cn (Latin in the defaults) and #D8B24A.
            ["mcd"] = () => new RoleIntroData
            {
                Family = RoleIntroFamily.Mcd, Code = "MC&D", CodeSuffix = "EXECUTIVE LIAISON",
                Brand = "MC&D", Title = "执行联络官",
                Objective = "主持交易，带走有价值的资产。\n与其他<color=#D8B24A>MC&D</color>成员一同合作。\n尽量避开其他阵营。",
                Color = Hex("#D8B24A"), FactionColor = Hex("#D8B24A"), Help = Help,
            },
            // AutoEvent City Siege team role.
            ["event"] = () => new RoleIntroData
            {
                Family = RoleIntroFamily.Event, Code = "CITY SIEGE", CodeSuffix = "ATTACKERS", Badge = "活动",
                Faction = "攻城战", Title = "攻城方",
                Objective = "攻破城门。\n在倒计时结束前占领中央广场。\n与其他<color=#F08A24>攻城方</color>成员一同合作。",
                Color = Hex("#F08A24"), FactionColor = Hex("#F08A24"), Help = Help,
            },
        };

        // ---- Upgrade cards: changes inside the current role ----

        public static readonly Dictionary<string, Func<RoleIntroData>> Card = new Dictionary<string, Func<RoleIntroData>>
        {
            ["mtf-manager"] = () => new RoleIntroData
            {
                Kind = RoleIntroKind.UpgradeCard, Family = RoleIntroFamily.Foundation, Code = "SITE-02", CodeSuffix = "FACILITY MANAGER", Badge = "任命",
                Title = "设施经理", Objective = "你的专属权限卡可以启动对讲室终端。",
                Color = Hex("#2AA6FF"),
            },
            // Delta4Defaults: corpsman accent #4BD47A, primary #5DB7FF.
            ["mtf-delta4"] = () => new RoleIntroData
            {
                Kind = RoleIntroKind.UpgradeCard, Family = RoleIntroFamily.Foundation, Code = "MTF DELTA-4", CodeSuffix = "CORPSMAN", Badge = "换装",
                Faction = "民兵", Title = "随队医护兵", Objective = "治疗队友。",
                Color = Hex("#4BD47A"), FactionColor = Hex("#5DB7FF"),
            },
            // O11Defaults: echo brief, the subtitle's pale cyan and lilac.
            ["mtf-o11"] = () => new RoleIntroData
            {
                Kind = RoleIntroKind.UpgradeCard, Family = RoleIntroFamily.Foundation, Code = "MTF OMICRON-11", CodeSuffix = "ECHO", Badge = "换装",
                Faction = "白板", Title = "回声", Objective = "释放参照脉冲，暴露污染源并恢复同伴记忆。",
                Color = Hex("#BCA9DD"), FactionColor = Hex("#D3EDF2"),
            },
            ["scp-tier"] = () => new RoleIntroData
            {
                Kind = RoleIntroKind.UpgradeCard, Family = RoleIntroFamily.Scp, Code = "SCP-939", CodeSuffix = "TIER III", Badge = "异常升级",
                Title = "三阶 · 回声猎手", Objective = "新技能已解锁。在控制台输入 .info 查看操作。",
                Color = Hex("#EC2222"),
            },
            ["chaos"] = () => new RoleIntroData
            {
                Kind = RoleIntroKind.UpgradeCard, Family = RoleIntroFamily.ChaosInsurgency, Code = "CI", CodeSuffix = "HOUND", Badge = "换装",
                Faction = "冠陨", Title = "猎犬", Objective = "持续压制。",
                Color = Hex("#A62B22"), FactionColor = Hex("#3FD968"),
            },
            ["goc"] = () => new RoleIntroData
            {
                Kind = RoleIntroKind.UpgradeCard, Family = RoleIntroFamily.Goc, Code = "GOC", CodeSuffix = "ENG", Badge = "换装",
                Title = "战斗工程师", Objective = "部署工业屏障。",
                Color = Hex("#47D9FF"), FactionColor = Hex("#2AA6FF"),
            },
            ["serpent"] = () => new RoleIntroData
            {
                Kind = RoleIntroKind.UpgradeCard, Family = RoleIntroFamily.SerpentsHand, Code = "SERPENT'S HAND", CodeSuffix = "WAY-OPENER",
                Title = "启门者", Objective = "封域者。唯你能开启通往图书馆的「门径」。",
                Color = Hex("#13EAFD"), FactionColor = Hex("#9D5BFF"),
            },
            ["mcd"] = () => new RoleIntroData
            {
                Kind = RoleIntroKind.UpgradeCard, Family = RoleIntroFamily.Mcd, Code = "MC&D", CodeSuffix = "APPRAISER",
                Title = "鉴价师", Objective = "评估资产。",
                Color = Hex("#F5E6C4"), FactionColor = Hex("#D8B24A"),
            },
            ["event"] = () => new RoleIntroData
            {
                Kind = RoleIntroKind.UpgradeCard, Family = RoleIntroFamily.Event, Code = "CITY SIEGE", CodeSuffix = "BREACHER", Badge = "活动",
                Title = "破门手", Objective = "你拿到了攻城锤。靠近城门按 E 撞门。",
                Color = Hex("#F08A24"),
            },
        };

        // ---- Timeline shared by every scenario ----

        private static void Play(PreviewContext ctx, RoleIntroData data, CjkMode cjk = CjkMode.Draw)
        {
            PreviewPlayer viewer = ctx.AddPlayer("Viewer");
            RoleIntroDirector director = new RoleIntroDirector { Options = new RoleIntroOptions { Cjk = cjk } };
            RoleIntro intro = director.Play(viewer.Hub, viewer.ConnectionId, data);
            RoleIntroTiming t = data.Timing;
            float hold = t.ResolveHold(data);

            void At(float time, string label)
            {
                director.Update(time - ctx.Time);
                ctx.Time = time;
                ctx.Frame(label);
            }

            if (data.Kind == RoleIntroKind.FullIntro)
            {
                // The native beats: pop, details, help, dim, out.
                At(0.00f, "pop");
                At(0.10f, "punch");
                At(0.50f, "title");
                At(t.DetailAt + t.LineFade * 0.5f, "details in");
                At(t.HelpAt + t.LineFade, "hold full");
                At(t.DimAt + t.DimFade * 0.5f, "dimming");
                At(Math.Max(t.DimAt + t.DimFade + 0.5f, hold - 0.5f), $"hold dimmed {hold:0.0}s");
                At(hold + t.FadeOut * 0.5f, "out 50%");
            }
            else
            {
                At(0.00f, "in 0%");
                At(t.FadeIn * 0.34f, "in 33%");
                At(t.FadeIn * 0.67f, "in 67%");
                At(t.FadeIn + hold * 0.5f, $"hold {hold:0.0}s");
                At(t.FadeIn + hold + t.FadeOut * 0.5f, "out 50%");
            }

            At(intro.Duration + 0.01f, "destroyed");
        }

        [DisplayKitScenario("full-mtf-nu7")] public static void F1(PreviewContext c) => Play(c, Full["mtf-nu7"]());
        [DisplayKitScenario("full-mtf-omega1")] public static void F2(PreviewContext c) => Play(c, Full["mtf-omega1"]());
        [DisplayKitScenario("full-scp-001a")] public static void F3(PreviewContext c) => Play(c, Full["scp-001a"]());
        [DisplayKitScenario("full-scp-999")] public static void F4(PreviewContext c) => Play(c, Full["scp-999"]());
        [DisplayKitScenario("full-chaos")] public static void F5(PreviewContext c) => Play(c, Full["chaos"]());
        [DisplayKitScenario("full-goc")] public static void F6(PreviewContext c) => Play(c, Full["goc"]());
        [DisplayKitScenario("full-serpent")] public static void F7(PreviewContext c) => Play(c, Full["serpent"]());
        [DisplayKitScenario("full-mcd")] public static void F8(PreviewContext c) => Play(c, Full["mcd"]());
        [DisplayKitScenario("full-event")] public static void F9(PreviewContext c) => Play(c, Full["event"]());

        [DisplayKitScenario("card-mtf-manager")] public static void C1(PreviewContext c) => Play(c, Card["mtf-manager"]());
        [DisplayKitScenario("card-mtf-delta4")] public static void C2(PreviewContext c) => Play(c, Card["mtf-delta4"]());
        [DisplayKitScenario("card-mtf-o11")] public static void C3(PreviewContext c) => Play(c, Card["mtf-o11"]());
        [DisplayKitScenario("card-scp-tier")] public static void C4(PreviewContext c) => Play(c, Card["scp-tier"]());
        [DisplayKitScenario("card-chaos")] public static void C5(PreviewContext c) => Play(c, Card["chaos"]());
        [DisplayKitScenario("card-goc")] public static void C6(PreviewContext c) => Play(c, Card["goc"]());
        [DisplayKitScenario("card-serpent")] public static void C7(PreviewContext c) => Play(c, Card["serpent"]());
        [DisplayKitScenario("card-mcd")] public static void C8(PreviewContext c) => Play(c, Card["mcd"]());
        [DisplayKitScenario("card-event")] public static void C9(PreviewContext c) => Play(c, Card["event"]());

        // ---- Worst case: no CJK in DisplayKit. Render with --cjk tofu. ----

        /// <summary>Draw mode on a client without CJK: what shipping the wrong guess looks like.</summary>
        [DisplayKitScenario("full-nu7-tofu", Description = "Draw mode, render with --cjk tofu")]
        public static void FullTofu(PreviewContext c) => Play(c, Full["mtf-nu7"]());

        /// <summary>Omit mode: the intro keeps its Latin code row and motif; Chinese lines go to the fallback hint.</summary>
        [DisplayKitScenario("full-nu7-omit", Description = "Omit mode: Latin-only intro + HSM hint lines")]
        public static void FullOmit(PreviewContext c) => Play(c, Full["mtf-nu7"](), CjkMode.Omit);

        [DisplayKitScenario("card-tier-omit", Description = "Omit mode: Latin-only card + HSM hint lines")]
        public static void CardOmit(PreviewContext c) => Play(c, Card["scp-tier"](), CjkMode.Omit);

        // ---- Lifecycle: a role change mid-intro replaces it; leaving removes it. ----
        [DisplayKitScenario("lifecycle", Description = "Card replaced by a full intro on conversion, cancel on leave")]
        public static void Lifecycle(PreviewContext ctx)
        {
            PreviewPlayer alice = ctx.AddPlayer("Alice");
            RoleIntroDirector director = new RoleIntroDirector();
            director.Play(alice.Hub, alice.ConnectionId, Card["scp-tier"]());
            director.Update(1.0f); ctx.Time = 1.0f; ctx.Frame("tier card holding");
            director.Play(alice.Hub, alice.ConnectionId, Full["scp-001a"]());   // converted mid-card: old canvas destroyed
            director.Update(1.5f); ctx.Time = 2.5f; ctx.Frame("replaced by SCP-001-A intro");
            director.Cancel(alice.ConnectionId);                                 // player left
            ctx.RemovePlayer(alice);
            ctx.Frame("left: no canvas");
        }
    }
}
