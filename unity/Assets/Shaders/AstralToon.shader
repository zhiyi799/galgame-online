// AstralToon — ASTRAL BLOOM 手写卡通渲染 shader（Built-in 管线，Gamma）。
// 做法借鉴 Custom-Shader 的 Anime.shader（MIT，yuanqiubai/Custom-Shader）：
// 分阶 ramp 漫反射 + 阈值高光 + 边缘光 + 各向异性头发高光 + 法线外扩描边。
// 本文件是为 Built-in 管线从头写的 CG 代码（原 shader 是 URP HLSL，不能直接用），
// 不复制其源码；体积雾 pass 未移植（原仓库 README 说明其雾逻辑未接完）。
// 许可证：本项目自有代码；思路来源 MIT 许可，改编保留来源说明见上。
Shader "AstralBloom/Toon"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (1, 1, 1, 1)
        _LightColor ("Light Color", Color) = (0.90, 0.93, 1.0, 1)
        _ShadowColor ("Shadow Color", Color) = (0.10, 0.16, 0.32, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001, 0.5)) = 0.05
        _ShadowSteps ("Shadow Steps", Range(1, 5)) = 3
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _Gloss ("Gloss", Range(1, 64)) = 24
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0.6
        _SpecularThreshold ("Specular Threshold", Range(0.01, 1)) = 0.75
        _SpecularSmoothness ("Specular Smoothness", Range(0.01, 0.5)) = 0.08
        _SpecularType ("Specular Type (0 round / 1 star / 2 cross)", Range(0, 2)) = 0
        _RimColor ("Rim Color", Color) = (0.87, 0.69, 0.80, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.5
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.35
        _RimSmoothness ("Rim Smoothness", Range(0.01, 0.5)) = 0.15
        _HairSpecularColor ("Hair Specular Color", Color) = (1, 0.85, 0.65, 1)
        _HairSpecularStrength ("Hair Specular Strength", Range(0, 2)) = 0
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.45
        _OutlineColor ("Outline Color", Color) = (0.05, 0.07, 0.16, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.08)) = 0.018
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        // ---------------- 主渲染 Pass ----------------
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _LightColor;
            fixed4 _ShadowColor;
            float _ShadowThreshold;
            float _ShadowSmoothness;
            float _ShadowSteps;
            fixed4 _SpecularColor;
            float _Gloss;
            float _SpecularStrength;
            float _SpecularThreshold;
            float _SpecularSmoothness;
            float _SpecularType;
            fixed4 _RimColor;
            float _RimStrength;
            float _RimThreshold;
            float _RimSmoothness;
            fixed4 _HairSpecularColor;
            float _HairSpecularStrength;
            float _AmbientStrength;
            float _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldTangent : TEXCOORD2;
                float2 uv : TEXCOORD3;
                LIGHTING_COORDS(4, 5)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 t = mul((float3x3)unity_ObjectToWorld, v.tangent.xyz);
                // 内置几何体若无切线则用 (1,0,0) 兜底，避免头发高光出现 NaN。
                if (dot(t, t) < 1e-6) t = float3(1, 0, 0);
                o.worldTangent = normalize(t);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                clip(tex.a - _Cutoff);

                float3 N = normalize(i.worldNormal);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 H = normalize(L + V);
                float atten = LIGHT_ATTENUATION(i);

                // 分阶漫反射：ndotl 先分级再平滑过阈值，得到硬朗的色块阴影。
                float ndotl = saturate(dot(N, L));
                float steps = max(1.0, _ShadowSteps);
                float toonLight = floor(ndotl * steps) / steps;
                toonLight *= atten;
                toonLight = smoothstep(_ShadowThreshold - _ShadowSmoothness,
                    _ShadowThreshold + _ShadowSmoothness, toonLight);
                float3 albedo = tex.rgb * _Color.rgb;
                float3 diffuse = lerp(_ShadowColor.rgb, _LightColor.rgb, toonLight) * albedo;

                // 阈值高光：圆形 / 星形 / 十字三种形状。
                float ndoth = max(0.0, dot(N, H));
                float specDot = ndoth;
                if (_SpecularType > 0.5)
                {
                    float3 o1 = float3(0.12, 0, 0);
                    float3 o2 = float3(-0.12, 0, 0);
                    float3 o3 = float3(0, 0.12, 0);
                    float3 o4 = float3(0, -0.12, 0);
                    float h1 = max(0.0, dot(N, normalize(H + o1)));
                    float h2 = max(0.0, dot(N, normalize(H + o2)));
                    float h3 = max(0.0, dot(N, normalize(H + o3)));
                    float h4 = max(0.0, dot(N, normalize(H + o4)));
                    specDot = max(max(h1, h2), max(h3, h4));
                }
                float spec = smoothstep(_SpecularThreshold - _SpecularSmoothness,
                    _SpecularThreshold + _SpecularSmoothness, pow(specDot, _Gloss));
                float3 specular = _SpecularColor.rgb * _SpecularStrength * spec * atten;

                // 边缘光。
                float rim = 1.0 - saturate(dot(N, V));
                rim = smoothstep(_RimThreshold - _RimSmoothness,
                    _RimThreshold + _RimSmoothness, rim);
                float3 rimLight = _RimColor.rgb * _RimStrength * rim;

                // 头发各向异性高光（切线方向两条亮带；非头发材质强度为 0 即关闭）。
                float3 hairSpec = float3(0, 0, 0);
                if (_HairSpecularStrength > 0.001)
                {
                    float3 T = normalize(i.worldTangent);
                    float tdotH = dot(T, H);
                    float s1 = pow(1.0 - tdotH * tdotH, _Gloss * 0.5);
                    float s2 = pow(max(0.0, 1.0 - tdotH * tdotH - 0.25), _Gloss * 0.3);
                    hairSpec = _HairSpecularColor.rgb * _HairSpecularStrength * max(s1, s2 * 0.5) * atten;
                }

                // 环境光：Unity 环境光 + 天空方向渐变（上亮下暗）。
                float sky = saturate(dot(N, float3(0, 1, 0)) * 0.5 + 0.5);
                float3 skyTint = lerp(float3(0.10, 0.13, 0.25), float3(0.35, 0.42, 0.60), sky);
                float3 ambient = (UNITY_LIGHTMODEL_AMBIENT.rgb + skyTint) * _AmbientStrength * albedo;

                float3 col = ambient + diffuse + specular + rimLight + hairSpec;
                return fixed4(saturate(col), tex.a);
            }
            ENDCG
        }

        // ---------------- 描边 Pass：前向裁剪 + 法线外扩 ----------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "Always" }
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float4 clipPos = UnityWorldToClipPos(worldPos);
                // 按裁剪空间 w 缩放，保持屏幕上的描边宽度基本一致。
                float3 offsetPos = worldPos + worldNormal * _OutlineWidth * clipPos.w;
                o.pos = UnityWorldToClipPos(offsetPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                clip(tex.a - _Cutoff);
                return _OutlineColor;
            }
            ENDCG
        }

        // ---------------- 阴影投射 Pass ----------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Cutoff;

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                clip(tex.a - _Cutoff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
