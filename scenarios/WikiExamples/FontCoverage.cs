using System;
using DisplayKit.Elements;
using DisplayKit.Enums;
using DisplayKit.Preview;
using UnityEngine;
using UnityEngine.UIElements;

namespace WikiExamples
{
    /// <summary>Not from the wiki: every FontType with Chinese and Latin text, to see CJK coverage per font.</summary>
    public static class FontCoverage
    {
        [DisplayKitScenario("font-coverage", Description = "Every FontType with Chinese + Latin sample text")]
        public static void Fonts(PreviewContext ctx)
        {
            DisplayCanvas canvas = DisplayCanvas.Create();
            canvas.Flex.Grow = 1f;
            canvas.Background.Color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
            canvas.Text.Color = Color.white;
            canvas.Text.FontSize = 22f;
            canvas.Spacing.Padding = 24f;
            canvas.Flex.Wrap = Wrap.Wrap;
            canvas.Flex.Direction = FlexDirection.Row;

            foreach (FontType font in (FontType[])Enum.GetValues(typeof(FontType)))
            {
                DisplayElement cell = canvas.AddElement();
                cell.Size.Width = Length.Percent(50);
                cell.Spacing.PaddingTop = 4f;
                cell.Spacing.PaddingBottom = 4f;
                cell.Flex.Direction = FlexDirection.Row;
                cell.Align.AlignItems = Align.Center;

                DisplayText name = cell.AddText(font.ToString());
                name.Text.Font = FontType.RobotoMonoRegular;
                name.Text.FontSize = 14f;
                name.Text.Color = new Color(0.6f, 0.6f, 0.65f);
                name.Size.Width = 220f;

                DisplayText sample = cell.AddText("你感到一阵寒意 Containment 049 剩余 8 秒");
                sample.Text.Font = font;
            }

            canvas.Spawn();
        }

        [DisplayKitScenario("per-player-scope", Description = "Global vs per-player canvases; switch the viewer to compare")]
        public static void PerPlayerScope(PreviewContext ctx)
        {
            PreviewPlayer alice = ctx.AddPlayer("Alice");
            PreviewPlayer bob = ctx.AddPlayer("Bob");

            DisplayCanvas global = DisplayCanvas.Create();
            global.SortOrder = 10;
            DisplayText banner = global.AddText("全局：回合开始");
            banner.Text.FontSize = 28f;
            banner.Text.Color = Color.white;
            banner.Text.Align = TextAnchor.MiddleCenter;
            banner.Background.Color = new Color(0, 0, 0, 0.6f);
            banner.Spacing.Padding = 12f;
            global.Spawn();

            foreach (PreviewPlayer player in ctx.Players)
            {
                DisplayCanvas own = DisplayCanvas.Create();
                own.SortOrder = 20;
                own.Flex.Grow = 1f;
                own.Align.JustifyContent = Justify.FlexEnd;
                own.Align.AlignItems = Align.FlexEnd;
                DisplayText label = own.AddText($"仅 {player.Name} 可见");
                label.Text.FontSize = 20f;
                label.Text.Color = Color.yellow;
                label.Spacing.Padding = 16f;
                own.Spawn(player.Hub);
            }
            ctx.Frame("both see the banner and their own label");

            global.Hide(bob.Hub);
            ctx.Frame("banner hidden for Bob only");
        }
    }
}
