using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Called by CI: buildMethod: BuildScript.BuildAndroid
public static class BuildScript
{
    public static void BuildAndroid()
    {
        PlayerSettings.companyName = "YourName";
        PlayerSettings.productName = "Fight without Fear";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.yourname.fightwithoutfear");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        EditorUserBuildSettings.buildAppBundle = false; // APK, not AAB

        // No scene yet? Generate the graybox test scene.
        if (!File.Exists(SceneBuilder.ScenePath)) SceneBuilder.Create();

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
            scenes = new[] { SceneBuilder.ScenePath };

        Directory.CreateDirectory("build/Android");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "build/Android/FightWithoutFear.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        });

        bool ok = report.summary.result == BuildResult.Succeeded;
        Debug.Log("Build result: " + report.summary.result);
        EditorApplication.Exit(ok ? 0 : 1);
    }
}
