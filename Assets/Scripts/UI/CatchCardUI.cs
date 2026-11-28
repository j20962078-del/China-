using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ChinaFishing
{
    // Экран улова: тёмный фон, имя и вес слева сверху, большая 3D-рыба, линейка в см, бейдж XP, кнопки «В садок» / «Отпустить».
    // Вешается на панель resR в GameUI; при выключении панели сам гасит камеру и удаляет рыбу.
    public class CatchCardUI : MonoBehaviour
    {
        const float RULER_W = 1200f, FISH_H = 520f;
        const int RT_W = 1600, RT_H = 693;
        static readonly Vector3 STAGE_POS = new Vector3(2000f, 2000f, 2000f);
        static readonly Color TEAL = new Color(0.25f, 0.85f, 0.80f);
        static readonly Color XPC = new Color(0.75f, 0.82f, 0.28f, 1f);
        static readonly CultureInfo RU = new CultureInfo("ru-RU");

        Font font;
        RectTransform root, rulerRoot;
        CanvasGroup group;
        Text nameTxt, weightTxt, infoTxt, xpTxt;
        Image bookmark;
        RawImage fishView;
        RenderTexture rt;
        Camera cam;
        Transform stage, holder;
        GameObject fishGO;

        public static CatchCardUI Create(RectTransform host, UnityAction onKeep, UnityAction onRelease)
        {
            var c = host.gameObject.AddComponent<CatchCardUI>();
            c.Build(host, onKeep, onRelease);
            return c;
        }

        // ---------------------------------------------------------- BUILD
        void Build(RectTransform host, UnityAction onKeep, UnityAction onRelease)
        {
            root = host;
            font = UIKit.Font != null ? UIKit.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            group = host.gameObject.GetComponent<CanvasGroup>();
            if (group == null) group = host.gameObject.AddComponent<CanvasGroup>();

            var bg = Img(root, "Bg", Color.white, RadialSprite());
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);

            bookmark = Img(root, "Bookmark", Color.white);
            Place(bookmark.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -62), new Vector2(18, 34));

            nameTxt = Txt(root, "Name", 44, Color.white, TextAnchor.UpperLeft, FontStyle.Normal);
            Place(nameTxt.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, -52), new Vector2(1000, 56));
            weightTxt = Txt(root, "Weight", 30, new Color(1, 1, 1, 0.9f), TextAnchor.UpperLeft, FontStyle.Normal);
            Place(weightTxt.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(98, -106), new Vector2(1000, 40));
            infoTxt = Txt(root, "Info", 24, new Color(1, 1, 1, 0.6f), TextAnchor.UpperLeft, FontStyle.Normal);
            infoTxt.supportRichText = true;
            Place(infoTxt.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(98, -146), new Vector2(1400, 34));

            fishView = new GameObject("Fish", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            fishView.transform.SetParent(root, false);
            fishView.raycastTarget = false;
            Place(fishView.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(RULER_W, FISH_H));

            rulerRoot = new GameObject("Ruler", typeof(RectTransform)).GetComponent<RectTransform>();
            rulerRoot.SetParent(root, false);
            Place(rulerRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(-RULER_W / 2f, -200), new Vector2(RULER_W, 0));

            var box = Img(root, "XpBox", new Color(0.28f, 0.28f, 0.28f, 0.85f));
            Place(box.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-60, 10), new Vector2(150, 110));
            var cfill = Img(box.transform, "CircleBg", new Color(0.08f, 0.08f, 0.08f, 1f), CircleSprite(false));
            Place(cfill.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66, 66));
            var ring = Img(cfill.transform, "Ring", XPC, CircleSprite(true));
            Stretch(ring.rectTransform);
            var xl = Txt(cfill.transform, "XpLabel", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); xl.text = "XP";
            Stretch(xl.rectTransform);
            xpTxt = Txt(box.transform, "XpNum", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal);
            Place(xpTxt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(150, 40));
            var cap = Txt(box.transform, "XpCap", 18, XPC, TextAnchor.MiddleCenter, FontStyle.Normal); cap.text = "Очков\nопыта";
            Place(cap.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(150, 46));

            MakeButton(root, "В садок", new Vector2(-135, 72), new Vector2(240, 64), onKeep);
            MakeButton(root, "Отпустить", new Vector2(135, 72), new Vector2(240, 64), onRelease);

            BuildStage();
        }

        void BuildStage()
        {
            stage = new GameObject("CatchStage").transform;
            stage.position = STAGE_POS;
            rt = new RenderTexture(RT_W, RT_H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            fishView.texture = rt;

            var camGO = new GameObject("CatchCam");
            camGO.transform.SetParent(stage, false);
            camGO.transform.localPosition = new Vector3(-3f, 0f, 0f);
            camGO.transform.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up); // смотрит в +X, значит +Z = влево
            cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 12f;
            cam.allowHDR = false; cam.depth = -30;
            cam.targetTexture = rt;
            cam.enabled = false;

            holder = new GameObject("FishHolder").transform;
            holder.SetParent(stage, false);

            MakeLight("Key", new Vector3(-2.2f, 1.8f, 0.8f), new Color(1f, 0.96f, 0.9f), 1.1f);
            MakeLight("Rim", new Vector3(1.0f, 0.9f, -1.8f), new Color(0.6f, 0.75f, 1f), 0.5f);
            MakeLight("Fill", new Vector3(-2.0f, -0.8f, -0.8f), new Color(1f, 0.85f, 0.7f), 0.3f);
            stage.gameObject.SetActive(false);
        }

        void MakeLight(string n, Vector3 localPos, Color c, float intensity)
        {
            var go = new GameObject(n);
            go.transform.SetParent(stage, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = 10f; l.shadows = LightShadows.None;
        }

        // ---------------------------------------------------------- SHOW
        public void Present(CatchResult r)
        {
            Color rc = RarityColor(r.fish.rarity);
            bookmark.color = rc;
            nameTxt.text = r.fish.name;
            weightTxt.text = r.weight.ToString("0.00", RU) + " кг";

            string info = "<color=#" + ColorUtility.ToHtmlStringRGB(rc) + ">" + r.fish.rarity + "</color>";
            info += "     Цена продажи: " + r.value + " сер.";
            if (r.record) info += "     <color=#FFB020>РЕКОРД</color>";
            if (r.firstTime) info += "     <color=#FFB020>НОВЫЙ ВИД</color>";
            if (r.levelUp) info += "     <color=#" + ColorUtility.ToHtmlStringRGB(XPC) + ">НОВЫЙ УРОВЕНЬ " + Game.S.level + "! Награда выдана</color>";
            infoTxt.text = info;
            xpTxt.text = "+" + r.xp;

            float maxCm = NiceMax(r.length);
            BuildRuler(maxCm, r.length);
            SpawnFish(r, maxCm);
            SetStageOn(true);
        }

        void SpawnFish(CatchResult r, float maxCm)
        {
            if (fishGO != null) Destroy(fishGO);
            holder.localRotation = Quaternion.identity;
            holder.localPosition = Vector3.zero;

            float lenM = Mathf.Max(0.05f, r.length / 100f);
            fishGO = FishModels.Create(r.fish, lenM);
            if (fishGO == null) fishGO = FishModel.Build(r.fish, lenM);
            if (fishGO == null) return;
            fishGO.transform.SetParent(holder, false);
            fishGO.transform.localPosition = Vector3.zero;
            fishGO.transform.localRotation = Quaternion.identity;

            var rs = fishGO.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds);
            float len = b.size.z > 0.0001f ? b.size.z : Mathf.Max(b.size.x, b.size.y, b.size.z);
            fishGO.transform.localScale *= lenM / len;     // точный реальный размер

            b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds);
            fishGO.transform.position += holder.position - b.center;   // центр рыбы в центр holder

            float halfW = maxCm / 200f;
            holder.localPosition = new Vector3(0, 0, halfW - b.size.z / 2f);   // нос ровно на отметке 0 см

            float aspect = (float)RT_W / RT_H;
            cam.aspect = aspect;
            cam.orthographicSize = halfW / aspect;
        }

        void SetStageOn(bool on)
        {
            if (stage != null) stage.gameObject.SetActive(on);
            if (cam != null) cam.enabled = on;
        }

        void OnEnable()
        {
            if (fishGO != null) SetStageOn(true);
            if (group != null) { StopAllCoroutines(); StartCoroutine(FadeIn()); }
        }

        void OnDisable()
        {
            SetStageOn(false);
            if (fishGO != null) { Destroy(fishGO); fishGO = null; }
        }

        IEnumerator FadeIn()
        {
            float t = 0; group.alpha = 0;
            while (t < 0.3f) { t += Time.unscaledDeltaTime; group.alpha = Mathf.Clamp01(t / 0.3f); yield return null; }
            group.alpha = 1;
        }

        void Update()
        {
            if (holder == null || fishGO == null) return;
            float t = Time.unscaledTime;
            holder.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 2.5f, Mathf.Sin(t * 0.6f) * 7f, Mathf.Sin(t * 0.7f) * 1.5f);
        }

        void OnDestroy()
        {
            if (rt != null) { rt.Release(); Destroy(rt); }
            if (stage != null) Destroy(stage.gameObject);
        }

        // ---------------------------------------------------------- RULER
        static float NiceMax(float sizeCm)
        {
            float step = sizeCm <= 50 ? 5 : sizeCm <= 150 ? 10 : 20;
            return Mathf.Max(step * 2, Mathf.Ceil(sizeCm * 1.12f / step) * step);
        }

        void BuildRuler(float maxCm, float sizeCm)
        {
            for (int i = rulerRoot.childCount - 1; i >= 0; i--) Destroy(rulerRoot.GetChild(i).gameObject);
            int minor = maxCm <= 60 ? 1 : maxCm <= 150 ? 2 : 5;
            int label = maxCm <= 60 ? 5 : maxCm <= 150 ? 10 : 20;
            for (int cm = 0; cm <= (int)maxCm; cm += minor)
            {
                float x = cm / maxCm * RULER_W;
                bool major = cm % label == 0;
                var tick = Img(rulerRoot, "t" + cm, new Color(1, 1, 1, major ? 0.95f : 0.5f));
                Place(tick.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0), new Vector2(x, 0), new Vector2(major ? 3 : 2, major ? 34 : 15));
                if (major && cm > 0)
                {
                    var t = Txt(rulerRoot, "l" + cm, 26, new Color(1, 1, 1, 0.85f), TextAnchor.UpperCenter, FontStyle.Normal); t.text = cm.ToString();
                    Place(t.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 1), new Vector2(x, -8), new Vector2(90, 34));
                }
            }
            var unit = Txt(rulerRoot, "unit", 26, new Color(1, 1, 1, 0.55f), TextAnchor.UpperCenter, FontStyle.Normal); unit.text = "см";
            Place(unit.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 1), new Vector2(-40, -8), new Vector2(80, 34));

            float sx = Mathf.Clamp01(sizeCm / maxCm) * RULER_W;
            var line = Img(rulerRoot, "sizeLine", TEAL);
            Place(line.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0), new Vector2(sx, 0), new Vector2(4, 56));
            var st = Txt(rulerRoot, "sizeTxt", 28, TEAL, TextAnchor.LowerCenter, FontStyle.Bold); st.text = sizeCm.ToString("0.#", RU) + " см";
            Place(st.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0), new Vector2(sx, 62), new Vector2(160, 36));
        }

        // ---------------------------------------------------------- HELPERS
        static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Необычная: return new Color(0.38f, 0.85f, 0.45f);
                case Rarity.Редкая: return new Color(0.35f, 0.65f, 1f);
                case Rarity.Легендарная: return new Color(1f, 0.65f, 0.15f);
                default: return new Color(0.75f, 0.78f, 0.8f);
            }
        }

        Image Img(Transform parent, string n, Color c, Sprite s = null)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var im = go.GetComponent<Image>();
            im.sprite = s; im.color = c; im.raycastTarget = false;
            return im;
        }

        Text Txt(Transform parent, string n, int size, Color c, TextAnchor a, FontStyle fs)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.color = c; t.alignment = a; t.fontStyle = fs;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

        static void Place(RectTransform r, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = amin; r.anchorMax = amax; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
        }

        void MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, UnityAction onClick)
        {
            var fill = Img(parent, "Btn_" + label, Color.white);
            fill.raycastTarget = true;
            Place(fill.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), pos, size);
            const float th = 2f;
            Border(fill.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, th));
            Border(fill.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, th));
            Border(fill.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(th, 0));
            Border(fill.transform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(th, 0));
            var t = Txt(fill.transform, "Label", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal); t.text = label;
            Stretch(t.rectTransform);

            var b = fill.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            var cb = b.colors;
            cb.normalColor = new Color(1, 1, 1, 0f);
            cb.highlightedColor = new Color(1, 1, 1, 0.08f);
            cb.selectedColor = new Color(1, 1, 1, 0f);
            cb.pressedColor = new Color(1, 1, 1, 0.22f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(onClick);
        }

        void Border(Transform p, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 size)
        {
            var im = Img(p, "Border", Color.white);
            im.rectTransform.anchorMin = amin; im.rectTransform.anchorMax = amax; im.rectTransform.pivot = pivot;
            im.rectTransform.anchoredPosition = Vector2.zero; im.rectTransform.sizeDelta = size;
        }

        static Sprite RadialSprite()
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x / (float)(n - 1) - 0.5f, dy = y / (float)(n - 1) - 0.52f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx * 1.2f + dy * dy * 2.2f) * 1.5f);
                    d = d * d * (3f - 2f * d);
                    float v = Mathf.Lerp(0.21f, 0.035f, d);
                    tex.SetPixel(x, y, new Color(v, v, v));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        static Sprite CircleSprite(bool ring)
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (n - 1) / 2f, ro = n / 2f - 1f, ri = ring ? ro - 7f : -1f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(ro - d + 0.5f);
                    if (ring) a *= Mathf.Clamp01(d - ri + 0.5f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
