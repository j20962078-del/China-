using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ChinaFishing
{
    // Общие детали интерфейса зданий: круглые картинки (рисуются кодом), подписи, кнопки, горизонтальная прокрутка, верхняя панель
    public static class UIParts
    {
        public static readonly Color Silver = new Color(0.80f, 0.85f, 0.90f);
        public static readonly Color GoldC = new Color(1f, 0.78f, 0.20f);
        static Sprite circle, runner;

        public static Sprite Circle { get { if (circle == null) circle = MakeCircle(); return circle; } }
        public static Sprite Runner { get { if (runner == null) runner = MakeRunner(); return runner; } }

        static Sprite MakeCircle()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = (n - 1) * 0.5f, r = n * 0.5f - 1.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f)));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        static void Dot(Color[] p, int n, float cx, float cy, float r, Color c)
        {
            for (int y = Mathf.Max(0, (int)(cy - r - 1)); y < Mathf.Min(n, (int)(cy + r + 2)); y++)
                for (int x = Mathf.Max(0, (int)(cx - r - 1)); x < Mathf.Min(n, (int)(cx + r + 2)); x++)
                {
                    float dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r * r) p[y * n + x] = c;
                }
        }

        static void Seg(Color[] p, int n, float x0, float y0, float x1, float y1, float r, Color c)
        {
            int steps = (int)(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2f) + 1;
            for (int i = 0; i <= steps; i++) { float t = i / (float)steps; Dot(p, n, Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), r, c); }
        }

        // Бегущий человечек (иконка на маркере здания)
        static Sprite MakeRunner()
        {
            const int n = 128;
            var px = new Color[n * n];
            var clear = new Color(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            var col = new Color(0.16f, 0.28f, 0.38f, 1f);
            Dot(px, n, 78, 108, 11, col);                       // голова
            Seg(px, n, 70, 92, 58, 62, 6.5f, col);              // корпус
            Seg(px, n, 68, 86, 90, 80, 5f, col);                // рука вперёд
            Seg(px, n, 90, 80, 98, 92, 5f, col);
            Seg(px, n, 68, 86, 48, 76, 5f, col);                // рука назад
            Seg(px, n, 48, 76, 38, 62, 5f, col);
            Seg(px, n, 58, 62, 78, 48, 6f, col);                // нога вперёд
            Seg(px, n, 78, 48, 74, 22, 6f, col);
            Seg(px, n, 58, 62, 40, 46, 6f, col);                // нога назад
            Seg(px, n, 40, 46, 22, 40, 6f, col);
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Image Disc(Transform p, Color c, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var r = UIKit.R("Круг", p);
            var i = r.gameObject.AddComponent<Image>();
            i.sprite = Circle; i.color = c; i.raycastTarget = false;
            UIKit.At(r, anchor, pos, size);
            return i;
        }

        public static Text Label(Transform p, string s, int size, Vector2 anchor, Vector2 pos, Vector2 sz,
            TextAnchor a = TextAnchor.MiddleLeft, Color? col = null, FontStyle st = FontStyle.Normal)
        {
            var t = UIKit.Txt(p, s, size, col ?? Color.white, a, st);
            UIKit.At(t.rectTransform, anchor, pos, sz);
            return t;
        }

        public static Button Btn(Transform p, string txt, Color c, Vector2 anchor, Vector2 pos, Vector2 sz, UnityAction act, int fs = 32)
        {
            var b = UIKit.Btn(p, txt, c, act, fs);
            UIKit.At(b.GetComponent<RectTransform>(), anchor, pos, sz);
            return b;
        }

        // Монета + число. Возвращает текст со значением; контейнер ставится по anchor/pos
        public static Text Price(Transform p, Color coin, string value, Vector2 anchor, Vector2 pos, float width, int fs = 28)
        {
            var box = UIKit.R("Цена", p);
            UIKit.At(box, anchor, pos, new Vector2(width, 46));
            Disc(box, coin, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(34, 34));
            return Label(box, value, fs, new Vector2(0, 0.5f), new Vector2(42, 0), new Vector2(width - 42, 46), TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
        }

        // Горизонтальный список с прокруткой (вкладки, фильтры). Кнопки добавляются в .content
        public static ScrollRect HScroll(Transform parent, float height, float spacing)
        {
            var root = UIKit.R("Лента", parent);
            var bg = root.gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0.001f);
            root.gameObject.AddComponent<RectMask2D>();
            var content = UIKit.R("Содержимое", root);
            content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 0.5f);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            var h = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            h.childControlWidth = true; h.childControlHeight = true; h.padding = new RectOffset(4, 4, 4, 4);
            var f = content.gameObject.AddComponent<ContentSizeFitter>(); f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = root.gameObject.AddComponent<ScrollRect>();
            sr.content = content; sr.horizontal = true; sr.vertical = false; sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 40f; sr.viewport = root;
            return sr;
        }

        public static Button TabBtn(Transform parent, string text, float width, UnityAction click, int fs = 30)
        {
            var b = UIKit.Btn(parent, text, UIKit.Panel2, click, fs);
            var le = b.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = width; le.preferredHeight = 70;
            return b;
        }

        // Убирает дочерние объекты сразу (Destroy сам работает до конца кадра, и раскладка успела бы посчитать «призраков»)
        public static void ClearNow(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                Object.Destroy(c);
            }
        }

        public static float TextWidth(string s, float perChar, float pad) { return s.Length * perChar + pad; }
    }

    // Верхняя панель: аватар, имя, уровень и опыт, серебро, золото, «+» и закрытие
    public class TopBar
    {
        Text level, silver, gold;
        Image xpFill;

        public static TopBar Build(Transform box, UnityAction onClose, UnityAction onPlus)
        {
            var b = new TopBar();
            var top = UIKit.R("Верхняя панель", box);
            top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(0.5f, 1);
            top.offsetMin = new Vector2(0, -120); top.offsetMax = Vector2.zero;

            var av = UIParts.Disc(top, UIKit.Blue, new Vector2(0, 1), new Vector2(24, -14), new Vector2(92, 92));
            UIParts.Label(av.transform, "Р", 52, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92, 92), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UIParts.Label(top, "Рыбак", 36, new Vector2(0, 1), new Vector2(130, -10), new Vector2(520, 44), TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            b.level = UIParts.Label(top, "", 26, new Vector2(0, 1), new Vector2(130, -54), new Vector2(520, 34), TextAnchor.MiddleLeft, UIKit.Dim);
            var xpBg = UIKit.Img(top, new Color(0, 0, 0, 0.5f));
            UIKit.At(xpBg.rectTransform, new Vector2(0, 1), new Vector2(130, -96), new Vector2(380, 12));
            b.xpFill = UIKit.Img(xpBg.transform, UIKit.Green);
            b.xpFill.rectTransform.anchorMin = Vector2.zero; b.xpFill.rectTransform.anchorMax = new Vector2(0.5f, 1);
            b.xpFill.rectTransform.offsetMin = b.xpFill.rectTransform.offsetMax = Vector2.zero;

            UIParts.Btn(top, "X", UIKit.Red, new Vector2(1, 1), new Vector2(-24, -14), new Vector2(92, 92), onClose, 44);
            UIParts.Btn(top, "+", UIKit.Green, new Vector2(1, 1), new Vector2(-130, -14), new Vector2(92, 92), onPlus, 54);

            var gp = UIKit.Img(top, new Color(0, 0, 0, 0.35f));
            UIKit.At(gp.rectTransform, new Vector2(1, 1), new Vector2(-236, -25), new Vector2(240, 70));
            b.gold = UIParts.Price(gp.transform, UIParts.GoldC, "0", new Vector2(0, 0.5f), new Vector2(14, 0), 215);
            var sp = UIKit.Img(top, new Color(0, 0, 0, 0.35f));
            UIKit.At(sp.rectTransform, new Vector2(1, 1), new Vector2(-492, -25), new Vector2(300, 70));
            b.silver = UIParts.Price(sp.transform, UIParts.Silver, "0", new Vector2(0, 0.5f), new Vector2(14, 0), 275);
            return b;
        }

        public void Refresh()
        {
            var s = Game.S; if (s == null) return;
            int need = s.level >= 99 ? 1 : GameData.XpForLevel(s.level);
            level.text = "Ур. " + s.level + "   опыт " + s.xp + "/" + (s.level >= 99 ? s.xp : need);
            xpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(s.level >= 99 ? 1f : (float)s.xp / Mathf.Max(1, need)), 1);
            silver.text = CurrencyManager.Silver.ToString("N0");
            gold.text = CurrencyManager.Gold.ToString("N0");
        }
    }
}
