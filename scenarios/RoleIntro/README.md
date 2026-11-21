# Role intro (DisplayKit)

Our replacement for SCP:SL's native role intro wherever a plugin changes a role with
`RoleSpawnFlags.ShowIntroScreen` off. It has two kinds:

| Kind | When | Look |
| --- | --- | --- |
| `FullIntro` | A player spawns into a new role: squad arrivals, custom and Tutorial-based roles, event roles, conversions such as SCP-001-A | Mirrors the native intro screen: centred, same anchors, hierarchy and timing |
| `UpgradeCard` | A change inside the current role: SCP Tiers upgrades, sub-role or kit changes, appointments such as facility manager | Lower-left card with a colour bar |

Each intro also has a faction `Family` that picks subtle motifs, so the families look distinct but
still read as one system. The component is DisplayKit-only, with no plugin or LabAPI dependency: data
in, canvas out. It lives here until a shared library takes it.

| File | Role |
| --- | --- |
| `RoleIntroData.cs` | `RoleIntroData` (what to show), `RoleIntroKind`, `RoleIntroTiming`, `RoleIntroOptions`, `CjkMode` |
| `RoleIntroFamily.cs` | `RoleIntroFamily` and the per-family motif table |
| `RoleIntro.cs` | `RoleIntro`, one player's canvas and its animation, plus the native and card layouts |
| `RoleIntroDirector.cs` | Keeps one intro per player, replaces it on a new role, handles the CJK fallback hint |
| `Scenarios.cs` | One full intro and one card per family, the no-CJK cases and the lifecycle |
| `render.py` | Review set: per-kind family sheets at 1920x1080 and 1280x720, timelines, native beside ours, worst cases |

```powershell
python dkpreview.py watch scenarios/RoleIntro              # live page; pick full-serpent, card-scp-tier, ...
python scenarios/RoleIntro/render.py                       # out/RoleIntro/png/ (about 80 s)
```

## Using it

```csharp
var director = new RoleIntroDirector { Options = { Cjk = CjkMode.Draw } };
director.FallbackHintShown += (conn, lines) => /* HsmAdapter: one hint, stable key "role-intro" */;
director.FallbackHintHidden += conn => /* remove that hint */;

// After a role change made with ShowIntroScreen off:
director.Play(player.ReferenceHub, player.ConnectionId, new RoleIntroData
{
    Kind = RoleIntroKind.FullIntro,
    Family = RoleIntroFamily.Foundation,
    Code = "MTF NU-7", CodeSuffix = "HAMMER DOWN",      // Latin only: always drawable
    Badge = "增援",                                     // optional chip
    Brand = force.DisplayNameCn,                        // 机动特遣队 Nu-7「落锤」, shown as Nu-7 落锤
    Title = "队长",                                     // rank: follows the brand on the same line
    Objective = "从敌对势力手中夺回设施。\n与<color=#5B6370>设施警卫</color>和其他<color=#0096FF>MTF特工</color>一同合作。"
              + "<color=#FFFF00>\n指挥小队推进，夺回站点。</color>",
    Help = "在控制台输入 <color=#FFFF00><b>.info</b></color> 了解关于角色的更多信息",
    Color = roleColour, FactionColor = factionPrimary,
});

director.Update(Time.deltaTime);           // every frame, or from a 10–20 Hz coroutine
director.Cancel(connectionId);             // on death, leave or a role change that brings its own intro
director.Clear();                          // on round restart
```

### Native wording

Full intros use the native intro's own Chinese (client `Translations/zh_Hans/`), so ours read like the game's:

- **Lead:** 「你 是」, with the space, as in `Facility.txt`. It is the `Lead` default.
- **Title:** the native class-name form, one line in the role colour: the faction, a space, then the rank
  (`Class_Names.txt`: 九尾狐 指挥官, 混沌分裂者 步枪手). Ours: the force's `display_name_cn` as `Brand`, a space, then
  the sub-role as `Title`, all one string at one size (蛇之手 奇术师, Nu-7 落锤 队长, 混沌分裂者 冠陨 伽玛行动官). The
  component passes the brand through `RoleIntroText.Brand`, which drops the generic 机动特遣队/MTF prefix (native says
  九尾狐, not 机动特遣队 九尾狐) and the 「」, which native titles never carry. Colours stay our own. The title is
  always the native 150 px; past the native 80 % width (1536 px) it wraps, the brand over the sub-role (混沌分裂者 冠陨 /
  伽玛行动官), as the native intro wraps long English names. A brand too long for one line on its own uses its
  common short form instead (MC&D, not Marshall, Carter & Dark Ltd.); only then would the line shrink, down to 96 px.
  SCPs are their designation with no brand (SCP-001-A, like SCP-049-2); event teams are their team name.
