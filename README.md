# DisplayKit preview

Design SCP:SL 15.0 DisplayKit UI before the 15.0 server build exists. Plugin C# written against
DisplayKit runs unchanged here: a stub assembly records the canvas tree instead of networking it,
a runner turns scenarios into scene JSON, and two renderers draw that JSON:

| Renderer | Use |
| --- | --- |
| HTML (`html/`) | Fast loop: live page that reloads on every C# change, and headless PNGs for agents. |
| Unity UI Toolkit (`unity/`) | Fidelity reference: the same JSON in real `UIDocument`s, saved as PNG plus element rects. |

The DisplayKit API comes from the LabAPI wiki (`DisplayKit*.md` pages); the code is not released.
Where the wiki was silent or inconsistent the stub guesses; every guess is listed under
[Guesses](#guesses). Recheck them when 15.0 ships.

## Requirements

- .NET SDK 9 or later (the runner targets .NET 9 with roll-forward; scenarios and the stub are `net48`).
- Unity 6000.4.10f1 installed at the default Hub path: the stub compiles against its
  `UnityEngine.*Module.dll` files (`DisplayKitUnityManaged` in `DisplayKitPreview.props`). Point
  that property at the 15.0 server's `Managed` folder once it exists.
- `.deps/scpsl/Managed` from `build/sync-deps.py` (for `ReferenceHub` and Mirror).
- Python 3.9+, `fonttools` (fonts) and Pillow (`compare`); Chrome or Edge for PNGs.
- Once per machine: `python .tools/displaykit-preview/fonts.py` fetches Roboto, Roboto Mono and
  Liberation Sans, instances static weights, copies Unity's Inter, and copies all of them into the
  Unity project. Both renderers read these same files.

## The loop

Run from `.tools/displaykit-preview`:

```powershell
python dkpreview.py watch scenarios/WikiExamples        # prints http://127.0.0.1:8765/html/index.html?dir=out/WikiExamples
```

Open the printed URL. Saving any `.cs` in the scenario project rebuilds and reruns it (about 1.5 s);
the page polls every 0.5 s and redraws. Compile errors and scenario exceptions appear over the
stage. Add `--watch <dir>` for plugin source outside the scenario project.

The page lets you choose the scenario, frame (arrow keys), viewing player, resolution, panel scale
mode, CJK handling and backdrop. It also has toggles for element boxes and font badges. The side
panel lists every font in use, flags text whose characters the font lacks, shows per-frame write
counts and canvas scope, and inspects the element under the pointer.

One-shot commands for agents:

```powershell
python dkpreview.py run scenarios/WikiExamples                         # build + all scenarios -> out/WikiExamples/*.json
python dkpreview.py png scenarios/WikiExamples --scenario scoreboard --res 1280x720 --badges --layout --out out/png/sb.png
python dkpreview.py png out/WikiExamples/health-bar.json --frame all   # every frame, no build
python dkpreview.py unity scenarios/WikiExamples --scenario scoreboard  # Unity reference PNG (+ .layout.json)
python dkpreview.py compare [--unity] [--wiki <LabAPI.wiki>/Images]    # wiki screenshot | HTML | Unity sheets in out/compare
```

`png` options: `--frame N|all|last`, `--viewer <connectionId>`, `--res WxH`,
`--scale constant|1920x1080` (Unity `ScaleWithScreenSize`, match 0.5), `--cjk fallback|tofu`,
`--badges` (font labels), `--layout` (element rects as JSON, same paths as the Unity renderer),
`--metrics client|reference` (the 15.0 client's text metrics, the default, or plain UI Toolkit as the
Unity renderer draws it; see [15.0 client text metrics](#150-client-text-metrics)). `compare` and
the live page's **Metrics** menu take the same choice.

## Writing scenarios for a plugin

Keep the DisplayKit code in a plugin class that receives the values it shows, the way
`scenarios/WikiExamples/ExamplesPage.cs` restructures the wiki examples. The plugin calls it from game events;
a scenario calls the same class with test values. Put the scenario project beside the plugin:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="$(SCPSL_METAREPO)\.tools\displaykit-preview\DisplayKitPreview.props" />
  <PropertyGroup><TargetFramework>net48</TargetFramework><LangVersion>latest</LangVersion></PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$(DisplayKitPreviewDir)src\DisplayKit.Stub\DisplayKit.Stub.csproj" />
    <ProjectReference Include="$(DisplayKitPreviewDir)src\DisplayKit.Preview\DisplayKit.Preview.csproj" />
    <Compile Include="..\src\Ui\**\*.cs" />  <!-- the plugin's UI classes, compiled unchanged -->
  </ItemGroup>
</Project>
```

```csharp
[DisplayKitScenario("hud", Description = "SCP-049 cure HUD")]
public static void Hud(PreviewContext ctx)
{
    PreviewPlayer alice = ctx.AddPlayer("Alice");     // alice.Hub is a ReferenceHub DisplayKit accepts
    CureHud hud = CureHud.Create(alice.Hub);          // plugin code
    ctx.Frame("spawned");
    hud.SetProgress(0.4f);
    ctx.Frame(0.5f, "40%");                           // advance simulated time, record a frame
}
```

`PreviewContext`: `AddPlayer(name)` (globally spawned canvases reach new players as late
joiners), `RemovePlayer`, `Frame(label)`, `Frame(seconds, label)`, `Time`. A scenario without
frames records one at the end; changes after the last frame record an `end` frame. A scenario
without players gets a `Viewer` player.

The preview player's `ReferenceHub` is an identity-only object. DisplayKit calls accept it, but
reading game state from it, or comparing it with `==` (UnityEngine.Object's operator calls native
code), throws. This is why UI classes should take the values they display as arguments.

To compile a plugin's UI against the stub before 15.0, reference `DisplayKit.Stub.csproj` and
import the props for the Unity 6 references. When 15.0 ships, remove the stub reference; the game
assembly provides the same namespaces.

## Scene JSON

`out/<Project>/<scenario>.json`: `frames[]`, each with `players`, `writes` (property and
structure writes since the previous frame; the real build sends each over the network) and
`canvases[]` (`id`, `sortOrder`, `global`, `defaultVisibility`, per-player `observers`, `root`).
Element nodes have `id`, `type` (`canvas|element|text`), `content` and `style`, where style keys are
UI Toolkit `IStyle` property names. Values are written as follows: lengths are `"12px"`, `"50%"`,
`"auto"` or `"none"`; colours are `[r,g,b,a]`; enums use their UI Toolkit names; keywords are
`"initial"`, `"auto"` or `"none"`; `font` holds the `FontType` name.

## HTML mapping

`html/dk-render.css` and `dk-render.js` reproduce UI Toolkit/Yoga behaviour that differs from CSS:

- **Element defaults:** every element is a column flexbox with `flex-shrink: 1` and `border-box`
  sizing. It has no implicit minimum size (CSS flex items default to `min-width: auto`), uses
  `align-content: flex-start`, and has a black, zero-width border.
- **Canvases:** each visible canvas is a full-screen layer with a column root, stacked by
  `SortOrder` and then by canvas ID. A canvas without `Flex.Grow` is as tall as its content and as
  wide as the screen, as the wiki screenshots show.
- **Text inheritance:** inherited text properties (font, size, colour, style, alignment,
  white-space, spacing, outline and shadow) are resolved down the tree as UI Toolkit does, so
  `canvas.Text.Color` sets the default colour. `text-overflow` does not inherit.
- **Client metrics:** text gets the 15.0 client's line and CJK corrections by default;
  see [15.0 client text metrics](#150-client-text-metrics).
- **Line height:** the line height is the font's hhea ascender-to-descender height, as TextCore
  uses, instead of the browser's `normal`.
- **Text alignment:** `-unity-text-align` positions text on both axes inside the label.
- **Default text:** the default font is Inter Regular at 14px in white. Labels have a 7.5px inset
  above and below and a 3px inset at each side. These values were calibrated against the wiki's
  in-game screenshots. The scoreboard matches them to within 1px: header 51/49.9, list header
  36/35.9, and a 300px box. The vertical-layout pitch is 31 against 31.4.
- **Rich text:** `b i u s sup sub br color size alpha mark cspace` render; other tags render
  literally.
- **Outline:** a text outline is drawn as a doubled `-webkit-text-stroke` painted under the fill,
  which approximates it.
- **Scale with screen size:** this mode uses Unity's log-space width/height average with match 0.5.

The renderer does not emulate:

- TextCore SDF rasterisation and kerning.
- Linear-colour-space blending of translucent colours. The game is linear; CSS blends in sRGB.
- TextCore's CJK line breaking.
- Exact paragraph spacing.

## 15.0 client text metrics

The real 15.0 client places Roboto and Roboto Mono text slightly differently from UI Toolkit in the
browser or the Unity renderer. In the default `client` mode, the HTML renderer applies three
corrections:

| Correction | Value | Applies to |
| --- | --- | --- |
| A line's ascent is the tallest ascender on that line | CJK fallback (Microsoft YaHei) ascender 1.058 em, descender 0.262 em | Any line containing CJK. The text moves down and the box grows by `(1.058 - font ascender) x size` (Roboto 0.928, so 0.13 x size; Roboto Mono 1.048, so about 0) |
| CJK glyphs drop | 1 px | CJK runs only; Latin text on the same line does not move |
| Auto-sized label height | +2 px | Roboto and Roboto Mono labels. Upper alignment adds the space below the line, Middle splits it, Lower adds it above. A fixed-height label does not move |

These corrections give one placement rule for an upper-aligned, auto-height label. The glyphs'
visual centre (CJK ideographs and Latin capitals) is this far below the label's `Top`:

`7.74 + (A - 0.339) x size` px

`A` is the font's hhea ascender, raised to 1.058 when the line contains CJK. A Middle-aligned
auto-height label is 1 px lower. Plugin layout code can use this rule to place marks beside text;
the GOC intro uses it in `GocHudLayer.TextCentre`.

**Sources.** The fit uses 31 text elements and 4 counter boxes measured in native 1920x1080 15.0
client captures of the ReinforcementsSystem GOC intro: the rail, status bar, title, power-up list,
rules, IFF key, jump prep, countdown, transit panel, visor ident, tapes, location card and landing
card. Each element was compared with the preview at the same moment and the same box. The sample
covers Roboto Mono Regular, Medium and Bold at 13-60 px and Roboto Medium and Bold at 16-60 px,
with both Latin and CJK copy.

**Residuals** (client minus preview):

| Measurement | `client` mode | `reference` mode |
| --- | --- | --- |
| Glyph centres | 26 of 31 within ±1 px; worst +1.5 px and -1 px; mean +0.35 px | Roboto lines with CJK 3-8 px high (8 px at 60 px); Roboto Mono CJK about 1.5 px high |
| Auto-sized counter heights (Roboto Mono Bold 22 and 30) | 0 px (48 and 58 px) | 2 px short |
| Fixed-height, middle-aligned tape numerals against their ticks | +0.5 px, against the client's 0 to +0.3 px | same |
| Placement rule above | within 0.7 px for upper-aligned labels | n/a |

Not covered:

- The default font (Inter) and Liberation Sans. Their insets still come from the wiki screenshots
  (see Default text above), so the +2 px correction is not applied to them.
- Multi-line labels. The corrections are applied once per label, not per line.

## Unity reference renderer

`unity/` is a minimal Unity 6000.4.10f1 project. `RenderHost` reads a jobs file and builds one
`UIDocument` per visible canvas, as the HTML layers do, on a `PanelSettings` that targets a render
texture (default runtime theme, linear colour space). It writes a PNG and the element rects.
`SceneBuilder` applies style keys to `IStyle` by reflection. Fonts are created from the same TTFs.
Jobs with `cjk: system` (what `--cjk fallback` sends) add Microsoft YaHei as the fallback font.

```powershell
python dkpreview.py unity-build      # batchmode: builds unity/Build/DkRender.exe once (needs a licensed editor)
python dkpreview.py unity <scene or project> --scenario X
```

To render without building a player, run `Unity.exe -batchmode -projectPath unity -executeMethod
DisplayKitPreviewBuild.RenderInEditor -dkJobs <jobs.json>`. This starts more slowly.

Unity batchmode needs Unity Hub signed in on the Windows account that runs it. A Hub that is not
signed in fails with `com.unity.editor.headless was not found`, and `unity-build` reports this.

## Guesses

The stub's API and the renderers' client defaults guess at these points:

1. **Enum namespaces:** `CanvasVisibility` and `FontType` are in `DisplayKit.Enums`. The full
   examples import that namespace, but Quick Start uses both types with only
   `DisplayKit.Elements`.
2. **Type namespaces:** the style-group classes (`BackgroundData` … `TextData`), `Element<T>`,
   `IDisplayElement` and `ObserverStatus` are in `DisplayKit.Elements`. `DisplayKitManager` and
   `RecyclableIdGenerator` are in `DisplayKit`.
3. **`ObserverStatus`:** a class with `IsVisible` and `IsConstructed`. The wiki only names the
   type.
4. **`OnPlayerCanvasConstructed`:** an `event Action<NetworkConnectionToClient>`. The wiki's
   handler reads `connection.connectionId`. The preview raises it on spawn with a stand-in
   connection.
5. **`DisplayKitManager.ServerCanvases`:** a `Dictionary<int, DisplayCanvas>`.
6. **`RecyclableIdGenerator`:** has `Next()` and `Return(int)`.
7. **Element IDs:** a canvas gets its `Id` at `Create()`. Element IDs are per canvas, start at 1
   and are recycled after `Remove()`.
8. **`Text` on every element:** canvases and containers have `Text` as well as `DisplayText`. The
   Element Reference lists it only on `DisplayText`, but the guides use `canvas.Text.Color` and
   `mainContainer.Text.Color` as inherited defaults.
9. **`Spacing.Padding`:** sets all four sides. The Element Reference examples use it; the Style
   Properties table omits it. No `Margin` shorthand exists.
10. **`TextData.Font`:** a plain `FontType`, not a style value, and unset means `Default`. The other
    properties use the documented `Style*` types.
11. **`DefaultVisibility`:** defaults to `Visible`, as the wiki says.
12. **Canvas spawning and visibility:**
    - `Spawn(connectionId)` can be called for several players on one canvas, as Example 1 does.
    - `Show`/`Hide` affect only current observers.
    - `SetVisibility(id, …)` for a non-observer is ignored.
    - `Remove()` on a canvas destroys it.
    - Reusing a destroyed canvas throws.
13. **`IsLoaded` and `HasChanges`:** `IsLoaded` means the canvas has at least one observer.
    `HasChanges` means there are writes since the last recorded frame. `Base` and `BaseElement` are
    null in the preview.
14. **Wiki calls the stub leaves out:**
    - `DisplayText.Value`: the Creating Elements table names it, but the reference and examples use
      `Content`.
    - `element.Show()`/`Hide()` on non-canvas elements: Modifying Elements shows these; the
      reference does not list them.
    - `Spawn`/`Show`/`SetVisibility` with a LabAPI `Player`: the documented overloads take
      `int`/`ReferenceHub`, so pass `player.ReferenceHub`.
    - `SetVisibility(hub, bool)`.
15. **Wiki snippets that do not compile against Unity 6 UI Toolkit:**
    - `new TextShadow(StyleKeyword.Null)`: use `new StyleTextShadow(StyleKeyword.Null)`.
    - `new Translate(new Vector2(…))`: use `new Translate(Length, Length)`.
    - `TextAnchor.Center` and `Overflow.Scroll` in the tables: the real names are
      `TextAnchor.MiddleCenter`, and `Overflow` has only `Visible` and `Hidden`.
16. **Unity version:** the stub compiles against Unity 6000.4 modules. The wiki links the 6000.4
    UI Toolkit docs; the 14.2.9 game is on an older Unity whose `WhiteSpace` lacks `Pre`/`PreWrap`.
17. **Client panel:** one `UIDocument` per canvas on a full-screen panel, ordered by `SortOrder`,
    with constant pixel size at scale 1. The wiki screenshots measure 1:1: a 200px panel is 200px
    and a 300px box is 300px.
18. **Default text:** Inter Regular ("NotInter") at 14px, white, with the label inset above. The
    wiki screenshots all show white text, but every snippet sets white somewhere upstream, so the
    real default colour is unknown. Set `Text.Color` explicitly.
19. **Fonts:** `RobotoBold` and the other Roboto/Roboto Mono types use the current Google Fonts
    builds. Default uses Unity's bundled Inter. The game may ship other versions with different
    metrics. The wiki scoreboard's `RobotoMonoBold` score column does not look monospaced.
20. **CJK:** none of the listed fonts contains CJK glyphs, and whether the 15.0 client has a
    fallback is unknown. The page's `system fallback` mode shows the best case. `no fallback (□)`
    shows the worst case.
