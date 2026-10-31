// Copyright 2026 Tsukumoe Contributors
// SPDX-License-Identifier: Apache-2.0 OR MIT

// Implements docs/specifications/rendering.md on stored (non-decoded) values.
// Passes: 0 normal, 1 additive, 2 multiply, 3 mask write (max of the texture alpha).
Shader "Tsukumoe/Drawable"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #pragma target 3.5
        #pragma vertex Vert
        float4x4 _TsukumoeVP;
        Texture2D _MainTex;
        SamplerState sampler_MainTex;
        Texture2D _TsukumoeMask;
        float4 _Multiply;
        float4 _Screen;
        float _Opacity;
        float _UseMask;
        float _Invert;

        struct Attr { float2 pos : POSITION; float2 uv : TEXCOORD0; };
        struct V2F { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

        V2F Vert(Attr a)
        {
            V2F v;
            v.pos = mul(_TsukumoeVP, float4(a.pos, 0, 1));
            v.uv = float2(a.uv.x, 1 - a.uv.y); // UV (0,0) is the top-left of the image
            return v;
        }

        float4 Shade(V2F i)
        {
            float4 t = _MainTex.Sample(sampler_MainTex, i.uv); // premultiplied
            float3 c = t.rgb * _Multiply.rgb;
            c = c + _Screen.rgb * t.a - c * _Screen.rgb;
            float k = 1;
            if (_UseMask > 0.5)
            {
                float m = _TsukumoeMask.Load(int3(i.pos.xy, 0)).r;
                k = _Invert > 0.5 ? 1 - m : m;
            }
            return float4(c, t.a) * _Opacity * k;
        }
        ENDHLSL

        Pass
        {
            Name "Normal"
            Blend One OneMinusSrcAlpha
            Cull [_Cull]
            HLSLPROGRAM
            #pragma fragment Frag
            float4 Frag(V2F i) : SV_Target { return Shade(i); }
            ENDHLSL
        }
        Pass
        {
            Name "Additive"
            Blend One One, Zero One
            Cull [_Cull]
            HLSLPROGRAM
            #pragma fragment Frag
            float4 Frag(V2F i) : SV_Target { return Shade(i); }
            ENDHLSL
        }
        Pass
        {
            Name "Multiply"
            Blend DstColor OneMinusSrcAlpha, Zero One
            Cull [_Cull]
            HLSLPROGRAM
            #pragma fragment Frag
            float4 Frag(V2F i) : SV_Target { return Shade(i); }
            ENDHLSL
        }
        Pass
        {
            Name "Mask"
            BlendOp Max
            Blend One One
            Cull [_Cull]
            HLSLPROGRAM
            #pragma fragment Frag
            float4 Frag(V2F i) : SV_Target
            {
                float a = _MainTex.Sample(sampler_MainTex, i.uv).a;
                return float4(a, a, a, a);
            }
            ENDHLSL
        }
    }
}
