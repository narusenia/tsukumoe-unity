# Tsukumoe for Unity

Plays Tsukumoe `.tmod` models in Unity. Evaluation (parameters, motions, expressions, automatic motion) runs in the `tsukumoe-ffi` native plugin; drawing uses Unity shaders. No wgpu is involved.

- Unity 6000.3 or later, **URP** (the model is drawn into a texture and shown on a quad, so other pipelines are not supported or tested).
- macOS (arm64) editor and standalone are tested. Windows x64 is planned and untested.
- Output matches the Tsukumoe reference renderer within 2/255 per channel (`rendering.md`), in Linear and Gamma color space.

## Use

1. Put a `.tmod` in `Assets/`. It imports as a `TsukumoeModelAsset`.
2. Add `TsukumoeRenderer` to a GameObject and assign the asset (or see `Samples~/Basic`).
3. Drive it from code through `renderer.Model`:

```csharp
renderer.Model.PlayMotion("wave", layer: 0, MotionPriority.Normal);
renderer.Model.SetParameter(renderer.Model.ParameterIndex("AngleX"), 12f);
renderer.Model.SetAuto(AutoMotion.WithBlink("EyeL", "EyeR"));
```

One drawing-space unit is one world unit at scale 1. The native ABI is documented in Tsukumoe's `docs/specifications/c-abi.md`; `Native.cs` is a one-to-one copy of it, and the plugin's ABI major version is checked when a model loads.

## Native plugin

The shared library is not stored in git. Build it from a Tsukumoe checkout:

```sh
scripts/build-plugin.sh /path/to/tsukumoe   # macOS: copies libtsukumoe_ffi.dylib into package/Runtime/Plugins/macOS
```

## Development

`project/` is a Unity project that references `package/`. Tests: `Unity -batchmode -projectPath project -runTests -testPlatform EditMode` (and `PlayMode`). `TsukumoeCapture` writes pixels and the draw list as JSON for comparing environments.

## License

Apache-2.0 OR MIT.
