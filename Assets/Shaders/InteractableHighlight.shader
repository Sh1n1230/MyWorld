// インタラクト可能な対象の縁取り。docs/ARCHITECTURE.md §4.2。
//
// 背面を法線方向に押し出して描く（inverted hull）。幅は画面のピクセルで指定する。
// 対象のマテリアルは差し替えない。InteractableHighlight が各 Renderer の末尾に
// このマテリアルを 1 枚足し、元の描画の上に重ねる。
Shader "Portfolio/InteractableHighlight"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineStrength ("Outline Strength", Range(0, 1)) = 0
        _OutlineWidth ("Outline Width (px)", Range(0, 8)) = 2.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _OutlineStrength;
                half _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 vert(Attributes input) : SV_POSITION
            {
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // 幅を画面のピクセルで揃えるため、クリップ空間で法線の向きに押し出す。
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, TransformObjectToWorldNormal(input.normalOS));
                float2 dir = normalize(normalCS.xy + 1e-5);
                positionCS.xy += dir * _OutlineWidth * 2 / _ScreenParams.xy * positionCS.w;
                return positionCS;
            }

            half4 frag() : SV_Target
            {
                return half4(_OutlineColor.rgb, _OutlineColor.a * _OutlineStrength);
            }
            ENDHLSL
        }
    }
}
