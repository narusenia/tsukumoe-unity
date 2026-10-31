// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using System;
using System.Runtime.InteropServices;

namespace Tsukumoe
{
    /// <summary>One-to-one P/Invoke of docs/specifications/c-abi.md (ABI 1.0).</summary>
    internal static unsafe class Native
    {
        const string Lib = "tsukumoe_ffi";
        public const uint AbiMajor = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct ParameterInfo
        {
            public IntPtr name;
            public float min, max, default_value;
            public byte repeat;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DrawBuffers
        {
            public uint* order;
            public uint drawable_count;
            public byte order_changed;
            public float* positions;
            public float* uvs;
            public uint vertex_count;
            public uint* indices;
            public uint index_count;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Drawable
        {
            public uint mesh;
            public IntPtr name;
            public uint texture;
            public int blend_mode;
            public byte double_sided;
            public float* positions;
            public float* uvs;
            public uint vertex_count;
            public uint* indices;
            public uint index_count;
            public uint vertex_start;
            public uint index_start;
            public uint* masks;
            public uint mask_count;
            public byte mask_inverted;
            public float opacity;
            public float draw_order;
            public float multiply_r, multiply_g, multiply_b;
            public float screen_r, screen_g, screen_b;
            public byte geometry_changed;
            public byte style_changed;
        }

        [DllImport(Lib)] public static extern uint tsukumoe_abi_version();
        [DllImport(Lib)] public static extern IntPtr tsukumoe_last_error_message();
        [DllImport(Lib)] public static extern int tsukumoe_runtime_load(byte* data, UIntPtr len, out IntPtr runtime);
        [DllImport(Lib)] public static extern int tsukumoe_runtime_destroy(IntPtr runtime);
        [DllImport(Lib)] public static extern int tsukumoe_runtime_parameter_count(IntPtr runtime, out uint count);
        [DllImport(Lib)] public static extern int tsukumoe_runtime_parameter_info(IntPtr runtime, uint index, out ParameterInfo info);
        [DllImport(Lib)] public static extern int tsukumoe_runtime_parameter_index(IntPtr runtime, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, out uint index);
        [DllImport(Lib)] public static extern int tsukumoe_runtime_set_parameter(IntPtr runtime, uint index, float value);
        [DllImport(Lib)] public static extern int tsukumoe_runtime_update(IntPtr runtime, float dt);
        [DllImport(Lib)] public static extern int tsukumoe_runtime_draw_buffers(IntPtr runtime, out DrawBuffers buffers);
        [DllImport(Lib)] public static extern int tsukumoe_runtime_drawable(IntPtr runtime, uint position, out Drawable drawable);
        [DllImport(Lib)] public static extern int tsukumoe_runtime_texture_count(IntPtr runtime, out uint count);
        [DllImport(Lib)] public static extern int tsukumoe_runtime_texture(IntPtr runtime, uint index, out byte* data, out uint len);

        public static void Check(int status)
        {
            if (status != 0)
                throw new TsukumoeException(status, Marshal.PtrToStringUTF8(tsukumoe_last_error_message()));
        }
    }

    public sealed class TsukumoeException : Exception
    {
        public int Status { get; }
        public TsukumoeException(int status, string message) : base($"tsukumoe status {status}: {message}") => Status = status;
    }
}
