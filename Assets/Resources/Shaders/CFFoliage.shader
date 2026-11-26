// China Fishing: трава, камыши, кувшинки, хвоя. Вырез по альфе (Alpha Cutoff), качание на ветру, туман, GPU Instancing.
// Лежит в Resources, поэтому гарантированно попадает в сборку (в отличие от Standard с ключом _ALPHATEST_ON, созданным в рантайме).
// Вес качания берётся из красного канала вершинного цвета (0 у корня, 1 у верхушки) — его записывает KamMap при загрузке.
Shader "ChinaFishing/Foliage"
{
    Properties
    {
        _MainTex ("Текстура (RGBA)", 2D) = "white" {}
        _Color ("Оттенок", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.4
        _WindAmp ("Сила ветра (м)", Float) = 0.15
        _WindSpeed ("Скорость ветра", Float) = 1.5
        _WindScale ("Размер порывов", Float) = 0.35
        _Light ("Рассеянный свет листвы", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Lambert alphatest:_Cutoff vertex:vert addshadow noforwardadd nolightmap
        #pragma target 3.0
        #pragma multi_compile_instancing

        sampler2D _MainTex;
        fixed4 _Color;
        float _WindAmp, _WindSpeed, _WindScale, _Light;
        float _CFWind;   // глобально: множитель ветра (ставит Atmosphere)
        float _CFWet;    // глобально: 0 сухо … 1 дождь

        struct Input { float2 uv_MainTex; };

        void vert (inout appdata_full v)
        {
            float w = v.color.r;                       // 0 у корня, 1 у верхушки
            float3 wp = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
            float ph = (wp.x * 0.9 + wp.z * 0.6) * _WindScale + _Time.y * _WindSpeed;
            float g = sin(ph) + 0.5 * sin(ph * 2.3 + 1.7) + 0.25 * sin(ph * 5.1 + wp.x);
            float amp = _WindAmp * _CFWind * w * w;
            v.vertex.x += g * amp;
            v.vertex.z += g * amp * 0.6;
            v.vertex.y -= abs(g) * amp * 0.15;         // при наклоне верхушка чуть опускается
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb * lerp(1.0, 0.72, _CFWet);   // мокрая листва темнее
            o.Alpha = c.a;
            o.Emission = o.Albedo * unity_AmbientEquator.rgb * _Light;   // «просвет» листвы: растёт с окружающим светом, ночью гаснет вместе с ним
        }
        ENDCG
    }
    Fallback "Legacy Shaders/Transparent/Cutout/VertexLit"
}
