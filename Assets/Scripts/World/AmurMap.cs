using System.Collections.Generic;
using UnityEngine;

namespace ChinaFishing
{
    // Карта «Река Амур» из архива amur_game_map.zip: рельеф 220×500 вершин, деревья, домики, дороги, мостки, лодки и план-текстура.
    // Читается прямо из исходного OBJ без упрощения: ни вершин, ни пикселей не теряется.
    // Оси OBJ: x — вправо (восток), y — вниз по плану (север сверху), z — высота. В игре: X = x, Z = -y (север = +Z), Y = z.
    public class AmurMap
    {
        public const string Id = "amur";
        public const int Cols = 220, Rows = 500;
        public const float HalfW = 110f, HalfH = 250f;
        public const float WaterY = 0.9f;      // уровень воды: чуть выше самых низких мест русла в модели (0…0,9)
        public const float DepthScale = 8f;    // игровая глубина = (уровень − дно) × 8: в модели русло мельче метра, а рыбе нужна глубина
        public const float Dx = 2f * HalfW / (Cols - 1), Dz = 2f * HalfH / (Rows - 1);
        public const float EdgeHeight = 1.77f; // высота по краю карты (плоская), от неё идут окрестности

        public static AmurMap Data;            // загруженная карта; null — ресурсов нет, мир строится по-старому

        public float[] h = new float[Cols * Rows];                      // высоты по сетке OBJ: индекс = строка * Cols + столбец
        public bool[] quadWater = new bool[(Cols - 1) * (Rows - 1)];    // клетки с материалом «water»
        public Texture2D plan;                                          // map_texture.png — план карты
        public Vector3 cabinCenter, dockCenter; public bool hasCabins, hasDocks;
        public readonly Dictionary<string, List<Vector3>> parts = new Dictionary<string, List<Vector3>>(); // материал → вершины треугольников

        public static float CX(int i) { return -HalfW + i * Dx; }
        public static float CZ(int j) { return HalfH - j * Dz; }

        public static AmurMap Load()
        {
            var obj = Resources.Load<TextAsset>("Amur/amur_game_map_obj");
            if (obj == null) return null;
            var m = new AmurMap();
            try { m.Parse(obj.bytes); }
            catch (System.Exception e) { Debug.LogError("China Fishing: не удалось прочитать карту Амура: " + e.Message); return null; }
            var png = Resources.Load<TextAsset>("Amur/map_texture_png");
            if (png != null)
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (t.LoadImage(png.bytes, false))
                {
                    t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; t.anisoLevel = 4;
                    m.plan = t;
                }
            }
            return m;
        }

