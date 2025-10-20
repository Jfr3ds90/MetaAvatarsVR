Shader "Custom/UVRevealShaderSimple"
{
    Properties
    {
        _BaseMap("Base Texture", 2D) = "white" {}
        _RevealMap("Reveal Texture (UV)", 2D) = "white" {}
        _RevealColor("Reveal Tint", Color) = (1,0,1,1)
        _RevealPower("Reveal Falloff", Range(0.5, 4)) = 2.0
        _BaseOpacity("Base Opacity", Range(0, 1)) = 0.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RevealMap);
            SAMPLER(sampler_RevealMap);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _RevealMap_ST;
                half4 _RevealColor;
                half _RevealPower;
                half _BaseOpacity;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 revealColor = SAMPLE_TEXTURE2D(_RevealMap, sampler_RevealMap, input.uv) * _RevealColor;
                
                half maxReveal = _BaseOpacity;
                
                #ifdef _LIGHT_LAYERS
                    uint meshLayers = GetMeshRenderingLayer();
                #endif
                
                // Main light
                Light mainLight = GetMainLight();
                #ifdef _LIGHT_LAYERS
                if (IsMatchingLightLayer(mainLight.layerMask, meshLayers))
                #endif
                {
                    half reveal = pow(mainLight.distanceAttenuation, _RevealPower);
                    maxReveal = max(maxReveal, reveal);
                }
                
                // Additional lights
                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = min(GetAdditionalLightsCount(), 4u);
                for (uint i = 0u; i < lightCount; ++i)
                {
                    Light light = GetAdditionalLight(i, input.positionWS);
                    #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(light.layerMask, meshLayers))
                    #endif
                    {
                        half reveal = pow(light.distanceAttenuation, _RevealPower);
                        maxReveal = max(maxReveal, reveal);
                    }
                }
                #endif
                
                half4 finalColor = lerp(baseColor, revealColor, maxReveal);
                finalColor.a = lerp(baseColor.a * _BaseOpacity, revealColor.a, maxReveal);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}