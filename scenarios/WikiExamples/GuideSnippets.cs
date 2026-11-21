using DisplayKit.Elements;
using DisplayKit.Enums;
using DisplayKit.Preview;
using UnityEngine;
using UnityEngine.UIElements;

namespace WikiExamples
{
    /// <summary>
    /// Snippets from the wiki's DisplayKit Quick Start and Creating Elements pages, copied verbatim. Where a wiki
    /// screenshot continues an earlier snippet's canvas, the scenario rebuilds that canvas first (noted inline).
    /// </summary>
    public static class GuideSnippets
    {
        [DisplayKitScenario("quick-start", Description = "DisplayKit.md Quick Start; wiki Images/example1.png")]
        public static void QuickStart(PreviewContext ctx)
        {
            // Create a global canvas (all players see it)
            DisplayCanvas canvas = DisplayCanvas.Create();
            canvas.DefaultVisibility = CanvasVisibility.Visible;
            canvas.Background.Color = new Color(0, 0, 0, 0.8f);

            // Add text
            DisplayText welcomeText = canvas.AddText("Welcome to the Server!");
            welcomeText.Text.Font = FontType.RobotoBold;
            welcomeText.Text.FontSize = 48f;
            welcomeText.Text.Color = Color.white;
            welcomeText.Text.Align = TextAnchor.MiddleCenter;

            // Center the text
            canvas.Align.AlignItems = Align.Center;
            canvas.Align.JustifyContent = Justify.Center;

            // Send to all players
            canvas.Spawn();
        }

        /// <summary>"Creating a Canvas" snippet; the following snippets' screenshots continue on this canvas.</summary>
        private static DisplayCanvas CreatingACanvas()
        {
            // Create a new canvas
            DisplayCanvas canvas = DisplayCanvas.Create();

            // Configure canvas properties
            canvas.DefaultVisibility = CanvasVisibility.Visible; // or Hidden, with Visible being default
            canvas.SortOrder = 10; // UI layering order

            // Set background color
            canvas.Background.Color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            // Sets default text color for all text elements in this canvas
            canvas.Text.Color = Color.white;

            // Make it fill the screen
            canvas.Flex.Grow = 1f;
            return canvas;
        }

        [DisplayKitScenario("creating-canvas", Description = "Creating Elements: Creating a Canvas; wiki Images/example2.png")]
        public static void CreatingCanvas(PreviewContext ctx)
        {
            CreatingACanvas().Spawn();
        }

        [DisplayKitScenario("creating-container", Description = "Creating Elements: Adding Container Elements; wiki Images/example3.png")]
        public static void CreatingContainer(PreviewContext ctx)
        {
            DisplayCanvas canvas = CreatingACanvas();

            // Add a container to the canvas
            DisplayElement container = canvas.AddElement();

            // Configure the container
            container.Flex.Grow = 1f;
            container.Flex.Direction = FlexDirection.Column;
            container.Align.JustifyContent = Justify.Center;
            container.Align.AlignItems = Align.Center;

            // Add background and margin
            container.Background.Color = new Color(0.5f, 0.5f, 0.5f, 0.9f);
            container.Spacing.MarginBottom = new Length(20, LengthUnit.Pixel);
            container.Spacing.MarginTop = new Length(20, LengthUnit.Pixel);
            container.Spacing.MarginLeft = new Length(20, LengthUnit.Pixel);
            container.Spacing.MarginRight = new Length(20, LengthUnit.Pixel);

            canvas.Spawn();
        }

        [DisplayKitScenario("creating-text", Description = "Creating Elements: Adding Text Elements; wiki Images/example4.png")]
        public static void CreatingText(PreviewContext ctx)
        {
            DisplayCanvas canvas = CreatingACanvas();

            // Add text to an element
            DisplayText titleText = canvas.AddText("Welcome to DisplayKit");

            // Configure text appearance
            titleText.Text.Font = FontType.RobotoBold;
            titleText.Text.FontSize = 32f;
            titleText.Text.Color = Color.white;
            titleText.Text.Align = TextAnchor.MiddleCenter;

            // Add text shadow
            titleText.Text.TextShadow = new TextShadow
            {
                color = new Color(0, 0, 0, 0.5f),
                offset = new Vector2(2, 2),
                blurRadius = 3f
            };

            // Configure spacing
            titleText.Spacing.MarginBottom = 10f;

            canvas.Spawn();
        }

