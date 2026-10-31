// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tsukumoe.Editor
{
    /// <summary>-executeMethod Tsukumoe.Editor.BuildTool.Standalone -tsukumoe-build dir : builds a player that runs the capture.</summary>
    public static class BuildTool
    {
        public static void Standalone()
        {
            var a = Environment.GetCommandLineArgs();
            string dir = a[Array.IndexOf(a, "-tsukumoe-build") + 1];
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("capture").AddComponent<TsukumoeCaptureOnStart>();
            const string path = "Assets/CaptureScene.unity";
            EditorSceneManager.SaveScene(scene, path);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { path },
                locationPathName = dir + "/capture.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            });
            Debug.Log("TSUKUMOE BUILD " + report.summary.result);
            EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}
