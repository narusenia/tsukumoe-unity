// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukumoe
{
    /// <summary>
    /// Draws a model into a non-sRGB render texture following rendering.md. The texture holds
    /// stored values, so the result does not depend on the project's color space or render pipeline.
    /// Call <see cref="Upload"/> after each <see cref="TsukumoeModel.Update"/>, then <see cref="Render"/>.
    /// </summary>
    public sealed unsafe class OffscreenRenderer : IDisposable
    {
        // The GPU projection flips Y when rendering into a texture, so rendering.md's counter-clockwise
        // front faces are Unity's back faces: "Cull Front" culls the faces rendering.md culls.
        const float CullBack = 1;
        const float CullOff = 0;

        public static RenderTextureDescriptor Descriptor(int width, int height) =>
            new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0) { sRGB = false };

        readonly TsukumoeModel _model;
        readonly Material _single, _double;
        readonly Texture2D[] _textures;
        readonly Dictionary<uint, Mesh> _meshes = new Dictionary<uint, Mesh>();
        readonly Dictionary<uint, uint> _positionOfMesh = new Dictionary<uint, uint>();
        Native.Drawable[] _drawables = Array.Empty<Native.Drawable>();
        int _uploadedAt = int.MinValue;
        RenderTexture _mask;

        public OffscreenRenderer(TsukumoeModel model, Shader shader)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
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

        static NativeArray<T> View<T>(void* p, int n) where T : struct
        {
            var a = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(p, n, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref a, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
            return a;
        }

        /// <summary>Copies the draw list of the last update into meshes; unchanged geometry is not copied.</summary>
        public void Upload()
        {
            // An update the renderer did not see leaves the change flags useless.
            bool all = _model.UpdateCount - _uploadedAt > 1;
            _uploadedAt = _model.UpdateCount;
            uint count = _model.DrawBuffers().drawable_count;
            if (_drawables.Length != count) _drawables = new Native.Drawable[count];
            _positionOfMesh.Clear();
            const MeshUpdateFlags Quiet = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
            for (uint pos = 0; pos < count; pos++)
            {
                var d = _model.Drawable(pos);
                _drawables[pos] = d;
                _positionOfMesh[d.mesh] = pos;
                int vc = (int)d.vertex_count, ic = (int)d.index_count;
                bool isNew = !_meshes.TryGetValue(d.mesh, out var mesh);
                if (isNew)
                {
                    mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                    _meshes[d.mesh] = mesh;
                    mesh.SetVertexBufferParams(vc,
                        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 2, 0),
                        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 1));
                    mesh.SetIndexBufferParams(ic, IndexFormat.UInt32);
                    mesh.SetIndexBufferData(View<uint>(d.indices, ic), 0, 0, ic, Quiet);
                    mesh.SetSubMesh(0, new SubMeshDescriptor(0, ic), Quiet);
                }
                if (isNew || all || d.geometry_changed != 0)
                {
                    mesh.SetVertexBufferData(View<Vector2>(d.positions, vc), 0, 0, vc, 0, Quiet);
                    mesh.SetVertexBufferData(View<Vector2>(d.uvs, vc), 0, 0, vc, 1, Quiet);
                }
            }
        }

        /// <summary>Draws the uploaded frame into <paramref name="target"/> (an <see cref="Descriptor"/> texture).</summary>
        public void Render(Vector4 view, RenderTexture target)
        {
            if (_mask == null || _mask.width != target.width || _mask.height != target.height)
            {
                if (_mask != null) _mask.Release();
                _mask = new RenderTexture(Descriptor(target.width, target.height)) { filterMode = FilterMode.Point };
            }
            var proj = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(view.x, view.z, view.y, view.w, -1, 1), true);
            var cmd = new CommandBuffer { name = "Tsukumoe" };
            cmd.SetGlobalMatrix("_TsukumoeVP", proj);
            cmd.SetGlobalTexture("_TsukumoeMask", _mask);
            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(false, true, Color.clear);

            for (int pos = 0; pos < _drawables.Length; pos++)
            {
                var d = _drawables[pos];
                if (d.opacity <= 0f || d.index_count == 0) continue;
                bool masked = d.mask_count > 0;
                if (masked)
                {
                    cmd.SetRenderTarget(_mask);
                    cmd.ClearRenderTarget(false, true, Color.clear);
                    for (uint m = 0; m < d.mask_count; m++)
                    {
                        var md = _drawables[_positionOfMesh[d.masks[m]]];
                        var mpb = new MaterialPropertyBlock();
                        mpb.SetTexture("_MainTex", _textures[md.texture]);
                        cmd.DrawMesh(_meshes[md.mesh], Matrix4x4.identity, md.double_sided != 0 ? _double : _single, 0, 3, mpb);
                    }
                    cmd.SetRenderTarget(target, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
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
        }

        /// <summary>Reads a render texture back as premultiplied RGBA8, top row first (y_max at the top).</summary>
        public static byte[] ReadPixels(RenderTexture rt)
        {
            int width = rt.width, height = rt.height;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            read.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            RenderTexture.active = prev;
            var raw = read.GetRawTextureData<byte>().ToArray();
            UnityEngine.Object.DestroyImmediate(read);
            // ReadPixels row 0 is the bottom of the view.
            var flipped = new byte[raw.Length];
            int stride = width * 4;
            for (int y = 0; y < height; y++)
                Buffer.BlockCopy(raw, (height - 1 - y) * stride, flipped, y * stride, stride);
            return flipped;
        }

        public void Dispose()
        {
            foreach (var m in _meshes.Values) UnityEngine.Object.DestroyImmediate(m);
            foreach (var t in _textures) UnityEngine.Object.DestroyImmediate(t);
            UnityEngine.Object.DestroyImmediate(_single);
            UnityEngine.Object.DestroyImmediate(_double);
            if (_mask != null) { _mask.Release(); UnityEngine.Object.DestroyImmediate(_mask); }
        }
    }
}
