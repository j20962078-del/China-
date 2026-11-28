using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChinaFishing
{
    [Serializable] public class StandData { public int slot; public float x, y, z, yaw; }
    [Serializable] class StandList { public List<StandData> items = new List<StandData>(); }

    // Подставки для удочек: ставятся в мире, сохраняются вместе с координатой-сектором, остаются после выхода из игры.
    // Версия 1: подставка + удочка-макет, сохранение, «Взять», «Вести» к точке. Поклёвки на подставке пока НЕ симулируются.
    public class RodStands : MonoBehaviour
    {
        public static RodStands I;
        public const int Max = 3;
        const string Key = "stands_v1";

        readonly List<StandData> data = new List<StandData>();
        readonly Dictionary<int, GameObject> objs = new Dictionary<int, GameObject>();
        public int guideSlot = 0;            // к какой подставке ведёт стрелка (0 — нет)

        public IList<StandData> Items { get { return data; } }
        public static string NameOf(int slot) { return "Удочка " + slot; }

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

        public StandData Nearest(Vector3 p, float maxDist)
        {
            StandData best = null; float bd = maxDist;
            foreach (var s in data) { float d = Vector3.Distance(p, PosOf(s)); if (d <= bd) { bd = d; best = s; } }
            return best;
        }

        public bool CanPlace { get { return data.Count < Max; } }

        public StandData Place(Vector3 playerPos, float yaw)
        {
            if (!CanPlace) return null;
            int slot = 1; while (Get(slot) != null) slot++;
            Vector3 p = playerPos + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 0.9f;
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out hit, 6f)) p = hit.point; else p.y = playerPos.y;
            var s = new StandData { slot = slot, x = p.x, y = p.y, z = p.z, yaw = yaw };
            data.Add(s); Spawn(s); Save();
            return s;
        }

        public void Take(StandData s)
        {
            GameObject o;
            if (objs.TryGetValue(s.slot, out o) && o != null) Destroy(o);
            objs.Remove(s.slot); data.Remove(s);
            if (guideSlot == s.slot) guideSlot = 0;
            Save();
        }

        void Spawn(StandData s)
        {
            var root = new GameObject("Подставка " + s.slot);
            root.transform.position = new Vector3(s.x, s.y, s.z);
            root.transform.rotation = Quaternion.Euler(0, s.yaw, 0);
            var wood = Mats.Lit(new Color(0.22f, 0.15f, 0.09f), 0.1f);
            var rodC = Mats.Lit(new Color(0.07f, 0.07f, 0.08f), 0.5f);
            Part(root.transform, PrimitiveType.Cylinder, new Vector3(0, 0.30f, 0), new Vector3(0.04f, 0.30f, 0.04f), Vector3.zero, wood);      // стойка
            Part(root.transform, PrimitiveType.Cube, new Vector3(0, 0.62f, 0), new Vector3(0.16f, 0.03f, 0.03f), Vector3.zero, wood);          // вилка
            Part(root.transform, PrimitiveType.Cylinder, new Vector3(0, 0.80f, 0.85f), new Vector3(0.012f, 1.2f, 0.012f), new Vector3(78, 0, 0), rodC); // удилище-макет, кончик над водой
            objs[s.slot] = root;
        }

        static void Part(Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            var c = g.GetComponent<Collider>(); if (c != null) Destroy(c);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localEulerAngles = euler;
            var r = g.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = m;
        }

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
