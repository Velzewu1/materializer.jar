Shader "Hidden/Fullscreen/CRT"
{
    Properties
    {
        _Tint            ("Green Tint", Color) = (0.6,1,0.6,1)
        _Intensity       ("Tint Intensity", Range(0,2)) = 0.6
        _Barrel          ("Barrel Distortion", Range(0,0.6)) = 0.12
        _Aberration      ("Chromatic Aberration", Range(0,2)) = 0.25
        _Vignette        ("Vignette", Range(0,2)) = 0.8
        _ScanlineScale   ("Scanline Density", Range(100,1200)) = 520
        _ScanlineAmount  ("Scanline Amount", Range(0,1)) = 0.45
        _NoiseAmount     ("Noise Amount", Range(0,0.3)) = 0.03
        _GrainSpeed      ("Noise Speed", Range(0,10)) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend One Zero

        Pass
        {
            Name "CRT_Fullscreen"
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target   3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Основной источник, который подаёт FullScreen Pass
            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            // Резервный источник (URP camera/opaque buffer)
            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            // (Не используется напрямую, но можно оставить как запасной)
            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _Tint;
            float  _Intensity, _Barrel, _Aberration, _Vignette;
            float  _ScanlineScale, _ScanlineAmount;
            float  _NoiseAmount, _GrainSpeed;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings  { float4 posCS : SV_Position; float2 uv : TEXCOORD0; };

            Varyings Vert (Attributes v)
            {
                Varyings o;
                o.posCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv    = GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }

            // Чтение с фолбэком: сначала _BlitTexture, если пусто — _CameraOpaqueTexture
            float4 ReadSrc(float2 uv)
            {
                float4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);
                // Если буфер не пришёл (часто нули) — используем opaque буфер
                if (c.r + c.g + c.b <= 1e-6)
                {
                    c = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv);
                }
                return c;
            }

            float2 barrel(float2 uv, float k)
            {
                float2 c = uv * 2.0 - 1.0;
                float r2 = dot(c, c);
                c *= 1.0 + k * r2;
                return (c * 0.5 + 0.5);
            }

            float vignette(float2 uv, float p)
            {
                float2 d = uv - 0.5;
                return 1.0 - pow(saturate(dot(d,d) * 2.0), p);
            }

            float3 sampleAberr(float2 uv, float k)
            {
                float2 d   = (uv - 0.5);
                float  r   = length(d);
                float2 off = d * k * r;

                float rCh = ReadSrc(uv + off).r;
                float gCh = ReadSrc(uv     ).g;
                float bCh = ReadSrc(uv - off).b;

                return float3(rCh, gCh, bCh);
            }

            float rand(float2 p) { return frac(sin(dot(p,float2(12.9898,78.233))) * 43758.5453); }

            float3 addScanlines(float3 col, float2 uv, float density, float amount)
            {
                float s = sin(uv.y * density) * 0.5 + 0.5;
                return lerp(col, col * (0.65 + 0.35*s), amount);
            }

            float3 addTint(float3 col, float3 tint, float k)
            {
                return lerp(col, col * tint, k);
            }

            float4 Frag (Varyings i) : SV_Target
            {
                // Искажение «бочкой»
                float2 uv = barrel(i.uv, _Barrel);
                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                    return float4(0,0,0,1);

                // Картинка с хром.аберрацией
                float3 col = sampleAberr(uv, _Aberration);

                // Зерно
                float n = (rand(uv * (_Time.y * _GrainSpeed)) - 0.5) * 2.0;
                col += n * _NoiseAmount;

                // Скан-линии, тинт, виньетка
                col = addScanlines(col, uv, _ScanlineScale, _ScanlineAmount);
                col = addTint(col, _Tint.rgb, _Intensity);
                col *= vignette(uv, max(0.0001, _Vignette));

                return float4(saturate(col), 1);
            }
            ENDHLSL
        }
    }
}
