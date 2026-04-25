Shader "Hidden/ScreenSpaceOutline"
{
    Properties
    {
        _DarkenFactor ("Darken Factor", Range(0, 1)) = 0.5
        _Thickness ("Thickness", Float) = 1.0
        _DepthThreshold ("Depth Threshold", Float) = 0.1
        _NormalThreshold ("Normal Threshold", Float) = 0.5
        _DistanceFadeStart ("Distance Fade Start", Float) = 10.0
        _DistanceFadeEnd ("Distance Fade End", Float) = 50.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "ScreenSpaceOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _DarkenFactor;
                float _Thickness;
                float _DepthThreshold;
                float _NormalThreshold;
                float _DistanceFadeStart;
                float _DistanceFadeEnd;
            CBUFFER_END

            TEXTURE2D_X(_CameraColorTexture);
            SAMPLER(sampler_CameraColorTexture);



            // Roberts Cross Edge Detection
            void GetDepthAndNormal(float2 uv, out float depth, out float3 normal)
            {
                depth = SampleSceneDepth(uv);
                depth = LinearEyeDepth(depth, _ZBufferParams);
                normal = SampleSceneNormals(uv);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                
                // Get center pixel data
                float centerDepth;
                float3 centerNormal;
                GetDepthAndNormal(uv, centerDepth, centerNormal);

                // Sample base color (use BlitTexture in URP 14+)
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // Ignore background (skybox)
                if(centerDepth >= _ProjectionParams.z * 0.99)
                    return sceneColor;

                // Dynamic thickness: scales inversely with depth
                // Thicker when close (small depth), thinner when far (large depth)
                // We add a max() to avoid division by zero or overly huge offsets
                float dynamicThickness = max(1.0, _Thickness * 10.0 / max(centerDepth, 0.5));
                float2 texelSize = (1.0 / _ScreenParams.xy) * dynamicThickness;

                float2 uv0 = uv + float2(-1, -1) * texelSize;
                float2 uv1 = uv + float2( 1, -1) * texelSize;
                float2 uv2 = uv + float2(-1,  1) * texelSize;
                float2 uv3 = uv + float2( 1,  1) * texelSize;

                float d0, d1, d2, d3;
                float3 n0, n1, n2, n3;

                GetDepthAndNormal(uv0, d0, n0);
                GetDepthAndNormal(uv1, d1, n1);
                GetDepthAndNormal(uv2, d2, n2);
                GetDepthAndNormal(uv3, d3, n3);

                // Depth Edge
                float depthDiff0 = d3 - d0;
                float depthDiff1 = d2 - d1;
                float edgeDepth = sqrt(depthDiff0 * depthDiff0 + depthDiff1 * depthDiff1);

                // Normal Edge
                float3 normalDiff0 = n3 - n0;
                float3 normalDiff1 = n2 - n1;
                float edgeNormal = sqrt(dot(normalDiff0, normalDiff0) + dot(normalDiff1, normalDiff1));

                // Thresholds
                float isEdgeDepth = step(_DepthThreshold, edgeDepth);
                float isEdgeNormal = step(_NormalThreshold, edgeNormal);
                float edge = max(isEdgeDepth, isEdgeNormal);

                if (edge > 0.0)
                {
                    half4 outlineColor = sceneColor * _DarkenFactor;
                    float fade = saturate((centerDepth - _DistanceFadeStart) / max(0.01, _DistanceFadeEnd - _DistanceFadeStart));
                    outlineColor = lerp(outlineColor, half4(0, 0, 0, 1), fade);
                    return lerp(sceneColor, outlineColor, edge);
                }

                return sceneColor;
            }
            ENDHLSL
        }
    }
}
// Force reimport
