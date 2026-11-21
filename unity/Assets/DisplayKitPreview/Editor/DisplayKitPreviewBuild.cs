using System.IO;
using DisplayKitPreview;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Batchmode entry points:
///   Unity.exe -batchmode -quit -projectPath unity -executeMethod DisplayKitPreviewBuild.BuildPlayer
///     builds Build/DkRender.exe (render with: DkRender.exe -batchmode -dkJobs jobs.json -logFile -).
///   Unity.exe -batchmode -projectPath unity -executeMethod DisplayKitPreviewBuild.RenderInEditor -dkJobs jobs.json
///     renders in editor play mode without a player build (slower start).
/// </summary>
public static class DisplayKitPreviewBuild
{
    private const string ScenePath = "Assets/DkRender.unity";

    public static void BuildPlayer()
    {
        EnsureScene();
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.runInBackground = true;
        PlayerSettings.visibleInBackground = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 320;
        PlayerSettings.defaultScreenHeight = 180;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.productName = "DkRender";
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/DkRender.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("[dk] build " + report.summary.result + " -> " + options.locationPathName);
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    public static void RenderInEditor()
    {
        PlayerSettings.colorSpace = ColorSpace.Linear;
        EnsureScene();
        EditorSceneManager.OpenScene(ScenePath);
        string[] args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-dkJobs");
        SessionState.SetString("dkJobs", i >= 0 ? Path.GetFullPath(args[i + 1]) : "");
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void PassJobsIntoPlayMode()
    {
        string jobs = SessionState.GetString("dkJobs", "");
        if (!string.IsNullOrEmpty(jobs))
            RenderHost.JobsPathOverride = jobs;
    }

    private static void EnsureScene()
    {
        if (File.Exists(ScenePath))
            return;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject cam = new GameObject("Camera");
        Camera camera = cam.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        new GameObject("RenderHost").AddComponent<RenderHost>();
        EditorSceneManager.SaveScene(scene, ScenePath);
    }
}
