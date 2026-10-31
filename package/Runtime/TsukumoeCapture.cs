// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Tsukumoe
{
    /// <summary>
    /// Writes the pixels and the draw list of a model, for comparing environments (M3-06).
    /// Pixels: premultiplied RGBA8, no header, top row first (seimei's native_render output).
    /// Draw list: JSON with the drawing order and every per-drawable number.
    /// </summary>
    public static unsafe class TsukumoeCapture
    {
        [Serializable]
        public class Drawable
        {
            public uint mesh, texture;
            public int blend_mode;
            public bool double_sided, mask_inverted;
            public uint[] masks;
            public float opacity, draw_order;
            public float[] multiply_color, screen_color, positions, uvs;
        }

        [Serializable]
        public class DrawList { public Drawable[] drawables; }

        /// <summary>Drawables back to front, like the draw list <c>order</c>.</summary>
        public static string DrawListJson(TsukumoeModel model)
        {
            uint count = model.DrawBuffers().drawable_count;
            var list = new List<Drawable>();
            for (uint pos = 0; pos < count; pos++)
            {
                var d = model.Drawable(pos);
                list.Add(new Drawable
                {
                    mesh = d.mesh, texture = d.texture, blend_mode = d.blend_mode,
                    double_sided = d.double_sided != 0, mask_inverted = d.mask_inverted != 0,
                    masks = new ReadOnlySpan<uint>(d.masks, (int)d.mask_count).ToArray(),
                    opacity = d.opacity, draw_order = d.draw_order,
                    multiply_color = new[] { d.multiply_r, d.multiply_g, d.multiply_b },
                    screen_color = new[] { d.screen_r, d.screen_g, d.screen_b },
                    positions = new ReadOnlySpan<float>(d.positions, (int)d.vertex_count * 2).ToArray(),
                    uvs = new ReadOnlySpan<float>(d.uvs, (int)d.vertex_count * 2).ToArray(),
                });
            }
            return JsonUtility.ToJson(new DrawList { drawables = list.ToArray() });
        }


        /// <summary>
        /// Plays a parity case (seimei <c>tests/parity/cases/*.json</c>): the model is loaded (which evaluates the
        /// default frame once), then every step applies <c>params</c>, starts <c>motion</c> on layer 0 at Normal
        /// priority and <c>expression</c>, and calls <c>Update(dt)</c> <c>repeat</c> times. Returns the pixels
        /// (premultiplied RGBA8, top row first) and the draw-list JSON.
        /// </summary>
        public static (byte[] Rgba, string Json) RunCase(byte[] tmod, string caseJson)
        {
            var c = (Dictionary<string, object>)MiniJson.Parse(caseJson);
            var size = (List<object>)c["size"];
            var v = (List<object>)c["view"];
            int width = (int)(double)size[0], height = (int)(double)size[1];
            var view = new Vector4((float)(double)v[0], (float)(double)v[1], (float)(double)v[2], (float)(double)v[3]);

            using var model = new TsukumoeModel(tmod);
            foreach (Dictionary<string, object> s in (List<object>)c["steps"])
            {
                if (s.TryGetValue("params", out var ps))
                    foreach (var kv in (Dictionary<string, object>)ps)
                    {
                        int i = model.ParameterIndex(kv.Key);
                        if (i < 0) throw new ArgumentException("no parameter " + kv.Key);
                        model.SetParameter(i, (float)(double)kv.Value);
                    }
                if (s.TryGetValue("motion", out var m)) model.PlayMotion((string)m, 0, MotionPriority.Normal);
                if (s.TryGetValue("expression", out var e)) model.PlayExpression((string)e);
                int repeat = s.TryGetValue("repeat", out var r) ? (int)(double)r : 1;
                for (int k = 0; k < repeat; k++) model.Update((float)(double)s["dt"]);
            }
            using var renderer = new OffscreenRenderer(model, TsukumoeShaders.Drawable);
            var rt = new RenderTexture(OffscreenRenderer.Descriptor(width, height));
            renderer.Upload();
            renderer.Render(view, rt);
            var rgba = OffscreenRenderer.ReadPixels(rt);
            rt.Release();
            return (rgba, DrawListJson(model));
        }

        /// <summary>Command line: -tsukumoe-cases dir -tsukumoe-models dir -tsukumoe-out dir; writes <c>&lt;case&gt;.rgba</c> and <c>.json</c> for every case.</summary>
        public static void RunCasesFromCommandLine()
        {
            string Arg(string name)
            {
                var a = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(a, name);
                return i >= 0 && i + 1 < a.Length ? a[i + 1] : throw new ArgumentException("missing " + name);
            }
            string cases = Arg("-tsukumoe-cases"), models = Arg("-tsukumoe-models"), outDir = Arg("-tsukumoe-out");
            Directory.CreateDirectory(outDir);
            foreach (var file in Directory.GetFiles(cases, "*.json"))
            {
                var text = File.ReadAllText(file);
                var model = (string)((Dictionary<string, object>)MiniJson.Parse(text))["model"];
                var (rgba, json) = RunCase(File.ReadAllBytes(Path.Combine(models, model)), text);
                var name = Path.GetFileNameWithoutExtension(file);
                File.WriteAllBytes(Path.Combine(outDir, name + ".rgba"), rgba);
                File.WriteAllText(Path.Combine(outDir, name + ".json"), json);
            }
        }

        /// <summary>Renders the default pose and writes <c>out.rgba</c> and <c>out.json</c>.</summary>
        public static void Write(byte[] tmod, Vector4? view, int width, int height, string outPrefix)
        {
            using var model = new TsukumoeModel(tmod);
            using var r = new OffscreenRenderer(model, TsukumoeShaders.Drawable);
            var rt = new RenderTexture(OffscreenRenderer.Descriptor(width, height));
            r.Upload();
            r.Render(view ?? r.Bounds(), rt);
            File.WriteAllBytes(outPrefix + ".rgba", OffscreenRenderer.ReadPixels(rt));
            File.WriteAllText(outPrefix + ".json", DrawListJson(model));
            rt.Release();
        }

        /// <summary>Command line: -tsukumoe-tmod f -tsukumoe-size WxH -tsukumoe-out prefix [-tsukumoe-view x0,y0,x1,y1].</summary>
        public static bool RunFromCommandLine()
        {
            string Arg(string name)
            {
                var a = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(a, name);
                return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
            }
            var path = Arg("-tsukumoe-tmod");
            if (path == null) return false;
            var size = Arg("-tsukumoe-size").Split('x');
            Vector4? view = null;
            if (Arg("-tsukumoe-view") is { } v)
            {
                var p = v.Split(',');
                view = new Vector4(F(p[0]), F(p[1]), F(p[2]), F(p[3]));
            }
            Write(File.ReadAllBytes(path), view, int.Parse(size[0]), int.Parse(size[1]), Arg("-tsukumoe-out"));
            return true;

            static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Put it in a scene of a build; with <c>-tsukumoe-tmod</c> on the command line it captures and quits.</summary>
    public sealed class TsukumoeCaptureOnStart : MonoBehaviour
    {
        void Start()
        {
            try { if (!TsukumoeCapture.RunFromCommandLine()) return; }
            catch (Exception e) { Debug.LogError("TSUKUMOE FAIL " + e); Application.Quit(1); return; }
            Application.Quit(0);
        }
    }
}