- **Description:** `Class_Descriptions.txt` style. Goal sentences one per line, team names in the native team
  colours (`<color=#F00>SCP</color>`, `<color=#0096FF>MTF特工</color>`, `<color=#5B6370>设施警卫</color>`,
  `<color=#008F1E>混沌分裂者</color>`, `<color=#0096FF>基金会人员</color>`), then mechanics on `<color=#FFFF00>`
  lines. SCPs open with the native three lines: 消灭所有人类。阻止人类逃离设施。与其它SCP合作。 Leave `Flavor`
  empty in full intros; the native intro has no mood line.
- **Help:** the native 按下 F1 了解关于角色的更多信息 with our console command:
  `在控制台输入 <color=#FFFF00><b>.info</b></color> 了解关于角色的更多信息`.
- **Faction line:** use the native team names from `Teams.txt` (SCP项目, 基金会势力, 混沌分裂者) where a role has no
  brand.

Upgrade cards have no native counterpart and keep leading with the sub-role, since that is what changed.

From ReinforcementsSystem, `Brand` is `RoleIntroText.Brand(display_name_cn)`, `Title` is the sub-role's `NameCn` (as a
rank, without the force's name repeated), `Color` is the sub-role's `AccentColor`, `FactionColor` is the
announcement profile's `PrimaryColor`, and `Objective` is written in the native description style above.

## Native reference

The full intro copies the native intro as the 14.2.9 client draws it in Simplified Chinese at its Regular
speed. The source is the local capture reference
`.references/scpsl-client-native-intros-14.2.9-zh_Hans/` (git-ignored): every role's intro at 1920x1080 with
stills, clips, a contact sheet and a README with the full measurements and colours. Our players use the
Chinese client, so measure against that, not the English layout (Chinese names are about 1.4x taller and the
help line about 2x larger than in English).

| Element | Native (zh_Hans, 1080p) | Ours |
| --- | --- | --- |
| 你 是 | White, regular, about 56 px, glyphs y 303-354 (centre 30.4 %), dark outline | `Lead`, Roboto Regular 56 px, band at 27.4 % |
| Role name | Role colour, Noto Sans CJK SC **Regular** about 150 px (143 px glyphs, y 392-534, centre 42.8 %); Latin SCP names in the game's heavy display face, 104 px caps | Regular 150 px for Chinese, Roboto Bold for Latin-only; shrinks to fit 1536 px (80 %), down to 96 px; centred on 42.8 % |
| Description | White, regular, 28 px, 33 px pitch, first line top y 585 (54.2 %), centred, team names in their colours at the same size | Roboto Regular 28 px from 53.8 %, top-anchored; the small family code row sits in the native empty gap at 50.4 % |
| Help | 按下 F1 了解关于角色的更多信息, white, about 48 px, F1 bold `#FFFF00`, centre 75.5 % | Same with `.info`, 48 px, band at 72.9 % |
| Backing | None; the world stays visible | None |

| Beat (from the role change) | Native | Ours (`RoleIntroTiming`) |
| --- | --- | --- |
| Name and 你 是 | In by 0.3 s | `Punch` 0.3 s (family entrance) |
| Description | 1.1-1.4 s | `DetailAt` 1.1, `LineFade` 0.35 |
| Help | 2.1-2.45 s | `HelpAt` 2.1 |
| Dim | From 5.6 s to about 0.72 by 7 s, then holds | `DimAt` 5.6, `DimFade` 1.4, `DimTo` 0.72 |
| Out | About 14 s after the start | `Hold` 13.4, `FadeOut` 0.6 |
| New role mid-intro | Replaced by the new intro; a role with no intro leaves the old one up | `Play` replaces the player's intro |

`png/native-vs-ours.png` shows the native NTF Captain intro beside ours at the same beats (the capture clip
covers 8.2 s). The client's Fast intro mode, which players can choose, is shorter and not measured.

## Families

The colours always come from the data. The family only changes the motifs.

