// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using System;
using System.Runtime.InteropServices;

namespace Tsukumoe
{
    /// <summary>Priority of a motion on a layer (c-abi.md, TsukumoePriority).</summary>
    public enum MotionPriority { Idle = 0, Normal = 1, Force = 2 }

    /// <summary>Automatic motion. Names are resolved when <see cref="TsukumoeModel.SetAuto"/> is called.</summary>
    public struct AutoMotion
    {
        public bool Blink;
        public string BlinkLeftEye, BlinkRightEye;
        public ulong BlinkSeed;
        public float BlinkInterval, BlinkCloseTime, BlinkClosedTime, BlinkOpenTime;
        public bool Breath;
        public string BreathParameter;
        public float BreathCycle, BreathAmplitude, BreathOffset;

        /// <summary>Blinks about every 4 seconds, closing in 0.1 s and opening in 0.2 s.</summary>
        public static AutoMotion WithBlink(string leftEye, string rightEye) => new AutoMotion
        {
            Blink = true, BlinkLeftEye = leftEye, BlinkRightEye = rightEye, BlinkSeed = 1,
            BlinkInterval = 4f, BlinkCloseTime = 0.1f, BlinkClosedTime = 0.05f, BlinkOpenTime = 0.2f,
        };
    }

    /// <summary>A loaded .tmod: parameters, motions, expressions, time and the draw list of the last update.</summary>
    public sealed unsafe class TsukumoeModel : IDisposable
    {
        IntPtr _runtime;

        /// <summary>Number of updates so far; the renderer uses it to notice a skipped update.</summary>
        public int UpdateCount { get; private set; }

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

        public int ParameterCount
        {
            get { Native.Check(Native.tsukumoe_runtime_parameter_count(_runtime, out uint n)); return (int)n; }
        }

        public string ParameterName(int index)
        {
            Native.Check(Native.tsukumoe_runtime_parameter_info(_runtime, (uint)index, out var info));
            return Marshal.PtrToStringUTF8(info.name);
        }

        public (float Min, float Max, float Default) ParameterRange(int index)
        {
            Native.Check(Native.tsukumoe_runtime_parameter_info(_runtime, (uint)index, out var info));
            return (info.min, info.max, info.default_value);
        }

        public int ParameterIndex(string name)
        {
            Native.Check(Native.tsukumoe_runtime_parameter_index(_runtime, name, out uint i));
            return (int)i;
        }

        /// <summary>Holds the parameter at <paramref name="value"/> from the next update on.</summary>
        public void SetParameter(int index, float value) =>
            Native.Check(Native.tsukumoe_runtime_set_parameter(_runtime, (uint)index, value));

        /// <summary>Gives the parameter back to motions, expressions and automatic motion.</summary>
        public void ClearParameter(int index) =>
            Native.Check(Native.tsukumoe_runtime_clear_parameter(_runtime, (uint)index));

        public float ParameterValue(int index)
        {
            Native.Check(Native.tsukumoe_runtime_parameter_value(_runtime, (uint)index, out float v));
            return v;
        }

        public int MotionCount
        {
            get { Native.Check(Native.tsukumoe_runtime_motion_count(_runtime, out uint n)); return (int)n; }
        }

        public string MotionName(int index)
        {
            Native.Check(Native.tsukumoe_runtime_motion_name(_runtime, (uint)index, out var name));
            return Marshal.PtrToStringUTF8(name);
        }

        public int ExpressionCount
        {
            get { Native.Check(Native.tsukumoe_runtime_expression_count(_runtime, out uint n)); return (int)n; }
        }

        public string ExpressionName(int index)
        {
            Native.Check(Native.tsukumoe_runtime_expression_name(_runtime, (uint)index, out var name));
            return Marshal.PtrToStringUTF8(name);
        }

        /// <returns>False when a motion of a higher priority is playing and keeps playing.</returns>
        public bool PlayMotion(string name, int layer = 0, MotionPriority priority = MotionPriority.Normal)
        {
            Native.Check(Native.tsukumoe_runtime_play_motion(_runtime, name, (uint)layer, (int)priority, out uint started));
            return started != 0;
        }

        public void StopMotion(int layer = 0) =>
            Native.Check(Native.tsukumoe_runtime_stop_motion(_runtime, (uint)layer));

        public bool IsMotionPlaying(int layer = 0)
        {
            Native.Check(Native.tsukumoe_runtime_is_motion_playing(_runtime, (uint)layer, out uint p));
            return p != 0;
        }

        public void PlayExpression(string name) =>
            Native.Check(Native.tsukumoe_runtime_play_expression(_runtime, name));

        public void StopExpression(string name) =>
            Native.Check(Native.tsukumoe_runtime_stop_expression(_runtime, name));

        public void SetAuto(AutoMotion auto)
        {
            var c = new Native.AutoConfig
            {
                size = (uint)sizeof(Native.AutoConfig),
                flags = (auto.Blink ? 1u : 0u) | (auto.Breath ? 2u : 0u),
                blink_seed = auto.BlinkSeed,
                blink_interval = auto.BlinkInterval,
                blink_close_time = auto.BlinkCloseTime,
                blink_closed_time = auto.BlinkClosedTime,
                blink_open_time = auto.BlinkOpenTime,
                breath_cycle = auto.BreathCycle,
                breath_amplitude = auto.BreathAmplitude,
                breath_offset = auto.BreathOffset,
            };
            try
            {
                if (auto.Blink)
                {
                    c.blink_left_eye = Marshal.StringToCoTaskMemUTF8(auto.BlinkLeftEye);
                    c.blink_right_eye = Marshal.StringToCoTaskMemUTF8(auto.BlinkRightEye);
                }
                if (auto.Breath) c.breath_parameter = Marshal.StringToCoTaskMemUTF8(auto.BreathParameter);
                Native.Check(Native.tsukumoe_runtime_set_auto(_runtime, ref c));
            }
            finally
            {
                Marshal.FreeCoTaskMem(c.blink_left_eye);
                Marshal.FreeCoTaskMem(c.blink_right_eye);
                Marshal.FreeCoTaskMem(c.breath_parameter);
            }
        }

        public void Update(float dt)
        {
            Native.Check(Native.tsukumoe_runtime_update(_runtime, dt));
            UpdateCount++;
        }

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
