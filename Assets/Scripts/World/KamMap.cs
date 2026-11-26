using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChinaFishing
{
    // Карта «Камышевское озеро»: сцена из Prisma 3D (дома, мостки, камыши, кувшинки, лес, камни, трава, дорога, вода).
    // Данные лежат в Assets/Resources/Kamyshevskoe: kam_scene.bytes (меши по материалам), kam_height.bytes (сетка высот 171×171
    // из меша «Ground»), PNG-текстуры сцены (*.bytes) и map_photo.bytes (фото для окна «Карта»).
    // Сцена отзеркалена по X относительно Prisma (иначе надписи на вывесках читаются задом наперёд). Вода — плоскость на высоте 0.
    // Мостки в модели обрезаны (пара досок и балка), поэтому в сцену они не попадают — их строит WorldBuilder.BuildKamPier.
    public class KamMap
    {
        public const string Id = "kamyshevskoe";
        public const float WaterY = 0f;
        public const float DepthScale = 3f;                       // дно озера всего до 3 м — растягиваем игровую глубину
        public const float WaterMinX = -26f, WaterMaxX = 143f, WaterMinZ = -42f, WaterMaxZ = 108f;
        // Какой кусок мира показывает фото на кнопке «Карта» (подогнано по подписям домов на фото)
        public const float PhotoX0 = -0.75f, PhotoX1 = 118.9f, PhotoZ0 = 0.2f, PhotoZ1 = 88.2f;
        public static readonly Vector3 SpawnPoint = new Vector3(4f, 0.71f, 21f);

        public static KamMap Data;

        public class MatInfo { public string name, tex; public Color color; public int mode; public float gloss; }   // mode: 0 обычный, 1 вырез по альфе, 2 прозрачный
        public class Part { public string cat, mat; public Mesh mesh; }

        public int cols, rows; public float minX, maxX, minZ, maxZ;
        public float[] h;
        public readonly Dictionary<string, MatInfo> mats = new Dictionary<string, MatInfo>();
        public readonly List<Part> parts = new List<Part>();
        public Texture2D photo;
        public Color waterColor = new Color(0.58f, 0.65f, 0.68f, 0.88f);

        static string Str(BinaryReader r) { int n = r.ReadUInt16(); return System.Text.Encoding.UTF8.GetString(r.ReadBytes(n)); }

        public static KamMap Load()
        {
            var scene = Resources.Load<TextAsset>("Kamyshevskoe/kam_scene");
            var hm = Resources.Load<TextAsset>("Kamyshevskoe/kam_height");
            if (scene == null || hm == null) return null;
            var m = new KamMap();
            try { m.ParseHeight(hm.bytes); m.ParseScene(scene.bytes); }
            catch (System.Exception e) { Debug.LogError("China Fishing: не удалось прочитать карту Камышевское озеро: " + e.Message); return null; }
            var ph = Resources.Load<TextAsset>("Kamyshevskoe/map_photo");
            if (ph != null)
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (t.LoadImage(ph.bytes, false)) { t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; m.photo = t; }
            }
            return m;
        }

        void ParseHeight(byte[] b)
        {
            using (var r = new BinaryReader(new MemoryStream(b)))
            {
                cols = r.ReadInt32(); rows = r.ReadInt32();
                minX = r.ReadSingle(); maxX = r.ReadSingle(); minZ = r.ReadSingle(); maxZ = r.ReadSingle();
                h = new float[cols * rows];
                for (int i = 0; i < h.Length; i++) h[i] = r.ReadSingle();     // индекс = строка(Z по возрастанию) * cols + столбец(X по возрастанию)
            }
        }

        void ParseScene(byte[] b)
        {
            using (var r = new BinaryReader(new MemoryStream(b)))
            {
                if (r.ReadByte() != 'K' || r.ReadByte() != 'A' || r.ReadByte() != 'M' || r.ReadByte() != '1') throw new IOException("неверный формат");
                int nm = r.ReadInt32();
                for (int i = 0; i < nm; i++)
                {
                    var mi = new MatInfo(); mi.name = Str(r);
                    float cr = r.ReadSingle(), cg = r.ReadSingle(), cb = r.ReadSingle(), ca = r.ReadSingle();
                    mi.color = new Color(cr, cg, cb, ca);
                    mi.tex = Str(r); mi.mode = r.ReadInt32(); mi.gloss = r.ReadSingle();
                    mats[mi.name] = mi;
                    if (mi.name == "Water") waterColor = mi.color;
                }
                int np = r.ReadInt32();
                for (int p = 0; p < np; p++)
                {
                    var part = new Part(); part.cat = Str(r); part.mat = Str(r);
                    int nv = r.ReadInt32(), ni = r.ReadInt32();
                    var v = new Vector3[nv]; var n = new Vector3[nv]; var uv = new Vector2[nv]; var tri = new int[ni];
                    for (int i = 0; i < nv; i++) v[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    for (int i = 0; i < nv; i++) n[i] = new Vector3(r.ReadSByte() / 127f, r.ReadSByte() / 127f, r.ReadSByte() / 127f);
                    for (int i = 0; i < nv; i++) uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    for (int i = 0; i < ni; i++) tri[i] = r.ReadInt32();
                    var mesh = new Mesh(); mesh.name = part.cat + "_" + part.mat;
                    if (nv > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices = v; mesh.normals = n; mesh.uv = uv; mesh.triangles = tri; mesh.RecalculateBounds();
                    part.mesh = mesh; parts.Add(part);
                }
            }
        }

        // ---------------- ВЫСОТА ----------------
        public float Height(float x, float z)
        {
            float fi = Mathf.Clamp((x - minX) / (maxX - minX) * (cols - 1), 0f, cols - 1.001f);
            float fj = Mathf.Clamp((z - minZ) / (maxZ - minZ) * (rows - 1), 0f, rows - 1.001f);
            int i = (int)fi, j = (int)fj; float tx = fi - i, tz = fj - j;
            float a = h[j * cols + i], b = h[j * cols + i + 1], c = h[(j + 1) * cols + i], d = h[(j + 1) * cols + i + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        // Игровая «высота» относительно воды: ниже нуля — вода (глубина растянута), выше — суша
        public float GameDepth(float x, float z)
        {
            float d = Height(x, z) - WaterY;
            return d < 0f ? d * DepthScale : d;
        }

        public bool Inside(float x, float z, float margin)
        {
            return x > minX + margin && x < maxX - margin && z > minZ + margin && z < maxZ - margin;
        }

        // Грубый меш для коллайдера земли: каждая 2-я точка сетки
        public Mesh BuildGroundCollider()
        {
            int step = 2, cc = (cols - 1) / step + 1, rr = (rows - 1) / step + 1;
            var v = new Vector3[cc * rr];
            for (int j = 0; j < rr; j++)
                for (int i = 0; i < cc; i++)
                {
                    int gi = Mathf.Min(i * step, cols - 1), gj = Mathf.Min(j * step, rows - 1);
                    v[j * cc + i] = new Vector3(minX + (maxX - minX) * gi / (cols - 1), h[gj * cols + gi], minZ + (maxZ - minZ) * gj / (rows - 1));
                }
            var t = new List<int>((cc - 1) * (rr - 1) * 6);
            for (int j = 0; j < rr - 1; j++)
                for (int i = 0; i < cc - 1; i++)
                {
                    int a = j * cc + i, b = a + 1, c = a + cc, d = c + 1;
                    t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d);
                }
            var m = new Mesh(); m.name = "Земля-коллайдер";
            if (v.Length > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = v; m.triangles = t.ToArray();
            return m;
        }

        // Зелёная трава: та же текстура «Rock», но с зелёным оттенком (в модели земля зелёная)
        public Material GetGroundMaterial()
        {
            Material m;
            if (matCache.TryGetValue("_ground", out m)) return m;
            m = new Material(GetMaterial("Rock")); m.name = "Земля-трава";
            var c = new Color(0.58f, 0.88f, 0.42f, 1f);
            Mats.Col(m, "_Color", c); Mats.Col(m, "_BaseColor", c);
            matCache["_ground"] = m;
            return m;
        }

        // ---------------- МАТЕРИАЛЫ ----------------
        readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

        public Material GetMaterial(string name)
        {
            Material m;
            if (matCache.TryGetValue(name, out m)) return m;
            MatInfo mi;
            if (!mats.TryGetValue(name, out mi)) { m = Mats.Lit(new Color(0.5f, 0.5f, 0.5f), 0.1f); matCache[name] = m; return m; }
            Material baseMat = null;
            if (mi.mode == 1) baseMat = Resources.Load<Material>("Mat_Cutout");   // шаблон с включённым вырезом по альфе (создаёт ProjectBuilder)
            float gloss = mi.mode == 1 ? 0.05f : Mathf.Clamp01(mi.gloss);
            m = baseMat != null ? new Material(baseMat) : new Material(Mats.Lit(new Color(mi.color.r, mi.color.g, mi.color.b, 1f), gloss));
            m.name = name;
            Mats.Flt(m, "_Glossiness", gloss); Mats.Flt(m, "_Smoothness", gloss);
            if (!string.IsNullOrEmpty(mi.tex))
            {
                var ta = Resources.Load<TextAsset>("Kamyshevskoe/" + mi.tex);
                if (ta != null)
                {
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    if (t.LoadImage(ta.bytes, false))
                    {
                        t.wrapMode = TextureWrapMode.Repeat; t.filterMode = FilterMode.Bilinear; t.anisoLevel = 4;
                        m.mainTexture = t;
                        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
                        Mats.Col(m, "_Color", Color.white); Mats.Col(m, "_BaseColor", Color.white);
                    }
                }
            }
            if (mi.mode == 1)
            {
                // трава, камыши, кувшинки: прозрачные места текстуры вырезаются (двусторонность заложена в меш)
                Mats.Flt(m, "_Mode", 1f); Mats.Flt(m, "_Cutoff", 0.5f);
                Mats.Flt(m, "_SrcBlend", 1f); Mats.Flt(m, "_DstBlend", 0f); Mats.Flt(m, "_ZWrite", 1f);
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.EnableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 2450;
            }
            matCache[name] = m;
            return m;
        }
    }
}