| Family | Rule beside the code | Card bar | Chip | Code separator | Entrance |
| --- | --- | --- | --- | --- | --- |
| Foundation (MTF, Nu-7, Delta-4, Omega-1, O-11, staff) | Solid 2 px | Solid | Filled square | ` · ` | Native pop |
| SCP (SCPs, SCP-001-A, SCP-999-like Tutorial roles) | Closed by an upright tick, a containment bracket | Bracket `[`, closes first | Outlined | ` · ` | Slam: 115 % down to 100 % |
| Chaos Insurgency | Three slanted strokes, then a thin rule | Three segments | Filled square | ` // ` | Pop with a sideways jolt; the card overshoots |
| GOC | Hollow square node and a thin rule | Hollow node on top | Outlined | ` \| ` | Native pop |
| Serpent's Hand | ◇──◆, the creed rule | Diamond on top | Outlined | ` · ` | Rises 14 px |
| MC&D | Two hairlines, letterhead style | Two hairline bars | Outlined | ` · `, wider tracking | Plain fade, no pop |
| Event | Dashes | Dashes | Pill | ` · ` | Pop past 100 % and back |

## Upgrade card

The card sits at the lower left (left 5.5 %, bottom 17 %) with the family's colour bar. It shows the
badge, the code and an optional faction line, then the title at 46 px, then the flavor and objective.
The lower-left band is free in every HSM capture. The HUD uses the top centre, the top right, the
left at about 30 % height (mission tracker), the bottom and the centre. The card has its own backing,
so it stays readable on bright scenes. It fades in for 0.45 s with a 36 px slide, holds for 3–6.5 s
depending on the text, and fades out for 0.6 s.

## Network cost

Animation is stepped on the server at `StepRate` (12/s). Time is snapped to whole steps, and each
property is written only when its value changes, so a hold costs nothing. Building a full intro is
about 140–170 writes, because the motifs add small elements. The card is about 70. If the beta
exposes UI Toolkit transitions, send each end state once and let the client animate.

## Layering

`SortOrder` bands for DisplayKit canvases:

| SortOrder | Use |
| --- | --- |
| 0–99 | Persistent HUD widgets |
| 100–199 | Toasts and prompts |
| **300** | Role intro |
| 400+ | Full-screen cinematic overlays (fade to black, letterbox), which cover the intro |
| 900+ | Admin and debug |

## Lifecycle

- **Create:** `Play` after the role change. Each player gets their own canvas, spawned only to
  that player.
- **Destroy:** the component destroys its canvas when the fade-out ends. `Play` for a player who
  still has an intro destroys the old one first, as the native intro does. `Cancel` handles death,
  leaving, and a role change that brings its own intro. `Clear` handles the round restart. The
  `lifecycle` scenario covers the replace and leave cases.
- The director is keyed by connection ID. In the preview, `ReferenceHub` equality is a Unity native call.

## Arrival cinematics

A squad with its own arrival cinematic should call `Play` when the cinematic hands control back, not
at the role change. If the cinematic's title card already shows the force name and goal, the full
intro can still run: it is the personal part (your sub-role, your brief). The NewPlayerGuide controls
card should come after the intro's `Duration`, so the two never overlap.

## Without CJK in DisplayKit

DisplayKit's fonts (Default/Inter, Liberation Sans, Roboto and Roboto Mono) have no Chinese glyphs.
Whether the 15.0 client falls back to a CJK font is unknown. The 14.2 client ships Noto Sans CJK SC
Regular only, so bold CJK would be synthesised.

- `CjkMode.Draw` assumes the client falls back. If it does not, every Chinese line turns into boxes
  (`worst/full-draw-on-no-cjk-*`).
- `CjkMode.Omit` leaves out every element whose text contains CJK. The full intro keeps the slots of
  the missing lines, so the Latin code row and motif stay on their native anchor. The card keeps its
  bar, Latin badge and code. The left-out lines come back as `RoleIntroLine`s (kind + text) through
  `FallbackHintShown`. The plugin shows them as one HsmAdapter hint: centred at the native anchors for
  a full intro, left-aligned just above the card for a card. The hint appears once the intro has
  settled and is removed when the fade-out starts, because hints cannot fade.

HSM scales with screen height, and DisplayKit may use constant pixels, so at 720p the hint and the
DisplayKit rows do not line up exactly (`worst/full-omit-1280x720.png`). Choose the mode once per
server after testing the beta client.
