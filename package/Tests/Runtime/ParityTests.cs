// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tsukumoe.Tests
{
    /// <summary>
    /// Every parity case against seimei's native output (the golden files in Fixtures/parity, see
    /// scripts/import-golden.sh): pixels within 2/255 per channel, draw-list numbers within 1e-4,
    /// order, mesh, texture, blend mode, flags and masks equal. Editor only (reads the repository).
    /// </summary>
    public class ParityTests
    {
        static string Dir => Application.dataPath + "/../../package/Tests/Fixtures/parity/";

        static IEnumerable<string> Cases() =>
            Directory.GetFiles(Dir + "cases", "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n);

        // Pixels that sit exactly on a triangle edge and are covered in Unity but not in the native
        // renderer (different rasterizer tie-breaking); a case may exceed 2/255 by at most this many pixels.
        static readonly Dictionary<string, int> KnownEdgePixels = new() { ["reference-physics-settle"] = 2 };

        [TestCaseSource(nameof(Cases))]
        public void MatchesNative(string name)
        {
            var text = File.ReadAllText(Dir + "cases/" + name + ".json");
            var tmodName = text.Split("\"model\"")[1].Split('"')[1];
            var (rgba, json) = TsukumoeCapture.RunCase(File.ReadAllBytes(Dir + "models/" + tmodName), text);

            var golden = File.ReadAllBytes(Dir + "golden/" + name + ".rgba");
            Assert.AreEqual(golden.Length, rgba.Length, "image size");
            int over = 0;
            for (int i = 0; i < rgba.Length; i += 4)
                for (int k = 0; k < 4; k++)
                    if (Mathf.Abs(rgba[i + k] - golden[i + k]) > 2) { over++; break; }
            KnownEdgePixels.TryGetValue(name, out var allowed);
            Assert.LessOrEqual(over, allowed, "pixels over 2/255");

            var want = JsonUtility.FromJson<TsukumoeCapture.DrawList>(File.ReadAllText(Dir + "golden/" + name + ".json")).drawables;
            var got = JsonUtility.FromJson<TsukumoeCapture.DrawList>(json).drawables;
            Assert.AreEqual(want.Length, got.Length, "drawable count");
            for (int i = 0; i < want.Length; i++)
            {
                var (w, g) = (want[i], got[i]);
                Assert.AreEqual(w.mesh, g.mesh, $"[{i}].mesh (order)");
                Assert.AreEqual(w.texture, g.texture, $"[{i}].texture");
                Assert.AreEqual(w.blend_mode, g.blend_mode, $"[{i}].blend_mode");
                Assert.AreEqual(w.double_sided, g.double_sided, $"[{i}].double_sided");
                Assert.AreEqual(w.mask_inverted, g.mask_inverted, $"[{i}].mask_inverted");
                CollectionAssert.AreEqual(w.masks, g.masks, $"[{i}].masks");
                Assert.AreEqual(w.opacity, g.opacity, 1e-4f, $"[{i}].opacity");
                Assert.AreEqual(w.draw_order, g.draw_order, 1e-4f, $"[{i}].draw_order");
                Near(w.multiply_color, g.multiply_color, $"[{i}].multiply_color");
                Near(w.screen_color, g.screen_color, $"[{i}].screen_color");
                Near(w.positions, g.positions, $"[{i}].positions");
                Near(w.uvs, g.uvs, $"[{i}].uvs");
            }
        }

        static void Near(float[] a, float[] b, string what)
        {
            Assert.AreEqual(a.Length, b.Length, what + " length");
            for (int i = 0; i < a.Length; i++) Assert.AreEqual(a[i], b[i], 1e-4f, $"{what}[{i}]");
        }
    }
}
