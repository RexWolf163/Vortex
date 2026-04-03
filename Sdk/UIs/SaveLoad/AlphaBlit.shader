Shader "Hidden/AlphaBlit"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _Overlay ("Overlay", 2D) = "white" {}
    }

    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            sampler2D _Overlay;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv);
                fixed4 overCol = tex2D(_Overlay, i.uv);

                // src_over composite — сохраняет результирующую альфу
                fixed3 rgb = overCol.rgb + baseCol.rgb * (1.0 - overCol.a);
                fixed  a   = overCol.a   + baseCol.a   * (1.0 - overCol.a);

                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}