        [DisplayKitScenario("nested", Description = "Creating Elements: Nested Elements; wiki Images/example5.png")]
        public static void Nested(PreviewContext ctx)
        {
            DisplayCanvas canvas = CreatingACanvas();

            // Create main container
            DisplayElement mainContainer = canvas.AddElement();
            mainContainer.Flex.Direction = FlexDirection.Row;
            mainContainer.Flex.Grow = 1f;

            // Sets default text color for all text elements in this container.
            mainContainer.Text.Color = Color.white;

            // Left panel
            DisplayElement leftPanel = mainContainer.AddElement();
            leftPanel.Size.Width = 200f;
            leftPanel.Background.Color = new Color(0.2f, 0.2f, 0.2f);

            // Right panel
            DisplayElement rightPanel = mainContainer.AddElement();
            rightPanel.Flex.Grow = 1f;
            rightPanel.Background.Color = new Color(0.15f, 0.15f, 0.15f);

            // Add content to panels
            DisplayText leftText = leftPanel.AddText("Left Panel");
            DisplayText rightText = rightPanel.AddText("Right Panel");

            canvas.Spawn();
        }

        [DisplayKitScenario("centered", Description = "Creating Elements: Centered Content; wiki Images/example6.png")]
        public static void Centered(PreviewContext ctx)
        {
            DisplayCanvas canvas = DisplayCanvas.Create();
            canvas.Align.AlignItems = Align.Center;
            canvas.Align.JustifyContent = Justify.Center;

            // Sets default text color for all text elements in this canvas
            canvas.Text.Color = Color.white;

            DisplayText text = canvas.AddText("Centered Text");

            canvas.Spawn();
        }

        [DisplayKitScenario("full-screen-panel", Description = "Creating Elements: Full-Screen Panel; wiki Images/example7.png")]
        public static void FullScreenPanel(PreviewContext ctx)
        {
            DisplayCanvas canvas = DisplayCanvas.Create();
            canvas.Flex.Grow = 1f; // Fill available space

            DisplayElement panel = canvas.AddElement();
            panel.Size.Width = Length.Percent(100f);
            panel.Size.Height = Length.Percent(100f);
            panel.Background.Color = Color.black;

            canvas.Spawn();
        }

        [DisplayKitScenario("horizontal-layout", Description = "Creating Elements: Horizontal Layout; wiki Images/example8.png")]
        public static void HorizontalLayout(PreviewContext ctx)
        {
            // The screenshot shows white text without a background: the previous pattern's canvas.Text.Color.
            DisplayCanvas canvas = DisplayCanvas.Create();
            canvas.Text.Color = Color.white;

            DisplayElement row = canvas.AddElement();
            row.Flex.Direction = FlexDirection.Row;
            row.Align.JustifyContent = Justify.SpaceAround;

            row.AddText("Item 1");
            row.AddText("Item 2");
            row.AddText("Item 3");

            canvas.Spawn();
        }

        [DisplayKitScenario("vertical-layout", Description = "Creating Elements: Vertical Layout; wiki Images/example9.png")]
        public static void VerticalLayout(PreviewContext ctx)
        {
            DisplayCanvas canvas = DisplayCanvas.Create();
            canvas.Text.Color = Color.white;

            DisplayElement column = canvas.AddElement();
            column.Flex.Direction = FlexDirection.Column;
            column.Align.AlignItems = Align.Center;

            column.AddText("Title");
            column.AddText("Subtitle");
            column.AddText("Description");

            canvas.Spawn();
        }
    }
}
