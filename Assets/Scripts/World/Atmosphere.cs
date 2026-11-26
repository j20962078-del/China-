using System.Collections.Generic;
using UnityEngine;

namespace ChinaFishing
{
    // Атмосфера: цикл дня и ночи, небо (шейдер ChinaFishing/Sky), солнце и луна, ambient и туман,
    // свечение окон и фонарей ночью, дождь (частицы + звук + мокрые поверхности), ветер.
    // Рассчитано на Built-in Render Pipeline и слабые телефоны: один направленный свет, нет постобработки,
    // дорогие вещи (точечные источники света) ограничены маленьким пулом и считаются по вершинам.
    public class Atmosphere : MonoBehaviour
    {
        public static Atmosphere I;

        public enum RainMode { Auto, On, Off }

        public float hour = 9.5f;            // время суток, 0..24
        public float dayMinutes = 24f;       // длина суток в реальных минутах
        public bool timePaused;
        public RainMode rainMode = RainMode.Auto;

        public float Rain { get; private set; }      // 0..1 — сейчас идёт дождь
        public float Wet { get; private set; }       // 0..1 — насколько всё мокрое (сохнет медленнее, чем кончается дождь)
        public float Night { get; private set; }     // 0 день … 1 ночь
        public bool IsRaining { get { return Rain > 0.1f; } }

        Light sun;
        Material sky;
        Camera cam;
        ParticleSystem rainPs;
        Material rainMat;
        float rainTarget, rainTimer = 150f;

        // ---------- реестры материалов (материалы сами регистрируются при создании) ----------
        class WetMat { public Material m; public Color color; public float gloss; }
        static readonly List<WetMat> wetMats = new List<WetMat>();
        static readonly List<Material> glowMats = new List<Material>();

        public static void RegisterWet(Material m)
        {
            if (m == null) return;
            var w = new WetMat { m = m, gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0f };
            w.color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            wetMats.Add(w);
        }

        public static void RegisterGlow(Material m) { if (m != null) glowMats.Add(m); }

        // ---------- ключевые кадры суток ----------
        struct Key
        {
            public float t;
            public Color zenith, horizon, ground, ambSky, ambEq, ambGnd, sunCol, fog, cloud;
            public float sunI, fogD, stars, cloudAmt;
        }

        static Key K(float t, Color zen, Color hor, Color gnd, Color aS, Color aE, Color aG, Color sunCol, float sunI, Color fog, float fogD, float stars, Color cloud, float cloudAmt)
        {
            return new Key { t = t, zenith = zen, horizon = hor, ground = gnd, ambSky = aS, ambEq = aE, ambGnd = aG, sunCol = sunCol, sunI = sunI, fog = fog, fogD = fogD, stars = stars, cloud = cloud, cloudAmt = cloudAmt };
        }

        static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

        static readonly Key[] keys = BuildKeys();

