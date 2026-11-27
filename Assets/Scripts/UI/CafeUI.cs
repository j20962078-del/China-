using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChinaFishing
{
    // Кафе: заказы на рыбу. Каждый заказ — вид рыбы, количество, минимальный вес, срок и награда в серебре.
    // Заказы хранятся в сохранении (SaveData.cafeOrders), срок идёт по реальному времени, в том числе пока игра закрыта.
    public class CafeUI
    {
        const int OrdersCount = 6;
        static readonly System.Random rng = new System.Random();

        UIManager ui; TopBar bar; RectTransform panel, body;
        ScrollRect gridScroll;
        readonly List<Text> timers = new List<Text>();
        readonly List<long> timerEnds = new List<long>();
        float tickT;

        public RectTransform Build(UIManager owner)
        {
            ui = owner;
            RectTransform b;
            panel = ui.MakeFrame("Кафе", out b, out bar);
            body = b;
            UIParts.Label(body, "Заказы на рыбу", 40, new Vector2(0, 1), new Vector2(8, 0), new Vector2(800, 60), TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            UIParts.Label(body, "Сдавайте рыбу из садка и получайте серебро. Цена выше рыночной.", 24, new Vector2(0, 1), new Vector2(8, -58), new Vector2(1400, 36), TextAnchor.MiddleLeft, UIKit.Dim);
            gridScroll = UIKit.Scroll(body, true, new Vector2(400, 470), 3);
            var gr = gridScroll.GetComponent<RectTransform>(); gr.name = "Заказы";
            gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one;
            gr.offsetMin = Vector2.zero; gr.offsetMax = new Vector2(0, -104);
            return panel;
        }

        // ---------------- ЗАКАЗЫ ----------------
        static string Location()
        {
            var w = WorldBuilder.I;
            if (w == null) return "оз. Лесное";
            if (w.MapId == KamMap.Id) return "оз. Камышевское";
            if (w.MapId == AmurMap.Id) return "р. Амур";
            return "оз. Лесное";
        }

        // Убирает просроченные и добивает список до OrdersCount. true — список изменился
        static bool EnsureOrders()
        {
            var l = Game.S.cafeOrders;
            bool changed = false;
            long now = DateTime.UtcNow.Ticks;
            if (l.RemoveAll(o => o.expireTicks <= now) > 0) changed = true;
            int guard = 0;
            while (l.Count < OrdersCount && guard++ < 20) { l.Add(NewOrder(l)); changed = true; }
            return changed;
        }

        static CafeOrderSave NewOrder(List<CafeOrderSave> existing)
        {
            // вид рыбы: взвешенно по частоте, без повторов, пока есть из чего выбирать
            FishDef f = null;
            for (int tries = 0; tries < 8; tries++)
            {
                float sum = 0f; foreach (var x in GameData.Fish) sum += Mathf.Max(0.5f, x.spawnWeight);
                float r = (float)rng.NextDouble() * sum;
                foreach (var x in GameData.Fish) { r -= Mathf.Max(0.5f, x.spawnWeight); if (r <= 0f) { f = x; break; } }
                if (f == null) f = GameData.Fish[0];
                bool dup = false; foreach (var e in existing) if (e.fishId == f.id) dup = true;
                if (!dup) break;
            }
            float minW = Mathf.Lerp(f.minW, f.maxW, 0.06f + (float)rng.NextDouble() * 0.25f);
            minW = Mathf.Max(f.minW, Mathf.Round(minW * 10f) / 10f);
            int count = f.rarity == Rarity.Легендарная ? 1 : 1 + rng.Next(5);
            int price = Mathf.Max(10, Mathf.RoundToInt(f.pricePerKg * minW * count * 1.5f / 5f) * 5);
            return new CafeOrderSave
            {
                fishId = f.id, count = count, minWeight = minW, price = price,
                expireTicks = DateTime.UtcNow.AddMinutes(180 + rng.Next(240)).Ticks
            };
        }

        // Сколько рыб из садка подходят под заказ
        static List<BagFish> Eligible(CafeOrderSave o)
        {
            var l = new List<BagFish>();
            foreach (var bf in Game.S.fishBag) if (bf.id == o.fishId && bf.weight >= o.minWeight) l.Add(bf);
            l.Sort((a, c) => a.weight.CompareTo(c.weight));    // сдаём самые лёгкие из подходящих
            return l;
        }

        // ---------------- ОБНОВЛЕНИЕ ----------------
        public void Refresh()
        {
            if (EnsureOrders()) Game.Save();
            bar.Refresh();
            FishPreview.SetAllActive(false);
            timers.Clear(); timerEnds.Clear();

            var content = gridScroll.content;
            float keepY = content.anchoredPosition.y;
            UIParts.ClearNow(content);
            Canvas.ForceUpdateCanvases();
            float cw = gridScroll.GetComponent<RectTransform>().rect.width; if (cw < 300f) cw = 1400f;
            int cols = Mathf.Clamp(Mathf.FloorToInt(cw / 400f), 2, 5);
            float gap = 16f, cellW = Mathf.Floor((cw - 20f - gap * (cols - 1)) / cols);
            var g = content.GetComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(cellW, 470); g.spacing = new Vector2(gap, gap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = cols; g.childAlignment = TextAnchor.UpperLeft;

            foreach (var o in Game.S.cafeOrders) BuildCard(content, o, cellW);

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float maxY = Mathf.Max(0f, content.rect.height - gridScroll.GetComponent<RectTransform>().rect.height);
            content.anchoredPosition = new Vector2(0, Mathf.Clamp(keepY, 0f, maxY));
            Tick(true);
        }

        public static string TimeText(long expireTicks)
        {
            long left = (expireTicks - DateTime.UtcNow.Ticks) / TimeSpan.TicksPerSecond;
            if (left < 0) left = 0;
            return (left / 3600) + "ч " + (left % 3600 / 60) + "м";
        }

        // Раз в секунду обновляет оставшееся время. Если заказ истёк — перестраивает список
        public void Tick(bool force = false)
        {
            tickT += Time.unscaledDeltaTime;
            if (!force && tickT < 1f) return;
            tickT = 0f;
            long now = DateTime.UtcNow.Ticks;
            for (int i = 0; i < timers.Count; i++)
            {
                if (timerEnds[i] <= now && !force) { Refresh(); return; }
                if (timers[i] != null) timers[i].text = "Осталось " + TimeText(timerEnds[i]);
            }
        }

        void BuildCard(Transform grid, CafeOrderSave o, float w)
        {
            var f = GameData.GetFish(o.fishId); if (f == null) return;
            var card = UIKit.Img(grid, UIKit.Panel2);
            Transform t = card.transform;
            float iw = Mathf.Min(w - 24f, 320f);
            var pv = UIKit.R("Просмотр", t); UIKit.At(pv, new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(iw, iw * 160f / 256f));
            var ri = pv.gameObject.AddComponent<RawImage>(); ri.raycastTarget = false;
            var prev = FishPreview.Get(f); prev.SetActive(true); ri.texture = prev.Texture;

            float y = -14f - iw * 160f / 256f - 6f;
            float lw = w - 24f;
            UIParts.Label(t, f.name, 32, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(lw, 42), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UIParts.Label(t, Location(), 24, new Vector2(0.5f, 1), new Vector2(0, y - 42), new Vector2(lw, 32), TextAnchor.MiddleCenter, UIKit.Dim);
            var tm = UIParts.Label(t, "", 24, new Vector2(0.5f, 1), new Vector2(0, y - 74), new Vector2(lw, 32), TextAnchor.MiddleCenter, UIKit.Accent);
            timers.Add(tm); timerEnds.Add(o.expireTicks);
            UIParts.Label(t, "Кол-во " + o.count + " Шт", 26, new Vector2(0.5f, 1), new Vector2(0, y - 108), new Vector2(lw, 34), TextAnchor.MiddleCenter, Color.white);
            UIParts.Label(t, "Мин. вес " + o.minWeight.ToString("0.0#") + " кг", 26, new Vector2(0.5f, 1), new Vector2(0, y - 142), new Vector2(lw, 34), TextAnchor.MiddleCenter, Color.white);
            UIParts.Price(t, UIParts.Silver, o.price.ToString("N0"), new Vector2(0.5f, 0), new Vector2(0, 84), 190, 30);

            int have = Eligible(o).Count;
            bool can = have >= o.count;
            var o_ = o;
            var b = UIParts.Btn(t, can ? "Сдать" : "В садке " + have + " из " + o.count, can ? UIKit.Green : UIKit.Gray,
                new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(lw, 62), () => Deliver(o_), 28);
            b.interactable = can;
        }

        void Deliver(CafeOrderSave o)
        {
            var el = Eligible(o);
            if (el.Count < o.count) { ui.Say("Не хватает подходящей рыбы"); return; }
            for (int i = 0; i < o.count; i++) Game.S.fishBag.Remove(el[i]);
            Game.S.cafeOrders.Remove(o);
            CurrencyManager.AddSilver(o.price);
            var f = GameData.GetFish(o.fishId);
            AudioManager.I?.Click();
            ui.Say("Заказ выполнен: " + (f != null ? f.name : "рыба") + " ×" + o.count + "   +" + o.price.ToString("N0") + " серебра");
            Game.Save(); Game.Notify();
        }
    }
}
