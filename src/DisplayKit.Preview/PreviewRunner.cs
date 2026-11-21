using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DisplayKit.Elements;

namespace DisplayKit.Preview
{
    /// <summary>
    /// Runner entry (hosted by DisplayKit.Preview.Runner.exe): <c>--assembly Scenarios.dll [--scenario name] [--out dir] [--probe dir;dir] [--list]</c>.
    /// Writes one <c>&lt;scenario&gt;.json</c> per scenario plus <c>index.json</c>.
    /// </summary>
    public static class PreviewRunner
    {
        private static readonly List<string> ProbeDirs = new List<string>();

        public static int Main(string[] args)
        {
            string assemblyPath = null, outDir = "out", only = null;
            bool list = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--assembly": assemblyPath = args[++i]; break;
                    case "--scenario": only = args[++i]; break;
                    case "--out": outDir = args[++i]; break;
                    case "--probe": ProbeDirs.AddRange(args[++i].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)); break;
                    case "--list": list = true; break;
                    default:
                        Console.Error.WriteLine("unknown argument " + args[i]);
                        return 2;
                }
            }

            if (assemblyPath == null)
            {
                Console.Error.WriteLine("usage: DisplayKit.Preview.Runner --assembly Scenarios.dll [--scenario name] [--out dir] [--probe dirs] [--list]");
                return 2;
            }

            assemblyPath = Path.GetFullPath(assemblyPath);
            ProbeDirs.Insert(0, Path.GetDirectoryName(assemblyPath));
            foreach (AssemblyMetadataAttribute meta in (Assembly.GetEntryAssembly() ?? typeof(PreviewRunner).Assembly).GetCustomAttributes<AssemblyMetadataAttribute>())
                if (meta.Key == "ProbeDir" && !string.IsNullOrEmpty(meta.Value))
                    ProbeDirs.Add(meta.Value);
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;

            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            var scenarios = assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Select(m => (method: m, attr: m.GetCustomAttribute<DisplayKitScenarioAttribute>()))
                .Where(x => x.attr != null)
                .Select(x => (x.method, name: x.attr.Name ?? x.method.Name, description: x.attr.Description))
                .OrderBy(x => x.name, StringComparer.Ordinal)
                .ToList();

            if (list)
            {
                foreach (var s in scenarios)
                    Console.WriteLine(s.name + (s.description != null ? "  - " + s.description : ""));
                return 0;
            }

            Directory.CreateDirectory(outDir);
            int failures = 0;
            foreach (var s in scenarios)
            {
                if (only != null && !string.Equals(only, s.name, StringComparison.OrdinalIgnoreCase))
                    continue;
                string json = Run(s.method, s.name, s.description, Path.GetFileName(assemblyPath), out bool failed);
                if (failed)
                    failures++;
                string file = Path.Combine(outDir, Sanitize(s.name) + ".json");
                File.WriteAllText(file, json, new UTF8Encoding(false));
                Console.WriteLine((failed ? "FAILED " : "wrote ") + file);
            }

            WriteIndex(outDir);
            if (only != null && !scenarios.Any(s => string.Equals(only, s.name, StringComparison.OrdinalIgnoreCase)))
            {
                Console.Error.WriteLine("no scenario named " + only);
                return 3;
            }
            return failures == 0 ? 0 : 1;
        }

        private static string Run(MethodInfo method, string name, string description, string assemblyName, out bool failed)
        {
            PreviewWorld.Reset();
            SceneRecorder recorder = new SceneRecorder();
            PreviewContext ctx = new PreviewContext(recorder);
            recorder.Context = ctx;
            string error = null;
            try
            {
                method.Invoke(null, method.GetParameters().Length == 0 ? null : new object[] { ctx });
            }
            catch (TargetInvocationException ex)
            {
                error = ex.InnerException?.ToString() ?? ex.ToString();
            }

            if (recorder.FrameCount == 0 || recorder.HasUncapturedChanges())
            {
                try { recorder.Capture(recorder.FrameCount == 0 ? "final" : "end", ctx.Time); }
                catch (Exception ex) { error = (error == null ? "" : error + "\n") + ex; }
            }

            failed = error != null;
            if (failed)
                Console.Error.WriteLine($"[{name}] {error}");
            return recorder.Build(name, description, assemblyName, error);
        }

        private static void WriteIndex(string outDir)
        {
            Json j = new Json();
            j.BeginArray();
            foreach (string file in Directory.GetFiles(outDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                string n = Path.GetFileNameWithoutExtension(file);
                if (n != "index")
                    j.Value(n);
            }
            j.EndArray();
            File.WriteAllText(Path.Combine(outDir, "index.json"), j.ToString(), new UTF8Encoding(false));
        }

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string file = new AssemblyName(args.Name).Name + ".dll";
            foreach (string dir in ProbeDirs)
            {
                string path = Path.Combine(dir, file);
                if (File.Exists(path))
                    return Assembly.LoadFrom(path);
            }
            return null;
        }
    }
}