        static Key[] BuildKeys()
        {
            var night = K(0f, C(0.012f, 0.028f, 0.085f), C(0.045f, 0.085f, 0.19f), C(0.02f, 0.03f, 0.05f),
                C(0.06f, 0.09f, 0.18f), C(0.045f, 0.065f, 0.12f), C(0.02f, 0.03f, 0.05f),
                C(0.6f, 0.7f, 1f), 0f, C(0.035f, 0.06f, 0.12f), 0.0045f, 1f, C(0.10f, 0.14f, 0.24f), 0.3f);
            var dawn = K(5.6f, C(0.17f, 0.24f, 0.46f), C(0.95f, 0.6f, 0.42f), C(0.25f, 0.22f, 0.22f),
                C(0.3f, 0.36f, 0.55f), C(0.3f, 0.28f, 0.3f), C(0.14f, 0.12f, 0.1f),
                C(1f, 0.68f, 0.45f), 0.75f, C(0.68f, 0.52f, 0.48f), 0.0045f, 0.15f, C(0.95f, 0.65f, 0.55f), 0.4f);
            var morning = K(7.5f, C(0.28f, 0.5f, 0.88f), C(0.74f, 0.84f, 0.93f), C(0.35f, 0.4f, 0.33f),
                C(0.5f, 0.65f, 0.9f), C(0.46f, 0.5f, 0.5f), C(0.22f, 0.2f, 0.16f),
                C(1f, 0.92f, 0.78f), 1.1f, C(0.72f, 0.8f, 0.88f), 0.0035f, 0f, C(1f, 0.98f, 0.95f), 0.4f);
            var noon = K(12f, C(0.2f, 0.44f, 0.9f), C(0.7f, 0.83f, 0.95f), C(0.35f, 0.4f, 0.33f),
                C(0.55f, 0.7f, 0.95f), C(0.5f, 0.54f, 0.52f), C(0.25f, 0.22f, 0.17f),
                C(1f, 0.96f, 0.88f), 1.25f, C(0.72f, 0.82f, 0.9f), 0.003f, 0f, C(1f, 1f, 1f), 0.35f);
            var late = K(16.5f, C(0.24f, 0.46f, 0.88f), C(0.74f, 0.82f, 0.92f), C(0.35f, 0.4f, 0.33f),
                C(0.5f, 0.65f, 0.9f), C(0.47f, 0.5f, 0.49f), C(0.22f, 0.2f, 0.16f),
                C(1f, 0.93f, 0.8f), 1.1f, C(0.73f, 0.8f, 0.88f), 0.0035f, 0f, C(1f, 0.98f, 0.94f), 0.4f);
            var sunset = K(19f, C(0.22f, 0.28f, 0.55f), C(1f, 0.5f, 0.28f), C(0.28f, 0.22f, 0.2f),
                C(0.38f, 0.4f, 0.6f), C(0.4f, 0.32f, 0.3f), C(0.16f, 0.12f, 0.1f),
                C(1f, 0.55f, 0.3f), 0.8f, C(0.8f, 0.48f, 0.38f), 0.0042f, 0.1f, C(1f, 0.6f, 0.45f), 0.45f);
            var dusk = K(20.4f, C(0.05f, 0.08f, 0.24f), C(0.38f, 0.28f, 0.4f), C(0.1f, 0.1f, 0.13f),
                C(0.14f, 0.18f, 0.32f), C(0.14f, 0.13f, 0.2f), C(0.06f, 0.06f, 0.08f),
                C(0.8f, 0.4f, 0.3f), 0.25f, C(0.2f, 0.18f, 0.28f), 0.0045f, 0.7f, C(0.3f, 0.25f, 0.4f), 0.4f);
            var night2 = night; night2.t = 22f;
            var night3 = night; night3.t = 24f;
            return new[] { night, dawn, morning, noon, late, sunset, dusk, night2, night3 };
        }

