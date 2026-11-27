using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChinaFishing
{
    // Рынок: слева панель «Продать», справа рыба из садка с фильтром и сортировкой.
    // Рыбу в садок кладёт кнопка «В садок» в окне улова.
    public class MarketUI
    {
        enum Sort { Auto, Mass, Price }

        class Stack { public FishDef fish; public List<BagFish> items = new List<BagFish>(); public float maxW; public int value; }

        UIManager ui; TopBar bar; RectTransform panel, body;
        Sort sort = Sort.Auto; string filter = ""; string selected = ""; int qty = 1;
        Text selName, qtyT, totalT, hintT, bagT;
        RawImage selPrev; Button sellBtn, sellAllBtn;
        ScrollRect chipScroll, gridScroll;
        readonly Image[] sortImgs = new Image[3];

        public RectTransform Build(UIManager owner)
        {
            ui = owner;
            RectTransform b;
            panel = ui.MakeFrame("Рынок", out b, out bar);
            body = b;

            // ---- левая панель «Продать»
            var left = UIKit.Img(body, UIKit.Panel2);
            left.rectTransform.anchorMin = new Vector2(0, 0); left.rectTransform.anchorMax = new Vector2(0, 1);
            left.rectTransform.pivot = new Vector2(0, 0.5f);
            left.rectTransform.offsetMin = new Vector2(0, 0); left.rectTransform.offsetMax = new Vector2(470, 0);
            var lt = left.transform;
            UIParts.Label(lt, "Продать", 44, new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(430, 60), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            var pv = UIKit.R("Просмотр", lt); UIKit.At(pv, new Vector2(0.5f, 1), new Vector2(0, -82), new Vector2(360, 225));
            selPrev = pv.gameObject.AddComponent<RawImage>(); selPrev.raycastTarget = false;
            selName = UIParts.Label(lt, "", 34, new Vector2(0.5f, 1), new Vector2(0, -316), new Vector2(430, 48), TextAnchor.MiddleCenter, UIKit.Accent, FontStyle.Bold);
            bagT = UIParts.Label(lt, "", 24, new Vector2(0.5f, 1), new Vector2(0, -364), new Vector2(430, 34), TextAnchor.MiddleCenter, UIKit.Dim);

            UIParts.Btn(lt, "−", UIKit.Blue, new Vector2(0.5f, 1), new Vector2(-130, -412), new Vector2(86, 86), () => ChangeQty(-1), 50);
            qtyT = UIParts.Label(lt, "-", 42, new Vector2(0.5f, 1), new Vector2(0, -412), new Vector2(150, 86), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UIParts.Btn(lt, "+", UIKit.Blue, new Vector2(0.5f, 1), new Vector2(130, -412), new Vector2(86, 86), () => ChangeQty(1), 50);
            hintT = UIParts.Label(lt, "", 22, new Vector2(0.5f, 1), new Vector2(0, -504), new Vector2(430, 30), TextAnchor.MiddleCenter, UIKit.Dim);

            totalT = UIParts.Label(lt, "", 34, new Vector2(0.5f, 0), new Vector2(0, 196), new Vector2(430, 50), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            sellBtn = UIParts.Btn(lt, "Продать", UIKit.Green, new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(400, 84), Sell, 38);
            sellAllBtn = UIParts.Btn(lt, "Продать всё", UIKit.Blue, new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(400, 66), SellAll, 30);

            // ---- правая часть: раздел «Рыба», сортировка, фильтр, список
            var tabBtn = UIParts.Btn(body, "Рыба", UIKit.Accent, new Vector2(0, 1), new Vector2(490, 0), new Vector2(220, 70), null, 32);
            string[] sn = { "АВТО", "Масса", "Цена" };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var sb = UIParts.Btn(body, sn[i], UIKit.Panel2, new Vector2(1, 1), new Vector2(-(2 - i) * 180, 0), new Vector2(170, 70), () => { sort = (Sort)k; Refresh(); }, 30);
                sortImgs[i] = sb.GetComponent<Image>();
            }
            UIParts.Label(body, "Сортировка:", 26, new Vector2(1, 1), new Vector2(-560, 0), new Vector2(200, 70), TextAnchor.MiddleRight, UIKit.Dim);

            chipScroll = UIParts.HScroll(body, 70, 8);
            var cr = chipScroll.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1); cr.pivot = new Vector2(0.5f, 1);
            cr.offsetMin = new Vector2(490, -156); cr.offsetMax = new Vector2(0, -84);

            gridScroll = UIKit.Scroll(body, true, new Vector2(360, 330), 3);
            var gr = gridScroll.GetComponent<RectTransform>(); gr.name = "Рыба";
            gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one;
            gr.offsetMin = new Vector2(490, 0); gr.offsetMax = new Vector2(0, -164);
            return panel;
        }

        public void OnOpen() { filter = ""; selected = ""; qty = 1; }

        // ---------------- ДАННЫЕ ----------------
        List<Stack> Stacks()
        {
            var map = new Dictionary<string, Stack>();
            var list = new List<Stack>();
            foreach (var bf in Game.S.fishBag)
            {
                var f = GameData.GetFish(bf.id); if (f == null) continue;
                Stack st;
                if (!map.TryGetValue(bf.id, out st)) { st = new Stack { fish = f }; map[bf.id] = st; list.Add(st); }
                st.items.Add(bf);
                if (bf.weight > st.maxW) st.maxW = bf.weight;
                st.value += Game.SellValue(bf.id, bf.weight);
            }
            if (sort == Sort.Mass) list.Sort((a, b) => b.maxW.CompareTo(a.maxW));
            else if (sort == Sort.Price) list.Sort((a, b) => b.value.CompareTo(a.value));
            return list;
        }

        // Какие именно рыбы пойдут на продажу: АВТО — сначала лёгкие (тяжёлые трофеи остаются), «Масса»/«Цена» — сначала тяжёлые
        List<BagFish> Pick(Stack st, int n)
        {
            var l = new List<BagFish>(st.items);
            if (sort == Sort.Auto) l.Sort((a, b) => a.weight.CompareTo(b.weight)); else l.Sort((a, b) => b.weight.CompareTo(a.weight));
            if (n < l.Count) l.RemoveRange(n, l.Count - n);
            return l;
        }

        static int Value(List<BagFish> l) { int s = 0; foreach (var f in l) s += Game.SellValue(f.id, f.weight); return s; }

        // ---------------- ОБНОВЛЕНИЕ ----------------
        public void Refresh()
        {
            bar.Refresh();
            for (int i = 0; i < 3; i++) sortImgs[i].color = i == (int)sort ? UIKit.Accent : UIKit.Panel2;
            var stacks = Stacks();

            // фильтр «ВСЕ» и виды рыб
            if (filter != "" && !stacks.Exists(x => x.fish.id == filter)) filter = "";
            UIParts.ClearNow(chipScroll.content);
            AddChip("ВСЕ", "");
            foreach (var st in stacks) AddChip(st.fish.name, st.fish.id);

            // сетка
            FishPreview.SetAllActive(false);
            var content = gridScroll.content;
            float keepY = content.anchoredPosition.y;
            UIParts.ClearNow(content);
            Canvas.ForceUpdateCanvases();
            float cw = gridScroll.GetComponent<RectTransform>().rect.width; if (cw < 300f) cw = 1200f;
            int cols = Mathf.Clamp(Mathf.FloorToInt(cw / 380f), 2, 4);
            float gap = 16f, cellW = Mathf.Floor((cw - 20f - gap * (cols - 1)) / cols);
            var g = content.GetComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(cellW, 330); g.spacing = new Vector2(gap, gap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = cols; g.childAlignment = TextAnchor.UpperLeft;

            int shown = 0;
            foreach (var st in stacks)
            {
                if (filter != "" && st.fish.id != filter) continue;
                BuildCard(content, st, cellW); shown++;
            }
            if (shown == 0)
            {
                var t = UIKit.Txt(content, "Садок пуст.\nПоймайте рыбу и нажмите «В садок» в окне улова.", 32, UIKit.Dim);
                t.gameObject.AddComponent<LayoutElement>().preferredHeight = 140;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float maxY = Mathf.Max(0f, content.rect.height - gridScroll.GetComponent<RectTransform>().rect.height);
            content.anchoredPosition = new Vector2(0, Mathf.Clamp(keepY, 0f, maxY));

            // панель «Продать»
            Stack cur = stacks.Find(x => x.fish.id == selected);
            if (cur == null) selected = "";
            qty = cur == null ? 1 : Mathf.Clamp(qty, 1, cur.items.Count);
            if (cur != null) { var p = FishPreview.Get(cur.fish); p.SetActive(true); selPrev.texture = p.Texture; selPrev.color = Color.white; }
            else { selPrev.texture = null; selPrev.color = new Color(1, 1, 1, 0.05f); }
            selName.text = cur != null ? cur.fish.name : "Выберите рыбу";
            bagT.text = "В садке: " + Game.S.fishBag.Count + " из " + Game.BagLimit;
            qtyT.text = cur != null ? qty.ToString() : "-";
            hintT.text = cur != null ? (sort == Sort.Auto ? "Сначала продаются лёгкие рыбы" : "Сначала продаются тяжёлые рыбы") : "";
            int total = cur != null ? Value(Pick(cur, qty)) : 0;
            totalT.text = "Цена: " + total.ToString("N0");
            sellBtn.interactable = cur != null;
            int all = 0; foreach (var bf in Game.S.fishBag) all += Game.SellValue(bf.id, bf.weight);
            sellAllBtn.interactable = Game.S.fishBag.Count > 0;
            UIKit.SetLabel(sellAllBtn, Game.S.fishBag.Count > 0 ? "Продать всё: " + all.ToString("N0") : "Продать всё");
        }

        void AddChip(string text, string id)
        {
            string id_ = id;
            var b = UIParts.TabBtn(chipScroll.content, text, UIParts.TextWidth(text, 20f, 56f), () => { filter = id_; Refresh(); }, 28);
            b.GetComponent<Image>().color = filter == id ? UIKit.Accent : UIKit.Panel2;
        }

        void BuildCard(Transform grid, Stack st, float w)
        {
            bool sel = st.fish.id == selected;
            var card = UIKit.Img(grid, sel ? UIKit.Accent : UIKit.Panel2);
            card.raycastTarget = true;
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
            string id = st.fish.id;
            btn.onClick.AddListener(() => { selected = id; qty = 1; Refresh(); });
            Transform t = card.transform;
            if (sel) { var inner = UIKit.Img(t, UIKit.Panel2); UIKit.Stretch(inner.rectTransform, 5, 5, 5, 5); t = inner.transform; }

            float iw = Mathf.Min(w - 24f, 300f);
            var pv = UIKit.R("Просмотр", t); UIKit.At(pv, new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(iw, iw * 160f / 256f));
            var ri = pv.gameObject.AddComponent<RawImage>(); ri.raycastTarget = false;
            var prev = FishPreview.Get(st.fish); prev.SetActive(true); ri.texture = prev.Texture;

            float y = -14f - iw * 160f / 256f - 8f;
            UIParts.Label(t, st.fish.name, 30, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(w - 24f, 40), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UIParts.Label(t, "Кол-во " + st.items.Count + " Шт", 24, new Vector2(0.5f, 1), new Vector2(0, y - 40), new Vector2(w - 24f, 32), TextAnchor.MiddleCenter, UIKit.Dim);
            UIParts.Label(t, "Макс. вес " + st.maxW.ToString("0.00") + " кг", 24, new Vector2(0.5f, 1), new Vector2(0, y - 72), new Vector2(w - 24f, 32), TextAnchor.MiddleCenter, UIKit.Dim);
            UIParts.Price(t, UIParts.Silver, st.value.ToString("N0"), new Vector2(0.5f, 0), new Vector2(0, 10), 190);
        }

        // ---------------- ДЕЙСТВИЯ ----------------
        void ChangeQty(int d)
        {
            var st = Stacks().Find(x => x.fish.id == selected); if (st == null) return;
            qty = Mathf.Clamp(qty + d, 1, st.items.Count);
            Refresh();
        }

        void Sell()
        {
            var st = Stacks().Find(x => x.fish.id == selected); if (st == null) return;
            var pick = Pick(st, qty);
            int total = Value(pick);
            foreach (var bf in pick) Game.S.fishBag.Remove(bf);
            CurrencyManager.AddSilver(total);
            AudioManager.I?.Click();
            ui.Say("Продано: " + st.fish.name + " ×" + pick.Count + "   +" + total.ToString("N0") + " серебра");
            Game.Save(); Game.Notify();
        }

        void SellAll()
        {
            int n = Game.S.fishBag.Count, total = 0;
            if (n == 0) return;
            foreach (var bf in Game.S.fishBag) total += Game.SellValue(bf.id, bf.weight);
            Game.S.fishBag.Clear();
            CurrencyManager.AddSilver(total);
            AudioManager.I?.Click();
            ui.Say("Продано рыб: " + n + "   +" + total.ToString("N0") + " серебра");
            Game.Save(); Game.Notify();
        }
    }
}
