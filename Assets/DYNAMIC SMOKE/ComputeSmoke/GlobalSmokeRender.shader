Shader "Custom/GlobalSmokeRender"
{
    Properties
    {
        _MasterOpacity ("Master Opacity", Range(0, 1)) = 1.0
        _DensityMultiplier ("Density Multiplier", Range(0.001, 1)) = 1
        
        [Header(Stylization)]
        [Toggle] _UsePosterize ("Enable Posterization", Float) = 0
        _PosterizeSteps ("Posterization Steps", Range(2, 64)) = 8
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        
        Blend SrcAlpha OneMinusSrcAlpha 
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0; 
            };

            TEXTURE2D(_GlobalSmokeDyeTex);
            SAMPLER(sampler_GlobalSmokeDyeTex);
            float2 _GlobalSmokeBottomLeft;
            float2 _GlobalSmokeSize;
            
            float _MasterOpacity;
            float _DensityMultiplier;
            
            float _UsePosterize;
            float _PosterizeSteps;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = (input.positionWS.xy - _GlobalSmokeBottomLeft) / _GlobalSmokeSize;

                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return half4(0, 0, 0, 0);

                half4 smokeData = SAMPLE_TEXTURE2D(_GlobalSmokeDyeTex, sampler_GlobalSmokeDyeTex, uv);                
                half3 pureColor = smokeData.rgb;
                half opacity = saturate(smokeData.a * _DensityMultiplier) * _MasterOpacity;

                //if you want one SOLID smoke color, use this instead (need to change compute shader as well)
                // half4 smokeData = SAMPLE_TEXTURE2D(_GlobalSmokeDyeTex, sampler_GlobalSmokeDyeTex, uv);
                // half3 pureColor = smokeData.rgb / max(smokeData.a, 0.0001);
                // half opacity = saturate(smokeData.a * _DensityMultiplier) * _MasterOpacity;

                //POSTERIZATION
                if (_UsePosterize > 0.5)
                {
                    pureColor = floor(pureColor * _PosterizeSteps) / _PosterizeSteps;
                    opacity = floor(opacity * _PosterizeSteps) / _PosterizeSteps;
                }

                return half4(pureColor, opacity);
            }
            ENDHLSL
        }
    }
}