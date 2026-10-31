// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using System.IO;
using UnityEditor.AssetImporters;

namespace Tsukumoe.Editor
{
    [ScriptedImporter(1, "tmod")]
    public sealed class TmodImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var asset = TsukumoeModelAsset.FromBytes(File.ReadAllBytes(ctx.assetPath));
            ctx.AddObjectToAsset("model", asset);
            ctx.SetMainObject(asset);
        }
    }
}
