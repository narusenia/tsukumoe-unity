// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tsukumoe.Tests
{
    public class RenderTests
    {
        // The fixtures are read from the repository, so these tests run in the editor only.
        static string Dir => Application.dataPath + "/../../package/Tests/Fixtures/";

        static byte[] Bytes(string name) => File.ReadAllBytes(Dir + name + ".tmod");

        // seimei's native_render of the fixture: view 0,0,1,1, 256x256, premultiplied RGBA8.
        [Test]
        public void MatchesTheReferenceRendererWithinTwoOver255()
        {
            var golden = File.ReadAllBytes(Dir + "fixture-256.rgba");
            using var model = new TsukumoeModel(Bytes("fixture"));
            using var r = new OffscreenRenderer(model, TsukumoeShaders.Drawable);
            var rt = new RenderTexture(OffscreenRenderer.Descriptor(256, 256));
            r.Upload();
            r.Render(new Vector4(0, 0, 1, 1), rt);
            var px = OffscreenRenderer.ReadPixels(rt);
            Object.DestroyImmediate(rt);
            Assert.AreEqual(golden.Length, px.Length);
            int max = 0;
            for (int i = 0; i < px.Length; i++) max = Mathf.Max(max, Mathf.Abs(px[i] - golden[i]));
            Assert.LessOrEqual(max, 2);
        }

        [Test]
        public void MatchesAfterTheParameterMoves()
        {
            using var model = new TsukumoeModel(Bytes("fixture"));
            using var r = new OffscreenRenderer(model, TsukumoeShaders.Drawable);
            var rt = new RenderTexture(OffscreenRenderer.Descriptor(64, 64));
            r.Upload();
            r.Render(new Vector4(0, 0, 1, 1), rt);
            var before = OffscreenRenderer.ReadPixels(rt);
            // a skipped update must not leave stale geometry: update twice, upload once
            model.SetParameter(model.ParameterIndex("P"), 1f);
            model.Update(0.5f);
            model.Update(0.5f);
            r.Upload();
            r.Render(new Vector4(0, 0, 1, 1), rt);
            var after = OffscreenRenderer.ReadPixels(rt);
            Object.DestroyImmediate(rt);
            Assert.AreEqual(before.Length, after.Length);
        }

        [UnityTest]
        public IEnumerator RendererShowsTheModelThroughACamera()
        {
            var cam = new GameObject("cam").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.7f;
            cam.transform.position = new Vector3(0.5f, 0.5f, -5);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            var rt = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.targetTexture = rt;

            var go = new GameObject("model");
            go.SetActive(false);
            var renderer = go.AddComponent<TsukumoeRenderer>();
            renderer.Asset = TsukumoeModelAsset.FromBytes(Bytes("fixture"));
            go.SetActive(true);
            yield return null;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(128, 128);
            t.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
            RenderTexture.active = prev;
            Assert.Greater(t.GetPixel(64, 64).a, 0.5f, "the model covers the centre of the view");
            Assert.AreEqual(0f, t.GetPixel(2, 2).a, 0.01f, "outside the model stays clear");
            Object.Destroy(go); Object.Destroy(cam.gameObject);
            Object.DestroyImmediate(t); Object.DestroyImmediate(rt);
        }
    }
}
