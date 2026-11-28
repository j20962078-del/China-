using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ChinaFishing
{
    // Настоящие 3D-модели рыб (OBJ + PNG-текстуры) из Resources/Fish. Нос смотрит в +Z, верх +Y.
    // Материалы собираются по Resources/FishMtl/*.txt (копии .mtl): цвета и текстуры верны, даже если Unity не подхватил .mtl сам.
    // Если модели нет, FishModel.Build рисует прежнюю процедурную рыбу из примитивов.
    public static class FishModels
    {
        class MtlEntry { public Color kd = new Color(0.5f, 0.5f, 0.5f); public float ns = 30f; public string map; }

        static readonly Dictionary<string, Dictionary<string, MtlEntry>> mtlCache = new Dictionary<string, Dictionary<string, MtlEntry>>();
        static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

        // id рыбы в игре → имя файла модели (сом в архиве назван wels)
        public static string FileOf(string fishId) { return fishId == "catfish" ? "wels" : fishId; }

        public static bool Has(FishDef f) { return f != null && Resources.Load<GameObject>("Fish/" + FileOf(f.id)) != null; }

        // Новая рыба: корень в нуле, центр тела в корне, полная длина (с хвостом) = lengthM. Null, если модели нет.
        public static GameObject Create(FishDef f, float lengthM)
        {
            if (f == null) return null;
            string file = FileOf(f.id);
            var prefab = Resources.Load<GameObject>("Fish/" + file);
            if (prefab == null) return null;

            var root = new GameObject("Рыба_" + f.name);
            var model = Object.Instantiate(prefab);
            model.name = "Модель";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            ApplyMaterials(model, file);

            // корень только что создан в нуле без поворота, поэтому мировые границы = локальные
            Bounds b = new Bounds(); bool has = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            if (has && b.size.z > 0.001f)
            {
                float k = Mathf.Max(0.05f, lengthM) / b.size.z;
                model.transform.localScale = Vector3.one * k;
                model.transform.localPosition = -b.center * k;
            }
            return root;
        }

        static void ApplyMaterials(GameObject go, string file)
        {
            var mtl = Mtl(file);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                var res = new Material[shared.Length];
                for (int i = 0; i < shared.Length; i++)
                {
                    string nm = shared[i] != null ? shared[i].name : null;
                    MtlEntry e = null;
                    if (nm != null && nm.EndsWith(" (Instance)")) nm = nm.Substring(0, nm.Length - 11);
                    if (nm == null || !mtl.TryGetValue(nm, out e))
                    {
                        nm = r.gameObject.name;
                        if (!mtl.TryGetValue(nm, out e)) e = null;
                    }
                    res[i] = e != null ? Make(file, e, nm) : shared[i];
                }
                r.sharedMaterials = res;
            }
        }

        static Material Make(string file, MtlEntry e, string name)
        {
            string key = file + "/" + name;
            Material m;
            if (matCache.TryGetValue(key, out m) && m != null) return m;
            Texture2D tex = null;
            if (!string.IsNullOrEmpty(e.map)) tex = Resources.Load<Texture2D>("Fish/" + System.IO.Path.GetFileNameWithoutExtension(e.map));
            float smooth = Mathf.Clamp(0.3f + e.ns / 400f, 0.2f, 0.7f);   // мокрая рыба блестит
            m = new Material(Mats.Lit(tex != null ? Color.white : e.kd, smooth));
            if (tex != null)
            {
                m.mainTexture = tex;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            }
            matCache[key] = m;
            return m;
        }

        static Dictionary<string, MtlEntry> Mtl(string file)
        {
            Dictionary<string, MtlEntry> d;
            if (mtlCache.TryGetValue(file, out d)) return d;
            d = new Dictionary<string, MtlEntry>();
            var ta = Resources.Load<TextAsset>("FishMtl/" + file);
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
                    else if (p[0] == "Ns" && p.Length >= 2) cur.ns = F(p[1]);
                    else if (p[0] == "map_Kd" && p.Length >= 2) cur.map = p[1].Trim();
                }
            }
            mtlCache[file] = d;
            return d;
        }

        static float F(string s)
        {
            float v;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
        }
    }

    // Повадки видов: как рыба бьётся под водой и как ведёт себя при вываживании
    public struct FishBehaviourData
    {
        public float wiggleSpeed;   // скорость покачивания, рад/с
        public float wiggleAmount;  // размах покачивания, градусы
        public float thrash;        // во сколько раз сильнее мечется во время рывка
        public float burstFreq;     // частота рывков (1 = как раньше)
        public float burstPower;    // сила рывков (1 = как раньше)
        public float slip;          // насколько легко срывается с крючка при резком натяжении (1 = обычно)
    }

    public static class FishBehaviour
    {
        static FishBehaviourData B(float ws, float wa, float th, float bf, float bp, float sl)
        {
            return new FishBehaviourData { wiggleSpeed = ws, wiggleAmount = wa, thrash = th, burstFreq = bf, burstPower = bp, slip = sl };
        }

        public static FishBehaviourData For(string id)
        {
            switch (id)
            {
                case "catfish":    return B(2.2f, 14f, 1.8f, 0.55f, 1.35f, 0.6f);  // тяжёлый, редкие мощные рывки
                case "pike":       return B(5.5f, 26f, 1.4f, 1.5f, 1.15f, 1.35f);  // резкая, частые рывки, трясёт головой
                case "perch":      return B(7.0f, 18f, 0.9f, 1.35f, 0.8f, 1.0f);   // мелкий и активный
                case "zander":     return B(4.0f, 16f, 1.0f, 1.0f, 1.0f, 1.25f);   // мягкий рот — срывается легче
                case "carp":       return B(3.5f, 20f, 1.5f, 1.0f, 1.2f, 1.0f);    // сильные уходы
                case "crucian":    return B(5.0f, 14f, 0.7f, 1.1f, 0.6f, 1.1f);
                case "ruffe":      return B(8.0f, 22f, 0.7f, 1.3f, 0.55f, 0.9f);
                case "white_amur": return B(3.0f, 18f, 1.2f, 0.9f, 1.1f, 1.0f);
                case "black_amur": return B(2.8f, 18f, 1.3f, 0.9f, 1.25f, 0.9f);
                case "sturgeon":   return B(2.2f, 12f, 1.5f, 0.7f, 1.3f, 0.7f);
                case "beluga":     return B(1.6f, 10f, 2.0f, 0.55f, 1.5f, 0.5f);
                default:           return B(4.0f, 16f, 1.0f, 1.0f, 1.0f, 1.0f);
            }
        }
    }
}
