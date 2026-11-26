Shader "TideSong/TerrainVC"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        // 探索地基（2026-11-26）：背光面漫反射下限。环形山内侧背光时 Lambert
        // diffuse≈0 + 场景环境光很暗（sky 0.21）→ 纯黑剪影；下限只抬背光面，
        // 不影响受光面。阴影距离 90 m 外本就无阴影，远山黑纯属背光。
        _MinDiffuse ("Min Diffuse (backlit floor)", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf AstralLambert vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Tint;
        half _MinDiffuse;

        struct Input
        {
            float4 vertColor;
            float3 worldPos;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vertColor = v.color;
        }

        // Lambert + 背光下限：背光面不低于 _MinDiffuse，受光面与原来一致。
        half4 LightingAstralLambert(SurfaceOutput s, half3 lightDir, half atten)
        {
            half diff = max(0.0, dot(s.Normal, lightDir));
            diff = max(diff, _MinDiffuse);
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * (diff * atten);
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 c = IN.vertColor.rgb;
            // 高频细节，避免大片纯色
            float d = frac(sin(dot(floor(IN.worldPos.xz * 3.0), float2(12.9898, 78.233))) * 43758.5453);
            c *= 0.95 + d * 0.10;
            o.Albedo = c * _Tint.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
