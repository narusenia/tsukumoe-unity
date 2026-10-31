// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using UnityEngine;

namespace Tsukumoe
{
    /// <summary>The bytes of a .tmod file, imported by the ScriptedImporter.</summary>
    public sealed class TsukumoeModelAsset : ScriptableObject
    {
        [SerializeField, HideInInspector] byte[] _bytes;
        public byte[] Bytes => _bytes;
        public static TsukumoeModelAsset FromBytes(byte[] bytes)
        {
            var a = CreateInstance<TsukumoeModelAsset>();
            a._bytes = bytes;
            return a;
        }
    }
}
