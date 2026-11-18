Shader "TideSong/TerrainVC"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Tint;

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

        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 c = IN.vertColor.rgb;
            // 高频细节，避免大片纯色
            float d = frac(sin(dot(floor(IN.worldPos.xz * 3.0), float2(12.9898, 78.233))) * 43758.5453);
            c *= 0.95 + d * 0.10;
            o.Albedo = c * _Tint.rgb;
            o.Alpha = 1;
            o.Specular = 0.02;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
