// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

using Tsukumoe;
using UnityEngine;

// Put this on an empty GameObject of an empty scene and assign sample.tmod. Press Play.
public sealed class BasicSample : MonoBehaviour
{
    [SerializeField] TsukumoeModelAsset _model;

    void Start()
    {
        var cam = Camera.main != null ? Camera.main : new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 0.7f;
        cam.transform.position = new Vector3(0.5f, 0.5f, -10);

        var go = new GameObject("Tsukumoe Model");
        go.SetActive(false);
        var renderer = go.AddComponent<TsukumoeRenderer>();
        renderer.Asset = _model;
        go.SetActive(true);

        renderer.Model.PlayMotion("rise");
        renderer.Model.SetAuto(AutoMotion.WithBlink("EyeL", "EyeR"));
    }
}
