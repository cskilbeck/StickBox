  Shader "BlockShader" {
    Properties {
      _MainTex ("Texture", 2D) = "white" {}
      _Color ("Tint", Color) = (1.0, 0.6, 0.6, 0.2)
    }
    SubShader {
      Tags { "RenderType" = "Opaque" }
      CGPROGRAM
      #pragma surface surf Lambert finalcolor:mycolor
      struct Input {
          float2 uv_MainTex;
      };
      fixed4 _Color;
      void mycolor (Input IN, SurfaceOutput o, inout fixed4 color)
      {
          color *= _Color;
      }
      sampler2D _MainTex;
      void surf (Input IN, inout SurfaceOutput o) {
           o.Albedo = tex2D (_MainTex, IN.uv_MainTex).rgb;
      }
      ENDCG
    } 
    Fallback "Diffuse"
  }