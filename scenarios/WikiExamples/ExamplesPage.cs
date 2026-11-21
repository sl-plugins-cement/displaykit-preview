using System.Collections.Generic;
using System.Linq;
using DisplayKit.Elements;
using DisplayKit.Enums;
using DisplayKit.Preview;
using UnityEngine;
using UnityEngine.UIElements;

namespace WikiExamples
{
    // DisplayKit-Examples.md. Each view class holds the example's DisplayKit code verbatim; only the LabAPI event
    // plumbing (CustomEventsHandler, Player, MEC) is replaced by scenario calls, because the preview has no
    // game. This is the pattern for plugins too: keep UI construction/update in a class that takes the values it
    // shows, and drive it from game events in the plugin and from a scenario in the preview.

    /// <summary>Example 1: Simple Welcome Message.</summary>
    public sealed class WelcomeView
    {
        private DisplayCanvas _canvas;
        private DisplayText _welcomeText;

        public void OnServerWaitingForPlayers()
        {
            _canvas = DisplayCanvas.Create();
            _canvas.DefaultVisibility = CanvasVisibility.Visible;

            _canvas.Flex.Grow = 1f;

            _welcomeText = _canvas.AddText("Welcome to the Server!");
            _welcomeText.Text.Font = FontType.RobotoBold;
            _welcomeText.Text.FontSize = 48f;
            _welcomeText.Text.Color = Color.white;
            _welcomeText.Text.Align = TextAnchor.MiddleCenter;
            _welcomeText.Text.TextShadow = new TextShadow
            {
                color = Color.black,
                offset = new Vector2(3, 3),
                blurRadius = 5f
            };

            // Center using flex on canvas
            _canvas.Align.AlignItems = Align.Center;
            _canvas.Align.JustifyContent = Justify.Center;
        }

        public void OnPlayerJoined(int connectionId) => _canvas?.Spawn(connectionId);

        // Timing.CallDelayed(7f, ...) in the wiki.
        public void AfterSevenSeconds(int connectionId) => _canvas?.Hide(connectionId);

        public void OnServerRoundEnded()
        {
            _canvas?.Destroy();
            _canvas = null;
        }
    }

    /// <summary>Example 2: Player Health Bar (Per-Player). PlayerHealthData with Player replaced by hub + values.</summary>
    public sealed class PlayerHealthData
    {
        public static PlayerHealthData Create(ReferenceHub player, float health, float maxHealth)
        {
            DisplayCanvas canvas = DisplayCanvas.Create();

            canvas.SortOrder = 100;
            canvas.Align.AlignItems = Align.Center;
            canvas.Position.Top = 20;
            canvas.Size.Width = Length.Percent(100); // height stays automatic

            DisplayText text = canvas.AddText($"{health} / {maxHealth}");
            text.Text.Font = FontType.RobotoBold;
            text.Text.FontSize = 18f;
            text.Text.Color = Color.white;

            DisplayElement background = canvas.AddElement();
            background.Position.Top = 10;
            background.Size.Width = 400f;
            background.Size.Height = 30f;
            background.Background.Color = new Color(0.2f, 0.2f, 0.2f);
            background.Border.Color = Color.white;
            background.Border.Width = 2f;
            background.Border.Radius = 5f;

            DisplayElement fill = background.AddElement();
            fill.Flex.Grow = 1f;
            fill.Background.Color = Color.green;
            fill.Border.Radius = 2.5f;

            canvas.Spawn(player);

            return new PlayerHealthData(canvas, background, fill, text);
        }

        public DisplayCanvas Canvas { get; }
        public DisplayElement Background { get; }
        public DisplayElement Fill { get; }
        public DisplayText HealthText { get; }

        public float CurrentHealth { get; set; }
        public float CurrentMaxHealth { get; set; }

        private PlayerHealthData(DisplayCanvas canvas, DisplayElement background, DisplayElement fill, DisplayText healthText)
        {
            Canvas = canvas;
            Background = background;
            Fill = fill;
            HealthText = healthText;
        }

        public bool NeedsUpdate(float health, float maxHealth) => CurrentHealth != health || CurrentMaxHealth != maxHealth;

        public void Update(float health, float maxHealth)
        {
            CurrentHealth = health;
            CurrentMaxHealth = maxHealth;

            HealthText.Content = $"{CurrentHealth:F0} / {CurrentMaxHealth:F0}";

            // Update bar width
            float healthPercent = CurrentHealth / CurrentMaxHealth;
            Fill.Size.Width = Length.Percent(healthPercent * 100f);

            // Change color based on health
            if (healthPercent > 0.6f)
                Fill.Background.Color = Color.green;
            else if (healthPercent > 0.3f)
                Fill.Background.Color = Color.yellow;
            else
                Fill.Background.Color = Color.red;
        }