        static Key Sample(float h)
        {
            h = Mathf.Repeat(h, 24f);
            int i = 0;
            while (i < keys.Length - 2 && h >= keys[i + 1].t) i++;
            var a = keys[i]; var b = keys[i + 1];
            float f = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.t, b.t, h));
            return new Key
            {
                zenith = Color.Lerp(a.zenith, b.zenith, f), horizon = Color.Lerp(a.horizon, b.horizon, f), ground = Color.Lerp(a.ground, b.ground, f),
                ambSky = Color.Lerp(a.ambSky, b.ambSky, f), ambEq = Color.Lerp(a.ambEq, b.ambEq, f), ambGnd = Color.Lerp(a.ambGnd, b.ambGnd, f),
                sunCol = Color.Lerp(a.sunCol, b.sunCol, f), fog = Color.Lerp(a.fog, b.fog, f), cloud = Color.Lerp(a.cloud, b.cloud, f),
                sunI = Mathf.Lerp(a.sunI, b.sunI, f), fogD = Mathf.Lerp(a.fogD, b.fogD, f), stars = Mathf.Lerp(a.stars, b.stars, f),
                cloudAmt = Mathf.Lerp(a.cloudAmt, b.cloudAmt, f)
            };
        }

        // ---------- создание ----------
        public static Atmosphere Create(Light sunLight)
        {
            if (I != null) Destroy(I.gameObject);
            var go = new GameObject("Атмосфера");
            var a = go.AddComponent<Atmosphere>();
            I = a;
            a.Init(sunLight);
            return a;
        }

        void Init(Light sunLight)
        {
            sun = sunLight;
            var sh = Resources.Load<Shader>("Shaders/CFSky");
            if (sh != null && sh.isSupported)
            {
                sky = new Material(sh);
                RenderSettings.skybox = sky;
            }
            else
            {
                var ps = Shader.Find("Skybox/Procedural");
                if (ps != null) { sky = new Material(ps); RenderSettings.skybox = sky; }
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;   // не пересчитывать отражения неба на телефоне
            BuildRain();
            Apply(0f);
        }

        // ---------- фонари: пул точечных источников света ----------
        readonly List<Vector3> lamps = new List<Vector3>();
        readonly List<Light> lampLights = new List<Light>();
        float lampTimer;

        // Центры фонарей берутся из меша «LampGlow» (вершины лампочек группируются по ячейкам 2,5 м)
        public void AddLampsFromMesh(Mesh mesh)
        {
            if (mesh == null) return;
            var sums = new Dictionary<long, Vector4>();
            foreach (var v in mesh.vertices)
            {
                long key = ((long)Mathf.RoundToInt(v.x / 2.5f) << 32) ^ (long)(Mathf.RoundToInt(v.z / 2.5f) & 0xFFFFFFF);
                Vector4 s; sums.TryGetValue(key, out s);
                sums[key] = new Vector4(s.x + v.x, s.y + v.y, s.z + v.z, s.w + 1f);
            }
            foreach (var kv in sums) lamps.Add(new Vector3(kv.Value.x, kv.Value.y, kv.Value.z) / kv.Value.w);
            int n = Game.S != null ? (Game.S.quality == 0 ? 1 : Game.S.quality == 1 ? 2 : 4) : 2;
            while (lampLights.Count < n)
            {
                var g = new GameObject("Свет фонаря");
                g.transform.SetParent(transform, false);
                var l = g.AddComponent<Light>();
                l.type = LightType.Point; l.range = 16f; l.intensity = 0f; l.color = new Color(1f, 0.8f, 0.5f);
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForceVertex;   // без дополнительных проходов: дёшево для слабых телефонов
                lampLights.Add(l);
            }
        }

        void UpdateLamps(float glow)
        {
            lampTimer -= Time.deltaTime;
            if (lampTimer > 0f) { for (int i = 0; i < lampLights.Count; i++) if (lampLights[i].enabled) lampLights[i].intensity = Mathf.Lerp(lampLights[i].intensity, 2.2f * glow, Time.deltaTime * 4f); return; }
            lampTimer = 0.5f;
            if (cam == null || lamps.Count == 0) return;
            var cp = cam.transform.position;
            var order = new List<int>();
            for (int i = 0; i < lamps.Count; i++) order.Add(i);
            order.Sort((a, b) => (lamps[a] - cp).sqrMagnitude.CompareTo((lamps[b] - cp).sqrMagnitude));
            for (int i = 0; i < lampLights.Count; i++)
            {
                var l = lampLights[i];
                if (i < order.Count && (lamps[order[i]] - cp).sqrMagnitude < 70f * 70f && glow > 0.02f)
                {
                    l.transform.position = lamps[order[i]] + Vector3.down * 0.3f;
                    l.enabled = true;
                }
                else { l.enabled = false; l.intensity = 0f; }
            }
        }

        // ---------- дождь ----------
        void BuildRain()
        {
            var tex = new Texture2D(4, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 4; x++)
                {
                    float a = Mathf.Sin(Mathf.PI * y / 31f) * (x == 1 || x == 2 ? 1f : 0.25f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply(); tex.wrapMode = TextureWrapMode.Clamp;
            var sh = Resources.Load<Shader>("Shaders/CFRain");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            rainMat = new Material(sh);
            rainMat.mainTexture = tex;

            var go = new GameObject("Дождь");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);       // эмиссия вниз
            rainPs = go.AddComponent<ParticleSystem>();
            var main = rainPs.main;
            main.loop = true; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.9f;
            main.startSpeed = 20f;
            main.startSize = 0.05f;
            main.startColor = new Color(0.78f, 0.85f, 0.95f, 0.38f);
            main.maxParticles = 1800;
            var sh2 = rainPs.shape;
            sh2.enabled = true; sh2.shapeType = ParticleSystemShapeType.Box; sh2.scale = new Vector3(26f, 26f, 0.5f);
            var em = rainPs.emission; em.enabled = true; em.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2.2f; r.velocityScale = 0.02f;
            r.sharedMaterial = rainMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        float RainRate()
        {
            int q = Game.S != null ? Game.S.quality : 1;
            return q == 0 ? 450f : q == 1 ? 900f : 1400f;
        }

        // ---------- публичное управление ----------
        public void SetRain(RainMode mode) { rainMode = mode; if (mode == RainMode.On) rainTarget = 1f; else if (mode == RainMode.Off) rainTarget = 0f; }
        public void ToggleRain() { SetRain(rainTarget > 0.5f ? RainMode.Off : RainMode.On); }
        public void SetHour(float h) { hour = Mathf.Repeat(h, 24f); }

        // ---------- обновление ----------
        void Update()
        {
            if (cam == null) cam = Camera.main;
            if (!timePaused) hour = Mathf.Repeat(hour + Time.deltaTime * 24f / (dayMinutes * 60f), 24f);

            if (rainMode == RainMode.Auto)
            {
                rainTimer -= Time.deltaTime;
                if (rainTimer <= 0f)
                {
                    bool start = rainTarget < 0.5f;
                    rainTarget = start ? 1f : 0f;
                    rainTimer = start ? Random.Range(70f, 180f) : Random.Range(150f, 420f);
                }
            }
            Rain = Mathf.MoveTowards(Rain, rainTarget, Time.deltaTime / 8f);
            Wet = Mathf.MoveTowards(Wet, rainTarget > 0.5f ? 1f : 0f, Time.deltaTime / (rainTarget > 0.5f ? 20f : 100f));
            Apply(Time.deltaTime);
        }

        void Apply(float dt)
        {
            var k = Sample(hour);

            // положение солнца и луны
            float a = (hour - 6f) / 12f * Mathf.PI;
            var sunPos = new Vector3(Mathf.Cos(a) * 0.8f, Mathf.Sin(a), -0.55f).normalized;
            var moonPos = new Vector3(-sunPos.x * 0.85f, -sunPos.y, 0.45f).normalized;
            float sunFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.04f, 0.16f, sunPos.y));
            float moonFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.04f, 0.2f, moonPos.y)) * (1f - sunFade);
            Night = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.1f, sunPos.y));

            float rain = Rain;
            float dark = Mathf.Lerp(1f, 0.55f, rain);       // дождь: тучи гасят свет
            Color Dull(Color c) { float g = (c.r + c.g + c.b) / 3f; return Color.Lerp(c, new Color(g, g * 1.02f, g * 1.06f), 0.45f * rain) * dark; }

            // небо
            if (sky != null)
            {
                sky.SetColor("_Zenith", Dull(k.zenith)); sky.SetColor("_Horizon", Dull(k.horizon)); sky.SetColor("_Ground", Dull(k.ground));
                sky.SetVector("_SunDir", sunPos); sky.SetVector("_MoonDir", moonPos);
                sky.SetColor("_SunColor", k.sunCol * (1f - rain * 0.85f));
                sky.SetFloat("_Stars", k.stars * (1f - rain));
                sky.SetFloat("_Cloud", Mathf.Lerp(k.cloudAmt, 0.95f, rain));
                sky.SetColor("_CloudColor", Dull(k.cloud) * (1f - rain * 0.2f));
            }

            // свет: солнце днём, луна ночью (один направленный свет)
            if (sun != null)
            {
                bool useSun = sunFade >= moonFade;
                var pos = useSun ? sunPos : moonPos;
                sun.transform.rotation = Quaternion.LookRotation(-pos);
                sun.color = useSun ? k.sunCol : new Color(0.55f, 0.66f, 1f);
                sun.intensity = (useSun ? k.sunI * sunFade : 0.32f * moonFade) * Mathf.Lerp(1f, 0.4f, rain);
                sun.shadowStrength = useSun ? 0.8f * (1f - rain * 0.7f) : 0.5f;
            }

            RenderSettings.ambientSkyColor = Dull(k.ambSky);
            RenderSettings.ambientEquatorColor = Dull(k.ambEq);
            RenderSettings.ambientGroundColor = Dull(k.ambGnd);
            RenderSettings.fogColor = Dull(k.fog);
            RenderSettings.fogDensity = k.fogD * (1f + rain * 0.9f);

            // глобальные параметры шейдеров
            Shader.SetGlobalColor("_CFSkyZenith", Dull(k.zenith));
            Shader.SetGlobalColor("_CFSkyHorizon", Dull(k.horizon));
            // цвет воды в свете: от дневного белого до тёмно-синего ночью
            Color water = Color.Lerp(new Color(1f, 1f, 1f), new Color(0.14f, 0.2f, 0.34f), Night);
            Shader.SetGlobalColor("_CFWaterLight", water * Mathf.Lerp(1f, 0.7f, rain));
            float gust = 0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 0.15f, 7.1f);
            float wind = (0.7f + rain * 0.9f) * gust;
            Shader.SetGlobalFloat("_CFWind", wind);
            Shader.SetGlobalFloat("_CFWet", Wet);

            // свечение окон и фонарей
            float glow = Mathf.Clamp01(Night + rain * 0.25f * (1f - Night) * (sunPos.y < 0.4f ? 1f : 0f));
            for (int i = 0; i < glowMats.Count; i++) if (glowMats[i] != null) glowMats[i].SetFloat("_GlowAmount", glow);
            UpdateLamps(glow);

            // мокрые поверхности: темнее и блестят
            for (int i = 0; i < wetMats.Count; i++)
            {
                var w = wetMats[i]; if (w.m == null) continue;
                Mats.Col(w.m, "_Color", Color.Lerp(w.color, w.color * 0.68f, Wet));
                Mats.Flt(w.m, "_Glossiness", Mathf.Lerp(w.gloss, Mathf.Max(w.gloss, 0.6f), Wet));
            }

            // дождь: эмиттер следует за камерой
            if (rainPs != null)
            {
                var em = rainPs.emission; em.rateOverTime = RainRate() * Rain;
                if (cam != null) rainPs.transform.position = cam.transform.position + Vector3.up * 12f;
                if (Rain > 0.02f && !rainPs.isPlaying) rainPs.Play();
                else if (Rain <= 0.02f && rainPs.isPlaying) rainPs.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            // звук: ветер, птицы (только днём и без дождя), дождь, сверчки ночью
            if (AudioManager.I != null)
                AudioManager.I.SetAmbience(Mathf.Clamp01(0.35f + wind * 0.45f), Rain, Mathf.Clamp01((1f - Night) * (1f - Rain * 1.2f)), Night * (1f - Rain));
        }
    }
}
