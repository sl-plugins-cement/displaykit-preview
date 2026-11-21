using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace DisplayKitPreview
{
    /// <summary>
    /// Renders scene-JSON frames with UI Toolkit into PNGs. Started by the player (or the editor) with
    /// <c>-dkJobs jobs.json</c>; each job is { scene, frame, viewer, width, height, scale, backdrop, cjk, out, layout }.
    /// Each visible canvas becomes one UIDocument on a shared panel, ordered by SortOrder (guess: one document per canvas).
    /// </summary>
    public sealed class RenderHost : MonoBehaviour
    {
        private const string DefaultFont = "Default";

        private static readonly Dictionary<string, string> FontFiles = new Dictionary<string, string>
        {
            ["Default"] = "Inter-Regular",
            ["LiberationSans"] = "LiberationSans-Regular",
            ["RobotoRegular"] = "Roboto-Regular",
            ["RobotoItalic"] = "Roboto-Italic",
            ["RobotoBold"] = "Roboto-Bold",
            ["RobotoBoldItalic"] = "Roboto-BoldItalic",
            ["RobotoLight"] = "Roboto-Light",
            ["RobotoLightItalic"] = "Roboto-LightItalic",
            ["RobotoMedium"] = "Roboto-Medium",
            ["RobotoMediumItalic"] = "Roboto-MediumItalic",
            ["RobotoThin"] = "Roboto-Thin",
            ["RobotoThinItalic"] = "Roboto-ThinItalic",
            ["RobotoMonoRegular"] = "RobotoMono-Regular",
            ["RobotoMonoItalic"] = "RobotoMono-Italic",
            ["RobotoMonoBold"] = "RobotoMono-Bold",
            ["RobotoMonoBoldItalic"] = "RobotoMono-BoldItalic",
            ["RobotoMonoLight"] = "RobotoMono-Light",
            ["RobotoMonoLightItalic"] = "RobotoMono-LightItalic",
            ["RobotoMonoMedium"] = "RobotoMono-Medium",
            ["RobotoMonoMediumItalic"] = "RobotoMono-MediumItalic",
            ["RobotoMonoThin"] = "RobotoMono-Thin",
            ["RobotoMonoThinItalic"] = "RobotoMono-ThinItalic",
        };

        private readonly Dictionary<string, FontAsset> _fonts = new Dictionary<string, FontAsset>();
        private FontAsset _cjkFallback;
        private string _cjkMode = "none";

        public static string JobsPathOverride;

        private IEnumerator Start()
        {
            string jobsPath = JobsPathOverride ?? Arg("-dkJobs");
            if (jobsPath == null)
            {
                Debug.LogError("[dk] missing -dkJobs <jobs.json>");
                Quit(2);
                yield break;
            }

            List<object> jobs = (List<object>)((Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(jobsPath)))["jobs"];
            SceneBuilder.FontResolver = ResolveFont;
            int failures = 0;
            foreach (object j in jobs)
            {
                Dictionary<string, object> job = (Dictionary<string, object>)j;
                bool ok = false;
                IEnumerator run = RenderJob(job, r => ok = r);
                while (true)
                {
                    object current;
                    try
                    {
                        if (!run.MoveNext()) break;
                        current = run.Current;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[dk] job failed: " + ex);
                        break;
                    }
                    yield return current;
                }
                if (!ok) failures++;
            }

            Debug.Log($"[dk] done, {jobs.Count - failures}/{jobs.Count} rendered");
            Quit(failures == 0 ? 0 : 1);
        }

        private IEnumerator RenderJob(Dictionary<string, object> job, Action<bool> done)
        {
            string scenePath = (string)job["scene"];
            int frameIndex = job.TryGetValue("frame", out object fi) && fi != null ? Convert.ToInt32(fi) : -1;
            int width = Convert.ToInt32(job["width"]);
            int height = Convert.ToInt32(job["height"]);
            string scale = job.TryGetValue("scale", out object sc) ? (string)sc : "constant";
            _cjkMode = job.TryGetValue("cjk", out object cj) ? (string)cj : "none";
            List<object> backdrop = job.TryGetValue("backdrop", out object bd) ? (List<object>)bd : null;

            Dictionary<string, object> scene = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(scenePath));
            List<object> frames = (List<object>)scene["frames"];
            Dictionary<string, object> frame = (Dictionary<string, object>)frames[frameIndex < 0 ? frames.Count - 1 : frameIndex];
            List<object> players = (List<object>)frame["players"];
            int viewer = job.TryGetValue("viewer", out object vw) && vw != null ? Convert.ToInt32(vw) : players.Count > 0 ? Convert.ToInt32(players[0]) : 0;

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();

            PanelSettings ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.themeStyleSheet = Resources.Load<ThemeStyleSheet>("DkRuntimeTheme");
            ps.targetTexture = rt;
            ps.clearColor = true;
            ps.colorClearValue = backdrop == null ? new Color(0.17f, 0.18f, 0.17f, 1f) : new Color(F(backdrop[0]), F(backdrop[1]), F(backdrop[2]), F(backdrop[3]));
            if (scale == "constant")
            {
                ps.scaleMode = PanelScaleMode.ConstantPixelSize;
                ps.scale = 1f;
            }
            else
            {
                string[] parts = scale.Split('x');
                ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                ps.referenceResolution = new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
                ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                ps.match = 0.5f;
            }

            List<GameObject> spawned = new List<GameObject>();
            List<(int canvasId, VisualElement root)> roots = new List<(int, VisualElement)>();
            foreach (object co in (List<object>)frame["canvases"])
            {
                Dictionary<string, object> canvas = (Dictionary<string, object>)co;
                bool visible = ((List<object>)canvas["observers"]).Cast<Dictionary<string, object>>()
                    .Any(o => Convert.ToInt32(o["id"]) == viewer && (bool)o["visible"]);
                if (!visible)
                    continue;

                int canvasId = Convert.ToInt32(canvas["id"]);
                GameObject go = new GameObject("canvas " + canvasId);
                UIDocument doc = go.AddComponent<UIDocument>();
                doc.panelSettings = ps;
                doc.sortingOrder = canvas["sortOrder"] == null ? 0 : Convert.ToSingle(canvas["sortOrder"]);
                spawned.Add(go);

                VisualElement root = doc.rootVisualElement;
                root.style.position = Position.Absolute;
                root.style.left = 0;
                root.style.top = 0;
                root.style.right = 0;
                root.style.bottom = 0;
                FontAsset defaultFont = ResolveFont(DefaultFont);
                if (defaultFont != null)
                    root.style.unityFontDefinition = FontDefinition.FromSDFFont(defaultFont);
                VisualElement built = SceneBuilder.Build((Dictionary<string, object>)canvas["root"], "c" + canvasId);
                root.Add(built);
                roots.Add((canvasId, built));
            }

            // Let layout, text generation and the panel repaint settle.
            for (int i = 0; i < 4; i++)
                yield return null;
            yield return new WaitForEndOfFrame();

            string outPath = (string)job["out"];
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Debug.Log("[dk] wrote " + outPath);

            if (job.TryGetValue("layout", out object lo) && lo is string layoutPath)
                File.WriteAllText(layoutPath, DumpLayout(roots, ps), new UTF8Encoding(false));

            foreach (GameObject go in spawned)
                Destroy(go);
            Destroy(tex);
            rt.Release();
            Destroy(rt);
            Destroy(ps);
            yield return null;
            done(true);
        }

        private static string DumpLayout(List<(int canvasId, VisualElement root)> roots, PanelSettings ps)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"renderer\":\"unity\",\"elements\":[");
            bool first = true;
            foreach ((int canvasId, VisualElement root) in roots)
            {
                foreach (VisualElement ve in Walk(root))
                {
                    Rect r = ve.worldBound;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("{\"path\":\"").Append(ve.name).Append("\",\"x\":").Append(N(r.x)).Append(",\"y\":").Append(N(r.y))
                        .Append(",\"w\":").Append(N(r.width)).Append(",\"h\":").Append(N(r.height)).Append('}');
                }
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static IEnumerable<VisualElement> Walk(VisualElement ve)
        {
            yield return ve;
            foreach (VisualElement child in ve.Children())
                foreach (VisualElement d in Walk(child))
                    yield return d;
        }

        private FontAsset ResolveFont(string fontType)
        {
            string key = fontType + "|" + _cjkMode;
            if (_fonts.TryGetValue(key, out FontAsset cached))
                return cached;

            FontAsset asset = null;
            if (FontFiles.TryGetValue(fontType, out string file))
            {
                Font font = Resources.Load<Font>("Fonts/" + file);
                if (font == null)
                    Debug.LogWarning($"[dk] font {file} missing from Assets/Resources/Fonts (run fonts.py)");
                else
                {
                    asset = FontAsset.CreateFontAsset(font);
                    asset.fallbackFontAssetTable = new List<FontAsset>();
                    if (_cjkMode == "system" && CjkFallback() is FontAsset fb)
                        asset.fallbackFontAssetTable.Add(fb);
                }
            }

            _fonts[key] = asset;
            return asset;
        }

        private FontAsset CjkFallback()
        {
            if (_cjkFallback == null)
            {
                Font cjk = Resources.Load<Font>("Fonts/NotoSansSC-Regular");
                _cjkFallback = cjk != null ? FontAsset.CreateFontAsset(cjk) : FontAsset.CreateFontAsset("Microsoft YaHei", "Regular");
            }
            return _cjkFallback;
        }

        private static float F(object o) => Convert.ToSingle(o, CultureInfo.InvariantCulture);

        private static string N(float f) => f.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static void Quit(int code)
        {
#if UNITY_EDITOR
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(code);
            else
                UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }
    }
}
