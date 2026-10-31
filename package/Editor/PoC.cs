// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tsukumoe.Editor
{
    /// <summary>
    /// Batchmode entry: -executeMethod Tsukumoe.Editor.PoC.Render -tmod f -size WxH -out f.rgba [-view x0,y0,x1,y1]
    /// Writes premultiplied RGBA8 (no header), like seimei's native_render example.
    /// </summary>
    public static class PoC
    {
        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        public static void Render()
        {
            try
            {
                var size = Arg("-size").Split('x');
                int w = int.Parse(size[0]), h = int.Parse(size[1]);
                var shader = TsukumoeShaders.Drawable;
                using var model = new TsukumoeModel(File.ReadAllBytes(Arg("-tmod")));
                using var r = new OffscreenRenderer(model, shader);
                var vs = Arg("-view");
                Vector4 view = r.Bounds();
                if (vs != null)
                {
                    var p = vs.Split(',');
                    float F(int i) => float.Parse(p[i], CultureInfo.InvariantCulture);
                    view = new Vector4(F(0), F(1), F(2), F(3));
                }
                Debug.Log($"TSUKUMOE VIEW {view.x.ToString("R", CultureInfo.InvariantCulture)},{view.y.ToString("R", CultureInfo.InvariantCulture)},{view.z.ToString("R", CultureInfo.InvariantCulture)},{view.w.ToString("R", CultureInfo.InvariantCulture)} colorspace={PlayerSettings.colorSpace} api={SystemInfo.graphicsDeviceType}");
                var rt = new RenderTexture(OffscreenRenderer.Descriptor(w, h));
                r.Upload();
                r.Render(view, rt);
                File.WriteAllBytes(Arg("-out"), OffscreenRenderer.ReadPixels(rt));
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("TSUKUMOE FAIL " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
