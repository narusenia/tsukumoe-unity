// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukumoe
{
    /// <summary>
    /// Draws a model into a non-sRGB render texture following rendering.md. The texture holds
    /// stored values, so the result does not depend on the project's color space.
    /// </summary>
    public sealed unsafe class OffscreenRenderer : IDisposable
    {
        // Unity treats clockwise as the front face; rendering.md uses counter-clockwise in Y-up space.
        const float CullBack = 1;
        const float CullOff = 0;

        readonly TsukumoeModel _model;
        readonly Material _single, _double;
        readonly Texture2D[] _textures;
        readonly Dictionary<uint, Mesh> _meshes = new Dictionary<uint, Mesh>();
        readonly Dictionary<uint, Native.Drawable> _drawables = new Dictionary<uint, Native.Drawable>();

        public OffscreenRenderer(TsukumoeModel model, Shader shader)
        {
            _model = model;
            _single = new Material(shader);
            _single.SetFloat("_Cull", CullBack);
            _double = new Material(shader);
            _double.SetFloat("_Cull", CullOff);
            _textures = new Texture2D[model.TextureCount];
            for (int i = 0; i < _textures.Length; i++) _textures[i] = Premultiplied(model.TexturePng(i));
        }

        static Texture2D Premultiplied(byte[] png)
        {
            // linear: true keeps the stored values (no sRGB decode).
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            t.LoadImage(png, false);
            var px = t.GetPixels32(); // independent of the format LoadImage chose
            for (int i = 0; i < px.Length; i++)
            {
                int a = px[i].a;
                px[i].r = (byte)((px[i].r * a + 127) / 255);
                px[i].g = (byte)((px[i].g * a + 127) / 255);
                px[i].b = (byte)((px[i].b * a + 127) / 255);
            }
            t.SetPixels32(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.Apply(false, false);
            return t;
        }

        /// <summary>Bounding box x0,y0,x1,y1 of every vertex of the last update.</summary>
        public Vector4 Bounds()
        {
            var b = _model.DrawBuffers();
            var r = new Vector4(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
            for (uint i = 0; i < b.vertex_count; i++)
            {
                float x = b.positions[2 * i], y = b.positions[2 * i + 1];
                r.x = Mathf.Min(r.x, x); r.y = Mathf.Min(r.y, y);
                r.z = Mathf.Max(r.z, x); r.w = Mathf.Max(r.w, y);
            }
            return r;
        }

        void Upload()
        {
            uint count = _model.DrawBuffers().drawable_count;
            _drawables.Clear();
            for (uint pos = 0; pos < count; pos++)
            {
                var d = _model.Drawable(pos);
                _drawables[pos] = d;
                if (!_meshes.TryGetValue(d.mesh, out var mesh))
                {
                    mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                    _meshes[d.mesh] = mesh;
                }
                int vc = (int)d.vertex_count, ic = (int)d.index_count;
                mesh.SetVertexBufferParams(vc,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 2, 0),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 1));
                mesh.SetVertexBufferData(new ReadOnlySpan<Vector2>(d.positions, vc).ToArray(), 0, 0, vc, 0, MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetVertexBufferData(new ReadOnlySpan<Vector2>(d.uvs, vc).ToArray(), 0, 0, vc, 1, MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetIndexBufferParams(ic, IndexFormat.UInt32);
                mesh.SetIndexBufferData(new ReadOnlySpan<uint>(d.indices, ic).ToArray(), 0, 0, ic, MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, ic), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            }
        }

        /// <summary>Renders the last update; returns premultiplied RGBA8, top row first.</summary>
        public byte[] Render(Vector4 view, int width, int height)
        {
            Upload();
            var desc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0) { sRGB = false };
            var rt = RenderTexture.GetTemporary(desc);
            var maskRt = RenderTexture.GetTemporary(desc);
            rt.filterMode = maskRt.filterMode = FilterMode.Point;
            var proj = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(view.x, view.z, view.y, view.w, -1, 1), true);

            var cmd = new CommandBuffer { name = "Tsukumoe" };
            cmd.SetGlobalMatrix("_TsukumoeVP", proj);
            cmd.SetGlobalTexture("_TsukumoeMask", maskRt);
            cmd.SetRenderTarget(rt);
            cmd.ClearRenderTarget(false, true, Color.clear);

            uint count = _model.DrawBuffers().drawable_count;
            for (uint pos = 0; pos < count; pos++)
            {
                var d = _drawables[pos];
                if (d.opacity <= 0f || d.index_count == 0) continue;
                bool masked = d.mask_count > 0;
                if (masked)
                {
                    cmd.SetRenderTarget(maskRt);
                    cmd.ClearRenderTarget(false, true, Color.clear);
                    for (uint m = 0; m < d.mask_count; m++)
                    {
                        var md = _drawables[FindPosition(d.masks[m])];
                        var mpb = new MaterialPropertyBlock();
                        mpb.SetTexture("_MainTex", _textures[md.texture]);
                        cmd.DrawMesh(_meshes[md.mesh], Matrix4x4.identity, md.double_sided != 0 ? _double : _single, 0, 3, mpb);
                    }
                    cmd.SetRenderTarget(rt, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
                }
                var p = new MaterialPropertyBlock();
                p.SetTexture("_MainTex", _textures[d.texture]);
                p.SetVector("_Multiply", new Vector4(d.multiply_r, d.multiply_g, d.multiply_b, 1));
                p.SetVector("_Screen", new Vector4(d.screen_r, d.screen_g, d.screen_b, 0));
                p.SetFloat("_Opacity", d.opacity);
                p.SetFloat("_UseMask", masked ? 1 : 0);
                p.SetFloat("_Invert", d.mask_inverted != 0 ? 1 : 0);
                cmd.DrawMesh(_meshes[d.mesh], Matrix4x4.identity, d.double_sided != 0 ? _double : _single, 0, d.blend_mode, p);
            }
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Release();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            read.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            RenderTexture.active = prev;
            var raw = read.GetRawTextureData<byte>().ToArray();
            UnityEngine.Object.DestroyImmediate(read);
            RenderTexture.ReleaseTemporary(rt);
            RenderTexture.ReleaseTemporary(maskRt);

            // ReadPixels row 0 is the bottom of the view; rendering.md puts y_max in the top row.
            var flipped = new byte[raw.Length];
            int stride = width * 4;
            for (int y = 0; y < height; y++)
                Buffer.BlockCopy(raw, (height - 1 - y) * stride, flipped, y * stride, stride);
            return flipped;
        }

        uint FindPosition(uint mesh)
        {
            foreach (var kv in _drawables) if (kv.Value.mesh == mesh) return kv.Key;
            throw new TsukumoeException(-1, $"mask mesh {mesh} is not in the draw list");
        }

        public void Dispose()
        {
            foreach (var m in _meshes.Values) UnityEngine.Object.DestroyImmediate(m);
            foreach (var t in _textures) UnityEngine.Object.DestroyImmediate(t);
            UnityEngine.Object.DestroyImmediate(_single);
            UnityEngine.Object.DestroyImmediate(_double);
        }
    }
}