        public void Destroy()
        {
            Canvas?.Destroy();
        }
    }

    /// <summary>Example 3: Global Server Announcements.</summary>
    public sealed class AnnouncementView
    {
        private DisplayCanvas _canvas;
        private DisplayText _announcementText;

        public void OnServerWaitingForPlayers()
        {
            _canvas = DisplayCanvas.Create();
            _canvas.DefaultVisibility = CanvasVisibility.Hidden;
            _canvas.SortOrder = 200;

            DisplayElement backgroundPanel = _canvas.AddElement();
            backgroundPanel.Position.Position = Position.Absolute;
            backgroundPanel.Position.Top = 100f;
            backgroundPanel.Position.Left = Length.Percent(50f);
            backgroundPanel.Transform.Translate = new Translate(new Length(-250), new Length(0));
            backgroundPanel.Size.Width = 500f;
            backgroundPanel.Size.Height = 80f;
            backgroundPanel.Background.Color = new Color(0, 0, 0, 0.85f);
            backgroundPanel.Border.Color = Color.cyan;
            backgroundPanel.Border.Width = 3f;
            backgroundPanel.Border.Radius = 10f;
            backgroundPanel.Align.AlignItems = Align.Center;
            backgroundPanel.Align.JustifyContent = Justify.Center;

            _announcementText = backgroundPanel.AddText("");
            _announcementText.Text.Font = FontType.RobotoBold;
            _announcementText.Text.FontSize = 24f;
            _announcementText.Text.Color = Color.white;
            _announcementText.Text.Align = TextAnchor.MiddleCenter;

            _canvas.Spawn();
        }

        // First half of AnnouncementHandle; the wiki waits `duration` seconds before Hide().
        public void Show(string message)
        {
            _announcementText.Content = message;
            _canvas.Show();
        }

        public void Hide() => _canvas.Hide();
    }

    /// <summary>Example 4: Scoreboard. ScoreboardEntry verbatim; score data keyed by name instead of Player.</summary>
    public sealed class ScoreboardView
    {
        public class ScoreboardEntry
        {
            public DisplayElement Element { get; }

            public DisplayText Name { get; }
            public DisplayText Score { get; }

            public ScoreboardEntry(DisplayElement playersList, string name, int score)
            {
                Element = playersList.AddElement();
                Element.Flex.Direction = FlexDirection.Row;
                Element.Align.JustifyContent = Justify.SpaceBetween;
                Element.Spacing.PaddingTop = 5f;
                Element.Spacing.PaddingBottom = 5f;
                Element.Border.BottomWidth = 1f;
                Element.Border.BottomColor = new Color(0.3f, 0.3f, 0.3f);

                Name = Element.AddText(name);
                Name.Text.Color = Color.white;

                Score = Element.AddText($"{score} xp");
                Score.Text.Color = Color.cyan;
                Score.Text.Font = FontType.RobotoMonoBold;
            }

            public void Destroy()
            {
                Element?.Remove();
            }
        }

        public Dictionary<string, int> Scores { get; } = new Dictionary<string, int>();
        public List<ScoreboardEntry> Entries { get; } = new List<ScoreboardEntry>();

        private DisplayCanvas _scoreCanvas;
        private DisplayElement _playersList;

        public void OnServerWaitingForPlayers()
        {
            _scoreCanvas = DisplayCanvas.Create();
            _scoreCanvas.DefaultVisibility = CanvasVisibility.Visible;
            _scoreCanvas.SortOrder = 50;

            DisplayElement mainContainer = _scoreCanvas.AddElement();
            mainContainer.Position.Position = Position.Absolute;
            mainContainer.Position.Top = 100f;
            mainContainer.Position.Right = 50f;
            mainContainer.Size.Width = 300f;
            mainContainer.Background.Color = new Color(0, 0, 0, 0.8f);
            mainContainer.Border.Color = Color.white;
            mainContainer.Border.Width = 2f;
            mainContainer.Border.Radius = 10f;

            mainContainer.Spacing.PaddingTop = 15f;
            mainContainer.Spacing.PaddingBottom = 15f;
            mainContainer.Spacing.PaddingLeft = 15f;
            mainContainer.Spacing.PaddingRight = 15f;

            mainContainer.Flex.Direction = FlexDirection.Column;

            // Header
            DisplayElement headerPanel = mainContainer.AddElement();
            headerPanel.Flex.Direction = FlexDirection.Row;
            headerPanel.Align.JustifyContent = Justify.Center;
            headerPanel.Spacing.PaddingBottom = 10f;
            headerPanel.Border.BottomWidth = 2f;
            headerPanel.Border.BottomColor = Color.gray;

            DisplayText titleText = headerPanel.AddText("SCOREBOARD");
            titleText.Text.Font = FontType.RobotoBold;
            titleText.Text.FontSize = 20f;
            titleText.Text.Color = Color.yellow;

            // List headers
            DisplayElement listHeader = mainContainer.AddElement();
            listHeader.Flex.Direction = FlexDirection.Row;
            listHeader.Align.JustifyContent = Justify.SpaceBetween;
            listHeader.Spacing.PaddingBottom = 5f;

            DisplayText nameHeader = listHeader.AddText("Player");
            nameHeader.Text.Font = FontType.RobotoBold;
            nameHeader.Text.Color = Color.gray;

            DisplayText scoreHeader = listHeader.AddText("Score");
            scoreHeader.Text.Font = FontType.RobotoBold;
            scoreHeader.Text.Color = Color.gray;

            // Player list container
            _playersList = mainContainer.AddElement();
            _playersList.Flex.Direction = FlexDirection.Column;
            _playersList.Flex.Grow = 1f;

            _scoreCanvas.Spawn();
        }

