using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace PewPewPew.Editor
{
    public static class MountRemoteValidationBuild
    {
        public static void Build()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-mountBuildPath");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Supply -mountBuildPath with the validation executable path.");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/GameScene.unity" },
                locationPathName = args[index + 1],
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
                extraScriptingDefines = new[] { "PEW_MOUNT_REMOTE_VALIDATION" }
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Remote validation build failed: {report.summary.result}.");
            UnityEngine.Debug.Log("MOUNT_REMOTE_BUILD_PASSED");
        }
    }
}
