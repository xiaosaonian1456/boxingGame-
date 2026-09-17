//B站“乐观的小强”制作
//https://space.bilibili.com/100714280?spm_id_from=333.1007.0.0
Shader "MixedReality/AlphaPass" {
    Properties{
        _MainTex("Base (RGB)", 2D) = "white" {}
        _Threshold("Alpha Threshold", Range(0,1)) = 0.05
    }

    SubShader{
        Cull Off ZWrite Off ZTest Always

        Pass{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _Threshold;

            v2f vert(appdata_img v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                fixed4 col = tex2D(_MainTex, i.uv);

                float luminance = dot(col.rgb, float3(0.299, 0.587, 0.114));
                float newAlpha = smoothstep(_Threshold, _Threshold + 0.1, luminance);
                col.a = max(col.a, newAlpha);
                
                return col;
            }
            ENDCG
        }
    }
}