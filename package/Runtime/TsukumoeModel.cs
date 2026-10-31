// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using System;

namespace Tsukumoe
{
    /// <summary>A loaded .tmod: parameters, time and the draw list of the last update.</summary>
    public sealed unsafe class TsukumoeModel : IDisposable
    {
        IntPtr _runtime;

        public TsukumoeModel(byte[] tmod)
        {
            uint abi = Native.tsukumoe_abi_version();
            if (abi >> 16 != Native.AbiMajor)
                throw new TsukumoeException(-1, $"native plugin ABI major {abi >> 16} differs from {Native.AbiMajor}");
            fixed (byte* p = tmod)
                Native.Check(Native.tsukumoe_runtime_load(p, (UIntPtr)tmod.Length, out _runtime));
            // The buffers hold zeros until the first update.
            Update(0f);
        }

        public int ParameterIndex(string name)
        {
            Native.Check(Native.tsukumoe_runtime_parameter_index(_runtime, name, out uint i));
            return (int)i;
        }

        public void SetParameter(int index, float value) =>
            Native.Check(Native.tsukumoe_runtime_set_parameter(_runtime, (uint)index, value));

        public void Update(float dt) => Native.Check(Native.tsukumoe_runtime_update(_runtime, dt));

        internal Native.DrawBuffers DrawBuffers()
        {
            Native.Check(Native.tsukumoe_runtime_draw_buffers(_runtime, out var b));
            return b;
        }

        internal Native.Drawable Drawable(uint position)
        {
            Native.Check(Native.tsukumoe_runtime_drawable(_runtime, position, out var d));
            return d;
        }

        public int TextureCount
        {
            get { Native.Check(Native.tsukumoe_runtime_texture_count(_runtime, out uint n)); return (int)n; }
        }

        public byte[] TexturePng(int index)
        {
            Native.Check(Native.tsukumoe_runtime_texture(_runtime, (uint)index, out byte* data, out uint len));
            return new ReadOnlySpan<byte>(data, (int)len).ToArray();
        }

        public void Dispose()
        {
            if (_runtime == IntPtr.Zero) return;
            Native.tsukumoe_runtime_destroy(_runtime);
            _runtime = IntPtr.Zero;
        }
    }
}
