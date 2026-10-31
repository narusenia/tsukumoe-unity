// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using System;
using UnityEditor;
using UnityEngine;

namespace Tsukumoe.Editor
{
    /// <summary>
    /// -executeMethod Tsukumoe.Editor.ParityTool.Run -tsukumoe-cases dir -tsukumoe-models dir -tsukumoe-out dir
    /// (batch mode with graphics): writes the pixels and draw list of every parity case. See scripts/parity.sh.
    /// </summary>
    public static class ParityTool
    {
        public static void Run()
        {
            try
            {
                TsukumoeCapture.RunCasesFromCommandLine();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("TSUKUMOE PARITY FAIL " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
