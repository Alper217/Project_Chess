Shader "Hidden/Stencil_Writer"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry-1" }
        Pass
        {
            ZWrite On // Derinlik yazmalı ki karakterin nerede olduğunu bilsin
            ColorMask 0 // Renk basma (Görünmez yap)
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 frag(Varyings input) : SV_Target { return half4(0,0,0,0); }
            ENDHLSL
        }
    }
}