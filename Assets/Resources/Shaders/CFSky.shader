// China Fishing: небо. Градиент зенит–горизонт–земля, диск и ореол солнца, луна, звёзды ночью, лёгкие облака.
// Все параметры ставит Atmosphere. Очень дешёвый фрагментный шейдер (подходит для слабых телефонов).
Shader "ChinaFishing/Sky"
{
    Properties
    {
        _Zenith ("Зенит", Color) = (0.25,0.45,0.85,1)
        _Horizon ("Горизонт", Color) = (0.7,0.8,0.9,1)
        _Ground ("Под горизонтом", Color) = (0.3,0.35,0.3,1)
        _SunDir ("Направление на солнце", Vector) = (0,1,0,0)
        _SunColor ("Цвет солнца", Color) = (1,0.95,0.8,1)
        _MoonDir ("Направление на луну", Vector) = (0,-1,0,0)
        _Stars ("Звёзды 0..1", Range(0,1)) = 0
        _Cloud ("Облачность 0..1", Range(0,1)) = 0.35
        _CloudColor ("Цвет облаков", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Zenith, _Horizon, _Ground, _SunColor, _CloudColor;
            float4 _SunDir, _MoonDir;
            float _Stars, _Cloud;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash21 (float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vnoise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1,0)), f.x),
                            lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), f.x), f.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = d.y;

                // градиент
                float3 col = lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(up), 0.45));
                col = lerp(col, _Ground.rgb, saturate(-up * 5.0));

                // солнце: диск + ореол (на закате ореол широкий и тёплый)
                float sd = saturate(dot(d, normalize(_SunDir.xyz)));
                col += _SunColor.rgb * (pow(sd, 900.0) * 6.0 + pow(sd, 40.0) * 0.35 + pow(sd, 6.0) * 0.12) * step(-0.05, up);

                // луна: диск с мягким краем и слабым ореолом
                float md = saturate(dot(d, normalize(_MoonDir.xyz)));
                float disc = smoothstep(0.99935, 0.99965, md);
                col += float3(0.85, 0.9, 1.0) * disc * 1.3 + float3(0.35, 0.45, 0.7) * pow(md, 60.0) * 0.25;

                // звёзды: ячейки на сфере, мерцают
                float3 sp = floor(d * 170.0);
                float h = frac(sin(dot(sp, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float tw = 0.65 + 0.35 * sin(_Time.y * 2.0 + h * 40.0);
                col += step(0.9968, h) * tw * _Stars * saturate(up * 3.0) * float3(0.9, 0.95, 1.0);

                // облака: плоскость на высоте, 2 октавы шума, медленный дрейф
                if (up > 0.02)
                {
                    float2 cp = d.xz / (up + 0.25) * 1.6 + _Time.y * float2(0.004, 0.0015);
                    float n = vnoise(cp) * 0.65 + vnoise(cp * 2.3 + 7.0) * 0.35;
                    float cov = smoothstep(1.0 - _Cloud, 1.0 - _Cloud + 0.3, n) * saturate(up * 4.0);
                    col = lerp(col, _CloudColor.rgb, cov * 0.85);
                }
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
