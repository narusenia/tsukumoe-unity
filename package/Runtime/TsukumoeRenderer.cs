// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using UnityEngine;

namespace Tsukumoe
{
    /// <summary>
    /// Plays a model and shows it on a quad. The model is drawn into a texture each frame, then the quad
    /// shows that texture, so it works in any render pipeline. One drawing-space unit is one world unit
    /// at scale 1.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TsukumoeRenderer : MonoBehaviour
    {
        [SerializeField] TsukumoeModelAsset _asset;
        [SerializeField] int _resolution = 1024;
        [SerializeField] float _padding = 0.05f;

        TsukumoeModel _model;
        OffscreenRenderer _renderer;
        RenderTexture _target;
        Material _display;
        MeshRenderer _quad;
        Vector4 _view;

        public TsukumoeModel Model => _model;
        /// <summary>The drawn rectangle x0,y0,x1,y1 in drawing space.</summary>
        public Vector4 View => _view;
        public RenderTexture Texture => _target;

        public TsukumoeModelAsset Asset { get => _asset; set => _asset = value; }

        void OnEnable()
        {
            if (_asset == null) return;
            _model = new TsukumoeModel(_asset.Bytes);
            _renderer = new OffscreenRenderer(_model, TsukumoeShaders.Drawable);
            var b = _renderer.Bounds();
            float padX = (b.z - b.x) * _padding, padY = (b.w - b.y) * _padding;
            _view = new Vector4(b.x - padX, b.y - padY, b.z + padX, b.w + padY);
            float w = _view.z - _view.x, h = _view.w - _view.y;
            int rw = _resolution, rh = Mathf.Max(1, Mathf.RoundToInt(_resolution * h / w));
            _target = new RenderTexture(OffscreenRenderer.Descriptor(rw, rh)) { filterMode = FilterMode.Bilinear, name = "Tsukumoe" };

            _display = new Material(TsukumoeShaders.Display);
            _display.mainTexture = _target;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Tsukumoe Quad";
            go.hideFlags = HideFlags.DontSave;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3((_view.x + _view.z) / 2, (_view.y + _view.w) / 2, 0);
            go.transform.localScale = new Vector3(w, h, 1);
            _quad = go.GetComponent<MeshRenderer>();
            _quad.sharedMaterial = _display;
            Draw();
        }

        void Update()
        {
            if (_model == null) return;
            _model.Update(Time.deltaTime);
            Draw();
        }

        void Draw()
        {
            _renderer.Upload();
            _renderer.Render(_view, _target);
        }

        void OnDisable()
        {
            if (_quad != null) Destroy(_quad.gameObject);
            _renderer?.Dispose();
            _model?.Dispose();
            if (_target != null) { _target.Release(); Destroy(_target); }
            if (_display != null) Destroy(_display);
            _renderer = null; _model = null; _target = null; _display = null;
        }
    }
}
