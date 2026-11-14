// 3D 물건 테두리 (뒤집은 껍데기). 같은 메시를 법선 방향으로 조금 부풀려 뒷면만 그리면 실루엣 둘레에 선이 생긴다.
Shader "Specimen07/OutlineHull"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.49, 0.55, 1)
        _Width ("Width (world)", Float) = 0.035
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+20" }
        Pass
        {
            Cull Front
            ZWrite On
            Lighting Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _Width;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; };
            v2f vert (appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 wn = normalize(UnityObjectToWorldNormal(v.normal));
                wp += wn * _Width;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                return o;
            }
            fixed4 frag (v2f i) : SV_Target { return fixed4(_Color.rgb, 1); }
            ENDCG
        }
    }
    Fallback Off
}
