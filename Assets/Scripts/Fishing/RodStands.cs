using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChinaFishing
{
    [Serializable]
    public class StandData
    {
        public int slot;
        public float x, y, z, yaw;
        public bool cast;                 // в воде ли снасть
        public float tx, tz;              // точка поплавка
        public string fishId = "";        // какая рыба придёт ("" — пока никого)
        public float w, l;
        public long biteTicks;            // когда клюнет (UTC-тики)
        public int st;                    // 0 ждёт, 1 клюёт
        public long biteStart;            // когда началась поклёвка
    }
    [Serializable] class StandList { public List<StandData> items = new List<StandData>(); }

    // Подставки для удочек. Удочку, заброшенную из рук, можно поставить на подставку (до 3 штук):
    // она сама ловит, сигналит поклёвкой (иконка слота, звук, вибрация), игрок ходит где хочет и возвращается,
    // чтобы взять удочку и подсечь. Состояние сохраняется (время по UTC).
    public class RodStands : MonoBehaviour
    {
        public static RodStands I;
        public const int Max = 3;
        public const double BiteWindowSec = 60.0;   // сколько рыба ждёт на крючке, пока вы бежите к удочке
        const string Key = "stands_v2";

        class View { public GameObject root; public Transform rod, bob; public LineRenderer line; public float seed; }

        readonly List<StandData> data = new List<StandData>();
        readonly Dictionary<int, View> views = new Dictionary<int, View>();
        public int guideSlot = 0;
        float checkT;

        public IList<StandData> Items { get { return data; } }
        public static string NameOf(int slot) { return "Удочка " + slot; }
        public static long Now { get { return DateTime.UtcNow.Ticks; } }

        public static RodStands Create()
        {
            if (I != null) return I;
            var g = new GameObject("Подставки");
            I = g.AddComponent<RodStands>();
            I.Load();
            return I;
        }

        public StandData Get(int slot) { foreach (var s in data) if (s.slot == slot) return s; return null; }
        public Vector3 PosOf(StandData s) { return new Vector3(s.x, s.y, s.z); }
        public bool CanPlace { get { return data.Count < Max; } }
        public bool IsBiting(StandData s) { return s != null && s.cast && s.st == 1; }

        public StandData Nearest(Vector3 p, float maxDist)
        {
            StandData best = null; float bd = maxDist;
            foreach (var s in data) { float d = Vector3.Distance(p, PosOf(s)); if (d <= bd) { bd = d; best = s; } }
            return best;
        }

        // ---------- поставить заброшенную удочку из рук ----------
        public StandData PlaceFromHand()
        {
            var fc = FishingController.I; var gm = GameManager.I;
            if (fc == null || gm == null || !CanPlace || fc.state != FState.Ожидание) return null;
            StandCast c;
            if (!fc.DetachToStand(out c)) return null;

            var pp = gm.player.transform.position;
            float yaw = Mathf.Atan2(c.x - pp.x, c.z - pp.z) * Mathf.Rad2Deg;   // удилище смотрит на поплавок
            Vector3 p = pp + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 0.9f;
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out hit, 6f)) p = hit.point; else p.y = pp.y;

            int slot = 1; while (Get(slot) != null) slot++;
            var s = new StandData { slot = slot, x = p.x, y = p.y, z = p.z, yaw = yaw, cast = true, tx = c.x, tz = c.z };
            ApplyRoll(s, c);
            data.Add(s); Spawn(s); Save();
            return s;
        }

        void ApplyRoll(StandData s, StandCast c)
        {
            s.fishId = c.fishId ?? ""; s.w = c.weight; s.l = c.length;
            s.biteTicks = Now + (long)(c.waitLeft * 10000000.0);
            s.st = 0; s.biteStart = 0;
        }

        // ---------- взять удочку с подставки ----------
        public void TakeToHand(StandData s)
        {
            var fc = FishingController.I;
            if (fc == null || fc.state != FState.Нет) return;
            bool biting = IsBiting(s);
            if (s.cast)
            {
                var c = new StandCast { x = s.tx, z = s.tz, fishId = s.fishId, weight = s.w, length = s.l };
                c.waitLeft = Mathf.Max(1f, (float)((s.biteTicks - Now) / 10000000.0));
                Remove(s);
                fc.TakeFromStand(c, biting);
            }
            else Remove(s);
        }

        void Remove(StandData s)
        {
            View v;
            if (views.TryGetValue(s.slot, out v) && v.root != null) Destroy(v.root);
            views.Remove(s.slot); data.Remove(s);
            if (guideSlot == s.slot) guideSlot = 0;
            Save();
        }

        // ---------- сборка модели ----------
        void Spawn(StandData s)
        {
            var root = new GameObject("Подставка " + s.slot);
            root.transform.position = new Vector3(s.x, s.y, s.z);
            root.transform.rotation = Quaternion.Euler(0, s.yaw, 0);
            var wood = Mats.Lit(new Color(0.22f, 0.15f, 0.09f), 0.1f);
            var rodC = Mats.Lit(new Color(0.07f, 0.07f, 0.08f), 0.5f);
            Part(root.transform, PrimitiveType.Cylinder, new Vector3(0, 0.30f, 0), new Vector3(0.04f, 0.30f, 0.04f), Vector3.zero, wood);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0, 0.62f, 0), new Vector3(0.16f, 0.03f, 0.03f), Vector3.zero, wood);
            var rod = Part(root.transform, PrimitiveType.Cylinder, new Vector3(0, 0.80f, 0.85f), new Vector3(0.012f, 1.2f, 0.012f), new Vector3(78, 0, 0), rodC);
            var v = new View { root = root, rod = rod, seed = UnityEngine.Random.value * 50f };

            if (s.cast)
            {
                var b = new GameObject("Поплавок подставки").transform;
                WorldBuilder.Prim(PrimitiveType.Sphere, b, Vector3.zero, new Vector3(0.09f, 0.12f, 0.09f), Mats.Lit(245, 245, 245, 0.5f));
                WorldBuilder.Prim(PrimitiveType.Sphere, b, new Vector3(0, 0.07f, 0), new Vector3(0.085f, 0.09f, 0.085f), Mats.Lit(230, 50, 40, 0.5f));
                b.SetParent(root.transform, true);
                b.position = new Vector3(s.tx, WorldBuilder.WaterLevel + 0.03f, s.tz);
                v.bob = b;
                var lr = new GameObject("Леска подставки").AddComponent<LineRenderer>();
                lr.transform.SetParent(root.transform, false);
                lr.positionCount = 12; lr.startWidth = 0.012f; lr.endWidth = 0.008f; lr.numCapVertices = 2;
                lr.material = Mats.Lit(240, 240, 240, 0.2f);
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.useWorldSpace = true;
                v.line = lr;
            }
            views[s.slot] = v;
        }

        static Transform Part(Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            var c = g.GetComponent<Collider>(); if (c != null) Destroy(c);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localEulerAngles = euler;
            var r = g.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = m;
            return g.transform;
        }

        // ---------- симуляция поклёвок ----------
        void Update()
        {
            AnimateViews();
            checkT -= Time.unscaledDeltaTime;
            if (checkT > 0f) return;
            checkT = 0.5f;
            var fc = FishingController.I; if (fc == null) return;
            long now = Now;
            bool dirty = false;
            foreach (var s in new List<StandData>(data))
            {
                if (!s.cast) continue;
                if (s.st == 0 && now >= s.biteTicks)
                {
                    bool expired = (now - s.biteTicks) / 10000000.0 > BiteWindowSec;   // пока игры не было — поклёвка прошла
                    if (string.IsNullOrEmpty(s.fishId) || expired) { Reroll(s, fc); dirty = true; }
                    else
                    {
                        s.st = 1; s.biteStart = now; dirty = true;
                        OnBite(s);
                    }
                }
                else if (s.st == 1 && (now - s.biteStart) / 10000000.0 > BiteWindowSec)
                {
                    if (GameUI.I != null) GameUI.I.Toast(NameOf(s.slot) + ": рыба ушла, вы не успели.");
                    Reroll(s, fc); dirty = true;
                }
            }
            if (dirty) Save();
        }

        void Reroll(StandData s, FishingController fc)
        {
            var c = fc.RollForStand(s.tx, s.tz);
            ApplyRoll(s, c);
        }

        void OnBite(StandData s)
        {
            string msg = "Поклёвка на удочке " + s.slot + "!";
            if (GameUI.I != null) GameUI.I.Toast(msg);
            if (HudRef.I != null) HudRef.I.Post("Система", msg + " Вернитесь и подсеките.");
            AudioManager.I?.Bite();
            Handheld.Vibrate();
            if (guideSlot == 0) guideSlot = s.slot;   // стрелка сама покажет путь к клюющей удочке
        }

        void AnimateViews()
        {
            long now = Now;
            foreach (var s in data)
            {
                View v;
                if (!views.TryGetValue(s.slot, out v) || v.root == null) continue;
                float t = Time.time;
                float dip = 0f, bobDip = 0f;
                if (s.cast)
                {
                    double toBite = (s.biteTicks - now) / 10000000.0;
                    if (s.st == 1) { dip = 8f + Mathf.Abs(Mathf.Sin(t * 14f)) * 7f; bobDip = -0.09f - Mathf.Abs(Mathf.Sin(t * 11f)) * 0.04f; }
                    else if (toBite < 10.0 && !string.IsNullOrEmpty(s.fishId))
                    {
                        // перед поклёвкой — лёгкие клевки, всё чаще
                        float n = Mathf.PerlinNoise(t * 2.2f, v.seed);
                        if (n > 0.72f) { dip = (n - 0.72f) * 28f; bobDip = -(n - 0.72f) * 0.25f; }
                    }
                }
                float sway = Mathf.Sin(t * 1.3f + v.seed) * 0.6f;   // ветер: рыхлое покачивание
                v.rod.localEulerAngles = new Vector3(78f + dip, sway, 0);
                if (v.bob != null)
                    v.bob.position = new Vector3(s.tx, WorldBuilder.WaterLevel + 0.03f + Mathf.Sin(t * 1.7f + v.seed) * 0.012f + bobDip, s.tz);
                if (v.line != null && v.bob != null)
                {
                    Vector3 a = v.rod.TransformPoint(new Vector3(0, 1f, 0)), b = v.bob.position + Vector3.up * 0.1f;
                    int cnt = v.line.positionCount;
                    for (int i = 0; i < cnt; i++)
                    {
                        float k = i / (float)(cnt - 1);
                        var p = Vector3.Lerp(a, b, k); p.y -= Mathf.Sin(k * Mathf.PI) * 0.4f;
                        v.line.SetPosition(i, p);
                    }
                }
            }
        }

        // ---------- сохранение ----------
        void Save() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(new StandList { items = data })); PlayerPrefs.Save(); }

        void Load()
        {
            try
            {
                var js = PlayerPrefs.GetString(Key, "");
                if (string.IsNullOrEmpty(js)) return;
                var l = JsonUtility.FromJson<StandList>(js);
                if (l == null || l.items == null) return;
                foreach (var s in l.items) { if (s.slot < 1 || s.slot > Max || Get(s.slot) != null) continue; data.Add(s); Spawn(s); }
            }
            catch (Exception e) { Debug.LogWarning("Подставки: не удалось прочитать сохранение: " + e.Message); }
        }
    }
}
