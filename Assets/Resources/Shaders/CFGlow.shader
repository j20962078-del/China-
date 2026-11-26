// China Fishing: материал со свечением (окна домов, фонари). Не требует ключевых слов Standard — работает в любой сборке.
// _GlowAmount ставит Atmosphere: 0 днём, 1 ночью. _Rand > 0: часть окон (по хэшу положения) остаётся тёмной.
Shader "ChinaFishing/Glow"
{
    Properties
    {
        _MainTex ("Текстура", 2D) = "white" {}
        _Color ("Цвет", Color) = (1,1,1,1)
        _GlowColor ("Цвет свечения", Color) = (1,0.82,0.5,1)
        _GlowAmount ("Свечение 0..1", Range(0,1)) = 0
        _Rand ("Доля тёмных окон", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Lambert addshadow nolightmap
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color, _GlowColor;
        float _GlowAmount, _Rand;
        struct Input { float2 uv_MainTex; float3 worldPos; };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            float2 cell = floor(IN.worldPos.xz * 0.45 + IN.worldPos.y * 0.7);
            float h = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
            float on = h >= _Rand ? 1.0 : 0.0;
            o.Emission = _GlowColor.rgb * (_GlowAmount * on * 1.4);
        }
        ENDCG
    }
    Fallback "Legacy Shaders/Diffuse"
}
