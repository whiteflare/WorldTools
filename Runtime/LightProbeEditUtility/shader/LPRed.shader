Shader "Hidden/LightProbeVisualizer/LPRed"
{
    Properties
    {
        _MainTex        ("Main Texture", 2D)    = "white" {}
        _Color          ("Color", Color)        = (1, 0, 0, 1)
        [HDR]
        _EmissionColor  ("Emission", Color)     = (0.5, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent-499" }
        LOD 200

        ZTest Always

        Stencil {
            Ref 128
            Comp NotEqual
        }

        CGPROGRAM

        #pragma surface surf Lambert exclude_path:deferred exclude_path:prepass noshadow novertexlights nolightmap nodynlightmap nodirlightmap nofog nometa noforwardadd nolppv noshadowmask 

        #pragma target 3.0

        struct Input
        {
            float2 uv_MainTex;
        };

        sampler2D   _MainTex;
        float4      _Color;
        float4      _EmissionColor;

        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 main = tex2D(_MainTex, IN.uv_MainTex).rgb;
            o.Albedo = _Color.rgb * main;
            o.Emission = _EmissionColor.rgb * main;
        }

        ENDCG
    }
    FallBack "Diffuse"
}
