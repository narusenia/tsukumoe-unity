// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

// Shows the premultiplied texture of a TsukumoeRenderer. Declares the built-in transforms itself so
// it needs no render pipeline include; the passes have no LightMode, so URP draws them as unlit.
Shader "Tsukumoe/Display"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            float4x4 unity_ObjectToWorld;
            float4x4 unity_MatrixVP;
            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            struct Attr { float3 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V2F { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V2F Vert(Attr a)
            {
                V2F v;
                v.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, float4(a.pos, 1)));
                v.uv = a.uv;
                return v;
            }
            float4 Frag(V2F i) : SV_Target { return _MainTex.Sample(sampler_MainTex, i.uv); }
            ENDHLSL
        }
    }
}
