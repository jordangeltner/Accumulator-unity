using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Accumulator.Editor
{
    public static class WindowsBuildTools
    {
        [MenuItem("Accumulator/Build Windows x64 ZIP")]
        public static void BuildWindows()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the open scenes before building.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Install Windows build support for this Unity Editor first.");

            // A fresh output folder prevents files from an older build entering the ZIP.
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Windows",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
            string playerFolder = Path.Combine(root, "Accumulator");
            Directory.CreateDirectory(playerFolder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { PrototypeSceneTools.ScenePath },
                locationPathName = Path.Combine(playerFolder, "Accumulator.exe"),
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows build {report.summary.result}; see the Unity Console. Output: {root}");

            string archive = Path.Combine(root, "Accumulator-windows-x64.zip");
            ZipFile.CreateFromDirectory(playerFolder, archive, System.IO.Compression.CompressionLevel.Optimal, true);
            Debug.Log($"Windows x64 package ready: {archive}");
        }
    }
}
