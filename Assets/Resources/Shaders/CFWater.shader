// China Fishing: вода. Две бегущие карты нормалей (мелкая рябь), отражение неба по Френелю, блик солнца/луны, туман.
// Цвета неба берутся из глобальных _CFSkyZenith/_CFSkyHorizon (ставит Atmosphere), поэтому вода «отражает» текущее небо.
// Честно: это не зеркальное отражение берега и деревьев (реальное планарное отражение слишком тяжёлое для слабых телефонов).
Shader "ChinaFishing/Water"
{
    Properties
    {
        _BumpMap ("Карта нормалей", 2D) = "bump" {}
        _Color ("Цвет мелководья (A = прозрачность)", Color) = (0.10,0.32,0.36,0.78)
        _DeepColor ("Цвет глубины", Color) = (0.02,0.10,0.14,1)
        _NormalStrength ("Сила ряби", Range(0,2)) = 0.7
        _WaveSpeed ("Скорость волн", Float) = 1
        _Fresnel ("Степень Френеля", Range(1,8)) = 4
        _SpecPower ("Острота блика", Range(20,1500)) = 500
        _Reflect ("Сила отражения неба", Range(0,1.5)) = 0.9
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _BumpMap;
            float4 _BumpMap_ST;
            fixed4 _Color, _DeepColor;
            float _NormalStrength, _WaveSpeed, _Fresnel, _SpecPower, _Reflect;
            float4 _CFSkyZenith, _CFSkyHorizon, _CFWaterLight;   // глобально, ставит Atmosphere
            float _CFWet;

            // Круги дождя: у каждой клетки мира своя капля, кольцо расходится и затухает
            float2 Ripple(float2 p, float t)
            {
                float2 cell = floor(p);
                float2 f = frac(p) - 0.5;
                float h = frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
                float2 c = float2(frac(h * 7.31), frac(h * 3.17)) - 0.5;
                float ph = frac(t * 0.8 + h);
                float2 d = f - c * 0.5;
                float len = length(d);
                float r = ph * 0.45;
                float ring = sin((len - r) * 42.0) * exp(-abs(len - r) * 16.0) * (1.0 - ph);
                return normalize(d + 1e-4) * ring;
            }

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float2 uv = TRANSFORM_TEX(v.uv, _BumpMap);
                float t = _Time.y * _WaveSpeed;
                o.uv.xy = uv + float2(0.020, 0.013) * t;
                o.uv.zw = uv * 1.73 + float2(-0.016, 0.021) * t + 0.37;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float k = _NormalStrength * (1.0 + _CFWet * 0.8);   // в дождь рябь сильнее
                float2 n1 = tex2D(_BumpMap, i.uv.xy).xy * 2 - 1;
                float2 n2 = tex2D(_BumpMap, i.uv.zw).xy * 2 - 1;
                // третий слой по мировым координатам, повёрнутый и крупный — рисунок ряби не повторяется плиткой
                float2 wp = i.wpos.xz;
                float2 w3 = float2(wp.x * 0.8 - wp.y * 0.6, wp.x * 0.6 + wp.y * 0.8) * 0.045 + _Time.y * float2(0.010, -0.007) * _WaveSpeed;
                float2 n3 = tex2D(_BumpMap, w3).xy * 2 - 1;
                float2 nn = (n1 + n2) * 0.8 + n3 * 0.6;

                // круги от дождя (только когда мокро)
                float rainK = saturate(_CFWet * 1.4);
                float2 rp = Ripple(wp * 1.3, _Time.y) + Ripple(wp * 2.1 + 17.0, _Time.y * 1.3) * 0.7;
                nn += rp * 0.55 * rainK;

                float3 n = normalize(float3(nn.x * k, 1.0, nn.y * k));

                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                float ndv = saturate(dot(n, V));
                float fres = pow(1.0 - ndv, _Fresnel);
                fres = lerp(0.04, 1.0, fres);

                float3 R = reflect(-V, n);
                float3 sky = lerp(_CFSkyHorizon.rgb, _CFSkyZenith.rgb, saturate(R.y * 1.2));
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float spec = pow(saturate(dot(R, L)), _SpecPower);

                float3 body = lerp(_DeepColor.rgb, _Color.rgb, ndv) * _CFWaterLight.rgb;
                float3 col = lerp(body, sky * _Reflect, fres);
                col += _LightColor0.rgb * spec * 2.2;

                float a = saturate(lerp(_Color.a, 1.0, fres) + spec);
                fixed4 c = fixed4(col, a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    Fallback "Legacy Shaders/Transparent/Diffuse"
}