        public void OnPlayerJoined(string nickname, int score)
        {
            Scores.Add(nickname, score);
            Entries.Add(new ScoreboardEntry(_playersList, nickname, score));
            UpdateEntries();
        }

        public void UpdateEntries()
        {
            List<KeyValuePair<string, int>> sorted = Scores.OrderByDescending(static s => s.Value).ToList();

            for (int i = 0; i < Entries.Count; i++)
            {
                ScoreboardEntry entry = Entries[i];
                entry.Name.Content = $"{sorted[i].Key}";
                entry.Score.Content = $"{sorted[i].Value} xp";
            }
        }
    }

    public static class ExamplesPage
    {
        [DisplayKitScenario("welcome", Description = "Examples: 1 Simple Welcome Message (per-player spawn, hidden after 7 s); wiki Images/example1-welcome.png")]
        public static void Welcome(PreviewContext ctx)
        {
            WelcomeView view = new WelcomeView();
            view.OnServerWaitingForPlayers();
            PreviewPlayer alice = ctx.AddPlayer("Alice");
            view.OnPlayerJoined(alice);
            ctx.Frame("Alice joined");
            ctx.Frame(7f, "7 s later: hidden for Alice");
        }

        [DisplayKitScenario("health-bar", Description = "Examples: 2 Player Health Bar (per-player canvases); wiki Images/example2-healthbar-*.png")]
        public static void HealthBar(PreviewContext ctx)
        {
            PreviewPlayer alice = ctx.AddPlayer("Alice");
            PreviewPlayer bob = ctx.AddPlayer("Bob");
            PlayerHealthData aliceBar = PlayerHealthData.Create(alice, 100, 100);
            PlayerHealthData bobBar = PlayerHealthData.Create(bob, 100, 100);
            ctx.Frame("joined (initial text)");

            aliceBar.Update(100, 100);
            bobBar.Update(100, 100);
            ctx.Frame(0.1f, "100 / 100");

            aliceBar.Update(50, 100);
            bobBar.Update(80, 100);
            ctx.Frame(0.1f, "Alice 50, Bob 80");

            aliceBar.Update(20, 100);
            ctx.Frame(0.1f, "Alice 20 (red)");
        }

        [DisplayKitScenario("announcement", Description = "Examples: 3 Global Server Announcements; wiki Images/example3-announcement.png")]
        public static void Announcement(PreviewContext ctx)
        {
            AnnouncementView view = new AnnouncementView();
            ctx.AddPlayer("Alice");
            view.OnServerWaitingForPlayers();
            ctx.Frame("spawned hidden");
            view.Show("SERVER ANNOUNCEMENT");
            ctx.Frame("shown");
            view.Show("Player Bob has joined the game!");
            ctx.AddPlayer("Bob");
            ctx.Frame("late joiner Bob");
            ctx.Time += 5f;
            view.Hide();
            ctx.Frame("hidden after 5 s");
        }

        [DisplayKitScenario("scoreboard", Description = "Examples: 4 Scoreboard; wiki Images/example4-scoreboard.png")]
        public static void Scoreboard(PreviewContext ctx)
        {
            ScoreboardView view = new ScoreboardView();
            view.OnServerWaitingForPlayers();
            view.OnPlayerJoined("Player1", 1500);
            view.OnPlayerJoined("Player2", 1200);
            view.OnPlayerJoined("Player3", 950);
            view.OnPlayerJoined("Player4", 800);
            ctx.Frame("four players");
            view.Scores["Player4"] = 1600;
            view.UpdateEntries();
            ctx.Frame(0.5f, "Player4 takes the lead");
        }
    }
}
