Shader "TideSong/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.22,0.50,0.92,1)
        _HorizonColor ("Horizon", Color) = (0.86,0.91,0.98,1)
        _GroundColor ("Ground", Color) = (0.55,0.62,0.70,1)
        _SunColor ("Sun Color", Color) = (1,0.92,0.75,1)
        _SunDir ("Sun Dir", Vector) = (0.4,0.7,0.3,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            fixed4 _TopColor, _HorizonColor, _GroundColor, _SunColor, _SunDir;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float t = saturate(d.y);
                float3 col = lerp(_HorizonColor.rgb, _TopColor.rgb, pow(t, 0.65));
                float below = saturate(-d.y * 3.0);
                col = lerp(col, _GroundColor.rgb, below);
                float sun = saturate(dot(d, normalize(_SunDir.xyz)));
                col += _SunColor.rgb * pow(sun, 260.0) * 6.0;
                col += _SunColor.rgb * pow(sun, 9.0) * 0.12;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
