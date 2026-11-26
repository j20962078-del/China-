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
                float3 n = normalize(float3((n1.x + n2.x) * k, 1.0, (n1.y + n2.y) * k));

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
