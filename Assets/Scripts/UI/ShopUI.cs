using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChinaFishing
{
    // Магазин: вкладки сверху, колонка «Производитель» слева, сетка карточек товаров.
    // Данные берутся из GameData.Items (ItemDef): добавили товар там — он сам появился в магазине.
    public class ShopUI
    {
        class Tab { public string name; public ItemCategory[] cats; public Tab(string n, params ItemCategory[] c) { name = n; cats = c; } }

        // Порядок и названия вкладок как на скриншоте. «Сигнализатор», «Поводок» и «Кормушки» пока пустые: таких товаров в игре ещё нет.
        // В конце — вкладки для товаров, которые уже есть в игре, но на скриншоте их нет.
        static readonly Tab[] Tabs =
        {
            new Tab("Все"),
            new Tab("Удилища", ItemCategory.Удочки, ItemCategory.Фидерные, ItemCategory.Карповые, ItemCategory.Пикерные, ItemCategory.Спиннинговые, ItemCategory.Поплавочные, ItemCategory.Маркерные),
            new Tab("Катушки", ItemCategory.Катушки),
            new Tab("Сигнализатор"),
            new Tab("Леска", ItemCategory.Лески),
            new Tab("Поводок"),
            new Tab("Кормушки"),
            new Tab("Грузило", ItemCategory.Грузила),
            new Tab("Крючки", ItemCategory.Крючки),
            new Tab("Приманки", ItemCategory.Приманки),
            new Tab("Наживки", ItemCategory.Наживки),
            new Tab("Прикормки", ItemCategory.Прикормки),
            new Tab("Материалы", ItemCategory.Материалы),
            new Tab("Поплавки", ItemCategory.Поплавки),
            new Tab("Оснастки", ItemCategory.Оснастки),
            new Tab("Прочее", ItemCategory.Прочее),
        };

        const string AllMakers = "ВСЕ", NoMaker = "н/д";

        UIManager ui; TopBar bar; RectTransform panel, body;
        ScrollRect tabScroll, sideScroll, gridScroll;
        readonly List<Image> tabImgs = new List<Image>();
        int tab; string maker = AllMakers;

        // окно товара
        RectTransform dlg; ItemDef dItem; int dQty = 1;
        RawImage dIcon; Text dName, dType, dStats, dDesc, dQtyT, dTotal, dMsg;
        Button dBuy;

        public RectTransform Build(UIManager owner)
        {
            ui = owner;
            RectTransform b;
            panel = ui.MakeFrame("Магазин", out b, out bar);
            body = b;

            // вкладки
            tabScroll = UIParts.HScroll(body, 80, 8);
            var tr = tabScroll.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(0.5f, 1);
            tr.offsetMin = new Vector2(0, -84); tr.offsetMax = Vector2.zero;
            for (int i = 0; i < Tabs.Length; i++)
            {
                int k = i;
                var btn = UIParts.TabBtn(tabScroll.content, Tabs[i].name, UIParts.TextWidth(Tabs[i].name, 20f, 60f), () => { tab = k; maker = AllMakers; Refresh(); }, 30);
                tabImgs.Add(btn.GetComponent<Image>());
            }

            // левая колонка «Производитель»
            var head = UIParts.Label(body, "Производитель", 30, new Vector2(0, 1), new Vector2(8, -100), new Vector2(300, 40), TextAnchor.MiddleLeft, UIKit.Accent, FontStyle.Bold);
            sideScroll = UIKit.Scroll(body, false, Vector2.zero, 1);
            var sr = sideScroll.GetComponent<RectTransform>(); sr.name = "Производители";
            sr.anchorMin = new Vector2(0, 0); sr.anchorMax = new Vector2(0, 1); sr.pivot = new Vector2(0, 1);
            sr.offsetMin = new Vector2(0, 0); sr.offsetMax = new Vector2(310, -146);
            var vl = sideScroll.content.GetComponent<VerticalLayoutGroup>();
            if (vl != null) { vl.spacing = 8; vl.padding = new RectOffset(0, 0, 0, 12); }

            // сетка карточек
            gridScroll = UIKit.Scroll(body, true, new Vector2(380, 380), 3);
            var gr = gridScroll.GetComponent<RectTransform>(); gr.name = "Товары";
            gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one;
            gr.offsetMin = new Vector2(330, 0); gr.offsetMax = new Vector2(0, -100);

            BuildDialog();
            return panel;
        }

        public void Refresh()
        {
            bar.Refresh();
            for (int i = 0; i < tabImgs.Count; i++) tabImgs[i].color = i == tab ? UIKit.Accent : UIKit.Panel2;
            RebuildSide();
            RebuildGrid();
            if (dlg.gameObject.activeSelf) RefreshDialog();
        }

        // Закрытие по кнопке «Назад»: сначала окно товара, потом сам магазин. true — нажатие обработано
        public bool CloseInner()
        {
            if (dlg != null && dlg.gameObject.activeSelf) { dlg.gameObject.SetActive(false); return true; }
            return false;
        }

        public void OnOpen() { dlg.gameObject.SetActive(false); tab = 0; maker = AllMakers; }

        // ---------------- ОТБОР ТОВАРОВ ----------------
        List<ItemDef> Items(bool applyMaker)
        {
            var cats = Tabs[tab].cats;
            var l = new List<ItemDef>();
            foreach (var it in GameData.Items)
            {
                if (cats.Length > 0 && System.Array.IndexOf(cats, it.cat) < 0) continue;
                if (cats.Length == 0 && tab != 0) continue;      // у вкладки нет категорий — товаров нет
                if (applyMaker && maker != AllMakers)
                {
                    if (maker == NoMaker) { if (!string.IsNullOrEmpty(it.maker)) continue; }
                    else if (it.maker != maker) continue;
                }
                l.Add(it);
            }
            return l;
        }

        // ---------------- ПРОИЗВОДИТЕЛИ ----------------
        void RebuildSide()
        {
            var makers = new List<string>();
            foreach (var it in Items(false)) if (!string.IsNullOrEmpty(it.maker) && !makers.Contains(it.maker)) makers.Add(it.maker);
            makers.Sort();
            if (maker != AllMakers && maker != NoMaker && !makers.Contains(maker)) maker = AllMakers;

            UIParts.ClearNow(sideScroll.content);
            var names = new List<string> { AllMakers, NoMaker };
            names.AddRange(makers);
            foreach (var m in names)
            {
                var m_ = m;
                var b = UIKit.Btn(sideScroll.content, m, m == maker ? UIKit.Accent : UIKit.Panel2, () => { maker = m_; RebuildSide(); RebuildGrid(); }, 28);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
            }
        }

        // ---------------- СЕТКА ----------------
        void RebuildGrid()
        {
            var content = gridScroll.content;
            float keepY = content.anchoredPosition.y;
            UIParts.ClearNow(content);

            Canvas.ForceUpdateCanvases();
            float cw = gridScroll.GetComponent<RectTransform>().rect.width; if (cw < 300f) cw = 1400f;
            int cols = Mathf.Clamp(Mathf.FloorToInt(cw / 400f), 2, 5);
            float gap = 16f;
            float cellW = Mathf.Floor((cw - 20f - gap * (cols - 1)) / cols);
            var g = content.GetComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(cellW, 390); g.spacing = new Vector2(gap, gap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = cols;
            g.childAlignment = TextAnchor.UpperLeft;

            var list = Items(true);
            if (list.Count == 0)
            {
                var label = UIKit.Txt(content, Tabs[tab].cats.Length == 0 && tab != 0 ? "Скоро в продаже" : "Нет товаров этого производителя", 36, UIKit.Dim);
                label.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;
                return;
            }
            foreach (var it in list) BuildCard(content, it, cellW);

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float maxY = Mathf.Max(0f, content.rect.height - gridScroll.GetComponent<RectTransform>().rect.height);
            content.anchoredPosition = new Vector2(0, Mathf.Clamp(keepY, 0f, maxY));
        }

        static string TypeLine(ItemDef d)
        {
            switch (d.cat)
            {
                case ItemCategory.Фидерные: return "Фидерное удилище";
                case ItemCategory.Карповые: return "Карповое удилище";
                case ItemCategory.Пикерные: return "Пикерное удилище";
                case ItemCategory.Спиннинговые: return "Спиннинговое удилище";
                case ItemCategory.Поплавочные: return "Поплавочное удилище";
                case ItemCategory.Маркерные: return "Маркерное удилище";
                case ItemCategory.Удочки: return "Удочка";
                default: return string.IsNullOrEmpty(d.sub) ? d.cat.ToString() : d.sub;
            }
        }

        void BuildCard(Transform grid, ItemDef d, float w)
        {
            var card = UIKit.Img(grid, d.special ? UIKit.Accent : UIKit.Panel2);
            card.raycastTarget = true; card.gameObject.name = "Товар " + d.id;
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
            var d_ = d; btn.onClick.AddListener(() => OpenDialog(d_));
            Transform t = card.transform;
            if (d.special)
            {
                var inner = UIKit.Img(t, UIKit.Panel2);    // золотая рамка: золотая подложка и тёмная вставка
                UIKit.Stretch(inner.rectTransform, 5, 5, 5, 5);
                t = inner.transform;
            }
            float iw = w - 24f;
            var icon = UIKit.R("Иконка", t);
            if (IconFactory.IsWide(d)) { float ww = Mathf.Min(iw, 330f); UIKit.At(icon, new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(ww, ww / RodPreview.Aspect)); }
            else UIKit.At(icon, new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(130, 130));
            var ri = icon.gameObject.AddComponent<RawImage>(); ri.texture = IconFactory.Get(d); ri.raycastTarget = false;

            UIParts.Label(t, d.name, 28, new Vector2(0.5f, 1), new Vector2(0, -156), new Vector2(iw, 64), TextAnchor.UpperCenter, Color.white, FontStyle.Bold);
            UIParts.Label(t, TypeLine(d), 22, new Vector2(0.5f, 1), new Vector2(0, -222), new Vector2(iw, 30), TextAnchor.UpperCenter, UIKit.Accent);
            UIParts.Label(t, d.statText ?? "", 21, new Vector2(0.5f, 1), new Vector2(0, -254), new Vector2(iw, 74), TextAnchor.UpperCenter, UIKit.Dim);

            bool owned = !d.consumable && Game.Owns(d.id);
            bool locked = Game.S.level < d.minLevel;
            int s = GameData.PriceSilver(d), gd = d.priceGold;
            if (owned) UIParts.Label(t, "Куплено", 32, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(iw, 46), TextAnchor.MiddleCenter, UIKit.Green, FontStyle.Bold);
            else if (locked) UIParts.Label(t, "Нужен ур. " + d.minLevel, 30, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(iw, 46), TextAnchor.MiddleCenter, UIKit.Dim, FontStyle.Bold);
            else if (s == 0 && gd == 0) UIParts.Label(t, "Бесплатно", 32, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(iw, 46), TextAnchor.MiddleCenter, UIKit.Accent, FontStyle.Bold);
            else
            {
                bool both = s > 0 && gd > 0;
                float dx = both ? w * 0.23f : 0f;
                if (s > 0) UIParts.Price(t, UIParts.Silver, s.ToString("N0"), new Vector2(0.5f, 0), new Vector2(both ? -dx : 0f, 14), 170).alignment = TextAnchor.MiddleLeft;
                if (gd > 0) UIParts.Price(t, UIParts.GoldC, gd.ToString("N0"), new Vector2(0.5f, 0), new Vector2(both ? dx : 0f, 14), 170);
                if (d.OnSale) UIParts.Label(t, "было " + d.price.ToString("N0"), 20, new Vector2(0.5f, 0), new Vector2(0, 62), new Vector2(iw, 26), TextAnchor.MiddleCenter, UIKit.Dim);
            }
            if (d.OnSale)
            {
                var badge = UIKit.Img(t, UIKit.Red);
                UIKit.At(badge.rectTransform, new Vector2(0, 1), new Vector2(10, -10), new Vector2(104, 40));
                UIParts.Label(badge.transform, "SALE", 24, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104, 40), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            }
            if (d.consumable && Game.Count(d.id) > 0)
                UIParts.Label(t, "В запасе: " + Game.Count(d.id), 20, new Vector2(1, 1), new Vector2(-10, -10), new Vector2(160, 30), TextAnchor.MiddleRight, UIKit.Green);
        }

        // ---------------- ОКНО ТОВАРА / ПОКУПКА ----------------
        void BuildDialog()
        {
            dlg = UIKit.R("Окно товара", panel); UIKit.Stretch(dlg);
            var dim = UIKit.R("Затемнение", dlg); UIKit.Stretch(dim);
            var di = dim.gameObject.AddComponent<Image>(); di.color = new Color(0, 0.03f, 0.06f, 0.82f);
            var box = UIKit.Img(dlg, UIKit.PanelC);
            UIKit.At(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1150, 640));
            var bt = box.transform;

            var icon = UIKit.R("Иконка", bt); UIKit.At(icon, new Vector2(0, 1), new Vector2(40, -40), new Vector2(260, 260));
            dIcon = icon.gameObject.AddComponent<RawImage>(); dIcon.raycastTarget = false;
            dName = UIParts.Label(bt, "", 46, new Vector2(0, 1), new Vector2(330, -30), new Vector2(700, 60), TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            dType = UIParts.Label(bt, "", 28, new Vector2(0, 1), new Vector2(330, -94), new Vector2(760, 40), TextAnchor.MiddleLeft, UIKit.Accent);
            dStats = UIParts.Label(bt, "", 28, new Vector2(0, 1), new Vector2(330, -138), new Vector2(780, 80), TextAnchor.UpperLeft, UIKit.Dim);
            dDesc = UIParts.Label(bt, "", 28, new Vector2(0, 1), new Vector2(330, -222), new Vector2(780, 150), TextAnchor.UpperLeft, Color.white);
            dMsg = UIParts.Label(bt, "", 30, new Vector2(0, 0), new Vector2(40, 160), new Vector2(1000, 44), TextAnchor.MiddleLeft, UIKit.Green, FontStyle.Bold);

            UIParts.Btn(bt, "Закрыть", UIKit.Red, new Vector2(1, 1), new Vector2(-24, -24), new Vector2(220, 76), () => dlg.gameObject.SetActive(false), 32);
            UIParts.Btn(bt, "−", UIKit.Blue, new Vector2(0, 0), new Vector2(40, 36), new Vector2(96, 96), () => { dQty = Mathf.Clamp(dQty - 1, 1, 20); RefreshDialog(); }, 54);
            dQtyT = UIParts.Label(bt, "1", 42, new Vector2(0, 0), new Vector2(140, 36), new Vector2(120, 96), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UIParts.Btn(bt, "+", UIKit.Blue, new Vector2(0, 0), new Vector2(264, 36), new Vector2(96, 96), () => { dQty = Mathf.Clamp(dQty + 1, 1, 20); RefreshDialog(); }, 54);
            dTotal = UIParts.Label(bt, "", 30, new Vector2(0, 0), new Vector2(390, 36), new Vector2(440, 96), TextAnchor.MiddleLeft, UIKit.Accent, FontStyle.Bold);
            dBuy = UIParts.Btn(bt, "Купить", UIKit.Green, new Vector2(1, 0), new Vector2(-40, 36), new Vector2(300, 96), Buy, 36);
            dlg.gameObject.SetActive(false);
        }

        void OpenDialog(ItemDef d)
        {
            dItem = d; dQty = 1; dMsg.text = "";
            dlg.gameObject.SetActive(true); dlg.SetAsLastSibling();
            RefreshDialog();
        }

        void RefreshDialog()
        {
            var d = dItem; if (d == null) return;
            dIcon.texture = IconFactory.Get(d);
            var ir = dIcon.rectTransform;
            ir.sizeDelta = IconFactory.IsWide(d) ? new Vector2(280, 280 / RodPreview.Aspect) : new Vector2(240, 240);
            dName.text = d.name;
            dType.text = TypeLine(d) + (string.IsNullOrEmpty(d.maker) ? "" : "  ·  " + d.maker) + (d.consumable && d.packSize > 1 ? "  ·  " + d.packSize + " шт" : "");
            dStats.text = d.statText ?? "";
            dDesc.text = d.desc ?? "";
            bool multi = d.consumable;
            dQtyT.gameObject.SetActive(multi);
            dQtyT.text = dQty.ToString();
            long s = (long)GameData.PriceSilver(d) * (multi ? dQty : 1), g = (long)d.priceGold * (multi ? dQty : 1);
            string total = "Итого: " + s.ToString("N0") + " сер." + (g > 0 ? " + " + g.ToString("N0") + " зол." : "");
            bool owned = !d.consumable && Game.Owns(d.id);
            dTotal.text = owned ? "Уже куплено" : total;
            dBuy.interactable = !owned;
        }

        void Buy()
        {
            var d = dItem; if (d == null) return;
            int n = d.consumable ? dQty : 1, bought = 0; string err = null;
            ui.Suppress = true;
            for (int i = 0; i < n; i++) { err = Game.Buy(d); if (err != null) break; bought++; }
            ui.Suppress = false;
            if (bought > 0) { AudioManager.I?.Click(); }
            if (err != null) { dMsg.color = UIKit.Red; dMsg.text = bought > 0 ? "Куплено " + bought + ", далее: " + err : err; }
            else { dMsg.color = UIKit.Green; dMsg.text = "Куплено: " + d.name; }
            Refresh();
        }
    }
}