        // ---------------- ВЫСОТА ----------------
        public float Height(float x, float z)
        {
            float fi = Mathf.Clamp((x + HalfW) / Dx, 0f, Cols - 1.001f);
            float fj = Mathf.Clamp((HalfH - z) / Dz, 0f, Rows - 1.001f);
            int i = (int)fi, j = (int)fj; float tx = fi - i, tz = fj - j;
            float a = h[j * Cols + i], b = h[j * Cols + i + 1], c = h[(j + 1) * Cols + i], d = h[(j + 1) * Cols + i + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        // Игровая «высота» относительно воды: ниже нуля — вода (глубина растянута), выше — суша
        public float GameDepth(float x, float z)
        {
            float d = Height(x, z) - WaterY;
            return d < 0f ? d * DepthScale : d;
        }

        public static bool Inside(float x, float z, float margin)
        {
            return Mathf.Abs(x) < HalfW - margin && Mathf.Abs(z) < HalfH - margin;
        }

        // ---------------- ЧТЕНИЕ OBJ ----------------
        struct Tri { public Vector3 a, b, c; public string mat; }

        static float F(byte[] b, ref int p, int end)
        {
            while (p < end && b[p] == 32) p++;
            bool neg = false;
            if (p < end && (b[p] == '-' || b[p] == '+')) { neg = b[p] == '-'; p++; }
            double v = 0; 
            while (p < end && b[p] >= '0' && b[p] <= '9') { v = v * 10 + (b[p] - '0'); p++; }
            if (p < end && b[p] == '.')
            {
                p++; double f = 0.1;
                while (p < end && b[p] >= '0' && b[p] <= '9') { v += (b[p] - '0') * f; f *= 0.1; p++; }
            }
            if (p < end && (b[p] == 'e' || b[p] == 'E'))
            {
                p++; bool en = false; int ex = 0;
                if (p < end && (b[p] == '-' || b[p] == '+')) { en = b[p] == '-'; p++; }
                while (p < end && b[p] >= '0' && b[p] <= '9') { ex = ex * 10 + (b[p] - '0'); p++; }
                v *= System.Math.Pow(10, en ? -ex : ex);
            }
            return (float)(neg ? -v : v);
        }

        static string Str(byte[] b, int from, int end)
        {
            while (end > from && (b[end - 1] == 13 || b[end - 1] == 32)) end--;
            while (from < end && b[from] == 32) from++;
            return System.Text.Encoding.ASCII.GetString(b, from, end - from);
        }

        static bool Starts(byte[] b, int p, int end, string s)
        {
            if (end - p < s.Length) return false;
            for (int i = 0; i < s.Length; i++) if (b[p + i] != s[i]) return false;
            return true;
        }

        static Vector3 W(Vector3 o) { return new Vector3(o.x, o.z, -o.y); }

        void Parse(byte[] b)
        {
            var vs = new List<Vector3>(140000);
            var tris = new List<Tri>();
            var idx = new List<int>(8);
            string group = null, mat = null;
            Vector3 cabMin = Vector3.one * 1e9f, cabMax = -Vector3.one * 1e9f, dockMin = cabMin, dockMax = cabMax;
            int n = b.Length, p = 0;

            while (p < n)
            {
                int e = p; while (e < n && b[e] != 10) e++;
                if (e - p > 2)
                {
                    byte c0 = b[p], c1 = b[p + 1];
                    if (c0 == 'v' && c1 == ' ')
                    {
                        int q = p + 2; float x = F(b, ref q, e), y = F(b, ref q, e), z = F(b, ref q, e);
                        vs.Add(new Vector3(x, y, z));
                    }
                    else if ((c0 == 'g' || c0 == 'o') && c1 == ' ')
                    {
                        Finish(tris, group);
                        group = Str(b, p + 2, e);
                    }
                    else if (c0 == 'u' && Starts(b, p, e, "usemtl "))
                    {
                        mat = Str(b, p + 7, e);
                    }
                    else if (c0 == 'f' && c1 == ' ')
                    {
                        idx.Clear();
                        int q = p + 2;
                        while (q < e)
                        {
                            while (q < e && b[q] == 32) q++;
                            if (q >= e || b[q] == 13) break;
                            int v = 0; bool any = false;
                            while (q < e && b[q] >= '0' && b[q] <= '9') { v = v * 10 + (b[q] - '0'); q++; any = true; }
                            while (q < e && b[q] != 32 && b[q] != 13) q++;   // пропустить /vt/vn
                            if (any) idx.Add(v - 1);
                        }
                        if (idx.Count >= 3)
                        {
                            if (group == "terrain")
                            {
                                int a0 = idx[0], i = a0 % Cols, j = a0 / Cols;
                                if (i < Cols - 1 && j < Rows - 1) quadWater[j * (Cols - 1) + i] = mat == "water";
                            }
                            else if (group != null)
                            {
                                for (int k = 1; k + 1 < idx.Count; k++)
                                {
                                    var t = new Tri { a = W(vs[idx[0]]), b = W(vs[idx[k]]), c = W(vs[idx[k + 1]]), mat = mat };
                                    tris.Add(t);
                                    if (group == "cabins") { Grow(ref cabMin, ref cabMax, t.a); Grow(ref cabMin, ref cabMax, t.b); Grow(ref cabMin, ref cabMax, t.c); }
                                    else if (group == "docks") { Grow(ref dockMin, ref dockMax, t.a); Grow(ref dockMin, ref dockMax, t.b); Grow(ref dockMin, ref dockMax, t.c); }
                                }
                            }
                        }
                    }
                }
                p = e + 1;
            }
            Finish(tris, group);

            if (vs.Count < Cols * Rows) throw new System.Exception("в OBJ меньше вершин рельефа, чем ожидается: " + vs.Count);
            for (int i = 0; i < Cols * Rows; i++) h[i] = vs[i].z;
            if (Mathf.Abs(vs[0].x + HalfW) > 0.5f || Mathf.Abs(vs[0].y + HalfH) > 0.5f || Mathf.Abs(vs[Cols].y - vs[0].y) < 0.1f)
                Debug.LogWarning("China Fishing: порядок вершин рельефа Амура отличается от ожидаемого.");
            hasCabins = cabMax.x > cabMin.x; if (hasCabins) cabinCenter = (cabMin + cabMax) * 0.5f;
            hasDocks = dockMax.x > dockMin.x; if (hasDocks) dockCenter = (dockMin + dockMax) * 0.5f;
        }

        static void Grow(ref Vector3 mn, ref Vector3 mx, Vector3 v) { mn = Vector3.Min(mn, v); mx = Vector3.Max(mx, v); }

        // Объект закончен: развернуть грани наружу (плоские — вверх) и сложить по материалам
        void Finish(List<Tri> tris, string group)
        {
            if (tris.Count > 0)
            {
                var c = Vector3.zero;
                foreach (var t in tris) c += t.a + t.b + t.c;
                c /= tris.Count * 3f;
                bool flat = group == "roads" || group == "docks";
                foreach (var t in tris)
                {
                    var nrm = Vector3.Cross(t.b - t.a, t.c - t.a);
                    bool flip = flat ? nrm.y < 0f : Vector3.Dot(nrm, (t.a + t.b + t.c) / 3f - c) < 0f;
                    List<Vector3> l;
                    if (!parts.TryGetValue(t.mat ?? "none", out l)) { l = new List<Vector3>(); parts[t.mat ?? "none"] = l; }
                    l.Add(t.a);
                    if (flip) { l.Add(t.c); l.Add(t.b); } else { l.Add(t.b); l.Add(t.c); }
                }
            }
            tris.Clear();
        }

        // ---------------- МЕШИ ----------------
        // Кусок рельефа по выбранным столбцам и строкам сетки. visual — с нормалями, UV и подсеткой «вода»
        public Mesh BuildGrid(int[] cols, int[] rows, bool visual, out bool hasWater)
        {
            int nc = cols.Length, nr = rows.Length;
            var pos = new Vector3[nc * nr];
            var nor = visual ? new Vector3[nc * nr] : null;
            var uv = visual ? new Vector2[nc * nr] : null;
            for (int r = 0; r < nr; r++)
                for (int c = 0; c < nc; c++)
                {
                    int ci = cols[c], rj = rows[r], k = r * nc + c;
                    pos[k] = new Vector3(CX(ci), h[rj * Cols + ci], CZ(rj));
                    if (!visual) continue;
                    int i0 = Mathf.Max(ci - 1, 0), i1 = Mathf.Min(ci + 1, Cols - 1), j0 = Mathf.Max(rj - 1, 0), j1 = Mathf.Min(rj + 1, Rows - 1);
                    float dydx = (h[rj * Cols + i1] - h[rj * Cols + i0]) / ((i1 - i0) * Dx);
                    float dydz = -(h[j1 * Cols + ci] - h[j0 * Cols + ci]) / ((j1 - j0) * Dz);
                    nor[k] = new Vector3(-dydx, 1f, -dydz).normalized;
                    uv[k] = new Vector2(ci / (Cols - 1f), 1f - rj / (Rows - 1f));
                }
            var g = new List<int>(); var w = new List<int>();
            for (int r = 0; r < nr - 1; r++)
                for (int c = 0; c < nc - 1; c++)
                {
                    int a = r * nc + c, b = a + 1, d = (r + 1) * nc + c, cc = d + 1;
                    var l = visual && quadWater[rows[r] * (Cols - 1) + cols[c]] ? w : g;
                    l.Add(a); l.Add(b); l.Add(cc); l.Add(a); l.Add(cc); l.Add(d);
                }
            var mesh = new Mesh();
            if (pos.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = pos;
            if (visual) { mesh.normals = nor; mesh.uv = uv; }
            hasWater = visual && w.Count > 0;
            if (hasWater) { mesh.subMeshCount = 2; mesh.SetTriangles(g, 0); mesh.SetTriangles(w, 1); }
            else mesh.SetTriangles(g, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static int[] Range(int from, int to, int step)
        {
            var l = new List<int>();
            for (int i = from; i < to; i += step) l.Add(i);
            l.Add(to);
            return l.ToArray();
        }

        // Декор (треугольники с плоскими нормалями)
        public static Mesh BuildProps(List<Vector3> v)
        {
            int n = v.Count;
            var tri = new int[n]; var nor = new Vector3[n];
            for (int k = 0; k < n; k += 3)
            {
                var nn = Vector3.Cross(v[k + 1] - v[k], v[k + 2] - v[k]).normalized;
                nor[k] = nor[k + 1] = nor[k + 2] = nn;
                tri[k] = k; tri[k + 1] = k + 1; tri[k + 2] = k + 2;
            }
            var mesh = new Mesh();
            if (n > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v); mesh.SetNormals(nor); mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Плоские окрестности за краем карты, чтобы не было пустоты. Край карты ровный (EdgeHeight)
        public static Mesh BuildSkirt(float dist)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            float y = EdgeHeight, x0 = -HalfW, x1 = HalfW, z0 = -HalfH, z1 = HalfH;
            AddQuadUp(v, t, new Vector3(x0 - dist, y, z1), new Vector3(x1 + dist, y, z1), new Vector3(x1 + dist, y, z1 + dist), new Vector3(x0 - dist, y, z1 + dist));
            AddQuadUp(v, t, new Vector3(x0 - dist, y, z0 - dist), new Vector3(x1 + dist, y, z0 - dist), new Vector3(x1 + dist, y, z0), new Vector3(x0 - dist, y, z0));
            AddQuadUp(v, t, new Vector3(x0 - dist, y, z0), new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x0 - dist, y, z1));
            AddQuadUp(v, t, new Vector3(x1, y, z0), new Vector3(x1 + dist, y, z0), new Vector3(x1 + dist, y, z1), new Vector3(x1, y, z1));
            var m = new Mesh();
            m.SetVertices(v); m.SetTriangles(t, 0);
            var nor = new Vector3[v.Count]; for (int i = 0; i < nor.Length; i++) nor[i] = Vector3.up;
            m.SetNormals(nor); m.RecalculateBounds();
            return m;
        }

        static void AddQuadUp(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            if (Vector3.Cross(b - a, c - a).y < 0f) { var tmp = b; b = d; d = tmp; }
            int s = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);
        }
    }
}
