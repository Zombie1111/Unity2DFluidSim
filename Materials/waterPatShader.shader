//Unity2DFluidSim by David Westberg https://github.com/Zombie1111/Unity2DFluidSim
Shader "Hidden/WaterPatShader"
{
    Properties
    {
        _MainTex ("Albedo (RGBA)", 2D) = "white" {}
        _ColorMultiplier ("Color Multiplier", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha //Alpha blending
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            sampler2D _MainTex;
            float4 _ColorMultiplier;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR0;
                float2 uv : TEXCOORD0;
            };

            StructuredBuffer<float2> _waterPatPoss;

            v2f vert(appdata_base v, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                v2f o;
                uint instanceID = GetIndirectInstanceID(svInstanceID);

                o.pos = mul(UNITY_MATRIX_VP, v.vertex + float4(_waterPatPoss[svInstanceID], 0, 0));
                o.uv = v.texcoord.xy;

                //()) / X))    X is noise frequency
                float noise = (frac(instanceID / (float(GetIndirectInstanceCount()) / 5.0))) * 0.2;

                o.color = float4(
                    noise,
                    noise,
                    0.0,
                    0.0);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                //Sample the albedo texture
                return float4(0.0, 0.0, 1.0, tex2D(_MainTex, i.uv).a) + i.color;
            }
            ENDCG
        }
    }
}
