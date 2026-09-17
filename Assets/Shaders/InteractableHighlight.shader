// インタラクト可能な対象のヒント表現。docs/ARCHITECTURE.md §4.2。
//
//   Rim     … L2 近接ヒント。輪郭だけが淡く光る（加算）。強さは距離で InteractableHighlight が決める
//   Outline … L3 照準ヒント。ホバー中の縁取り（背面を法線方向に押し出す inverted hull）
//
// 対象のマテリアルは差し替えない。InteractableHighlight が各 Renderer の末尾に
// このマテリアルを 1 枚足し、元の描画の上に重ねる。
Shader "Portfolio/InteractableHighlight"
{
    Properties
    {
        _RimColor ("Rim Color", Color) = (1, 0.86, 0.62, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineStrength ("Outline Strength", Range(0, 1)) = 0
        _OutlineWidth ("Outline Width (px)", Range(0, 8)) = 2.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half4 _OutlineColor;
            half _OutlineStrength;
            half _OutlineWidth;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };
        ENDHLSL

        Pass
        {
            Name "Rim"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half facing = saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                half rim = pow(1 - facing, _RimPower) * _RimStrength;
                return half4(_RimColor.rgb * rim, 0);
            }
            ENDHLSL
        }

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
