//Unity2DFluidSim by David Westberg https://github.com/Zombie1111/Unity2DFluidSim
Shader"Hidden/WaterRenderingShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        //No culling or depth
        Cull Off
        ZWrite Off
        ZTest Always
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
        
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            sampler2D _MainTex;

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 ogColor = tex2D(_MainTex, i.uv);

                if (ogColor.b < 0.99)
                {
                    if (ogColor.b < 0.9) ogColor.rgb = 0.0;
                    else ogColor.rgb = float3(0.7, 0.7, 1.0);
                }

                return ogColor;
            }

            ENDCG
        }
    }
}
