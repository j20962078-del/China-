using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ChinaFishing
{
    // 3D-модели удилищ (OBJ без костей) из Resources/Rods. Удилище лежит вдоль оси Z: торец рукояти в нуле, вершинка на Z = длина.
    // Материалы собираются по файлам Resources/RodMtl/*.txt (копии .mtl), поэтому цвета верны даже если Unity не подхватил .mtl сам.
    public static class RodModels
    {
        public const float Grip = 0.42f;   // расстояние от торца рукояти до ладони, м

        class MtlEntry { public Color kd = new Color(0.5f, 0.5f, 0.5f); public float ks, ns = 30f; }

        static readonly Dictionary<string, Dictionary<string, MtlEntry>> mtlCache = new Dictionary<string, Dictionary<string, MtlEntry>>();
        static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

        // Есть ли у предмета модель, которую Unity реально импортировал
        public static bool Has(ItemDef d)
        {
            return d != null && !string.IsNullOrEmpty(d.model) && Resources.Load<GameObject>("Rods/" + d.model) != null;
        }

        // Новый экземпляр модели (без родителя) или null, если модели нет
        public static GameObject Create(ItemDef d)
        {
            if (d == null || string.IsNullOrEmpty(d.model)) return null;
            var prefab = Resources.Load<GameObject>("Rods/" + d.model);
            if (prefab == null) { Debug.LogWarning("China Fishing: модель удилища не найдена: Rods/" + d.model); return null; }
            var go = Object.Instantiate(prefab);
            go.name = "Удилище_" + d.name;
            go.transform.localScale = Vector3.one;
            ApplyMaterials(go, d.model);
            return go;
        }

        static void ApplyMaterials(GameObject go, string model)
        {
            var mtl = Mtl(model);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                var res = new Material[shared.Length];
                for (int i = 0; i < shared.Length; i++)
                {
                    string nm = shared[i] != null ? shared[i].name : null;
                    MtlEntry e = null;
                    if (nm == null || !mtl.TryGetValue(nm, out e))
                    {
                        nm = r.gameObject.name;
                        if (!mtl.TryGetValue(nm, out e)) e = null;
                    }
                    res[i] = e != null ? Make(model, e, nm) : shared[i];
                }
                r.sharedMaterials = res;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        static Material Make(string model, MtlEntry e, string name)
        {
            string key = model + "/" + name;
            Material m;
            if (matCache.TryGetValue(key, out m) && m != null) return m;
            float smooth = Mathf.Clamp(0.12f + e.ns / 300f, 0.1f, 0.8f);
            float metal = e.ks >= 0.6f ? 0.35f : 0f;
            m = Mats.Lit(e.kd, smooth, metal);
            matCache[key] = m;
            return m;
        }

        static Dictionary<string, MtlEntry> Mtl(string model)
        {
            Dictionary<string, MtlEntry> d;
            if (mtlCache.TryGetValue(model, out d)) return d;
            d = new Dictionary<string, MtlEntry>();
            var ta = Resources.Load<TextAsset>("RodMtl/" + model);
            if (ta != null)
            {
                MtlEntry cur = null;
                foreach (var raw in ta.text.Split('\n'))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("newmtl ")) { cur = new MtlEntry(); d[line.Substring(7).Trim()] = cur; continue; }
                    if (cur == null) continue;
                    var p = line.Split(' ');
                    if (p[0] == "Kd" && p.Length >= 4) cur.kd = new Color(F(p[1]), F(p[2]), F(p[3]));
                    else if (p[0] == "Ks" && p.Length >= 2) cur.ks = F(p[1]);
                    else if (p[0] == "Ns" && p.Length >= 2) cur.ns = F(p[1]);
                }
            }
            mtlCache[model] = d;
            return d;
        }

        static float F(string s)
        {
            float v;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
        }
    }

    // Картинка-превью удилища для карточек магазина и инвентаря: рисуется один раз и сохраняется в обычную текстуру
    public static class RodPreview
    {
        public const int W = 640, H = 288;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static int counter;

        public static float Aspect { get { return W / (float)H; } }

        public static Texture2D Get(ItemDef d)
        {
            Texture2D t;
            if (d == null) return null;
            if (cache.TryGetValue(d.id, out t) && t != null) return t;
            t = Render(d);
            if (t != null) cache[d.id] = t;
            return t;
        }

        static Texture2D Render(ItemDef d)
        {
            var model = RodModels.Create(d);
            if (model == null) return null;
            float L = d.length > 0.5f ? d.length : 3f;

            var stage = new GameObject("Просмотр_" + d.id);
            stage.transform.position = new Vector3(4000f + (counter++ % 50) * 30f, -800f, 0f);
            model.transform.SetParent(stage.transform, false);

            var lg = new GameObject("Свет"); lg.transform.SetParent(stage.transform, false);
            lg.transform.rotation = Quaternion.Euler(32f, -70f, 0f);
            var light = lg.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.25f; light.shadows = LightShadows.None;

            var cg = new GameObject("Камера"); cg.transform.SetParent(stage.transform, false);
            cg.transform.localPosition = new Vector3(5f, 0.03f, L * 0.5f);
            cg.transform.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.up) * Quaternion.Euler(0f, 0f, 12f);
            var cam = cg.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = L * 0.25f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.10f, 0.25f, 0.31f, 1f);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 30f; cam.allowHDR = false; cam.allowMSAA = true;
            cam.enabled = false;

            Texture2D tex = null;
            var rt = RenderTexture.GetTemporary(W, H, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var prev = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply(false, false);
            }
            catch (System.Exception e) { Debug.LogWarning("China Fishing: не удалось нарисовать превью удилища: " + e.Message); }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                light.enabled = false;
                stage.SetActive(false);
                Object.Destroy(stage);
            }
            return tex;
        }
    }
}
