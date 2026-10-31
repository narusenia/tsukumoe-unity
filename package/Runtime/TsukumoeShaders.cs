// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using UnityEngine;

namespace Tsukumoe
{
    /// <summary>The package shaders. They live in Resources so that player builds include them.</summary>
    public static class TsukumoeShaders
    {
        public static Shader Drawable => Resources.Load<Shader>("Drawable");
        public static Shader Display => Resources.Load<Shader>("Display");
    }
}
