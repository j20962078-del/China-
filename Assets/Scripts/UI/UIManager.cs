using UnityEngine;
using UnityEngine.UI;

namespace ChinaFishing
{
    // Окна зданий: Магазин, Рынок, Кафе. Создаёт их один раз, открывает и закрывает, ставит игру на паузу.
    // Висит на том же объекте, что и GameUI, и строит панели внутри его безопасной области.
    public class UIManager : MonoBehaviour
    {
        public static UIManager I;
        public bool Suppress;                  // true — не перестраивать окно на каждое изменение (пакетные покупки)

        GameUI gui;
        ShopUI shop; MarketUI market; CafeUI cafe;
        RectTransform shopP, marketP, cafeP, openPanel;
        BuildingType openType;
        Text toast; RectTransform toastBox; float toastT;

        public bool IsOpen { get { return openPanel != null; } }

        public static UIManager Create(GameUI owner)
        {
            var m = owner.gameObject.AddComponent<UIManager>();
            m.Init(owner);
            return m;
        }

        void Init(GameUI owner)
        {
            I = this; gui = owner;
            shop = new ShopUI(); market = new MarketUI(); cafe = new CafeUI();
            shopP = shop.Build(this); marketP = market.Build(this); cafeP = cafe.Build(this);
            BuildToast();
            shopP.gameObject.SetActive(false); marketP.gameObject.SetActive(false); cafeP.gameObject.SetActive(false);
            Game.Changed += OnChanged;
        }

        void OnDestroy() { Game.Changed -= OnChanged; if (I == this) I = null; }

        void OnChanged() { if (IsOpen && !Suppress) RefreshOpen(); }

        // Рамка окна: затемнение, тёмная панель, верхняя панель с валютой и областью содержимого
        public RectTransform MakeFrame(string title, out RectTransform body, out TopBar bar)
        {
            var p = UIKit.R(title, gui.SafeRoot); UIKit.Stretch(p);
            var dim = UIKit.R("Фон", p); UIKit.Stretch(dim);
            var di = dim.gameObject.AddComponent<Image>(); di.color = new Color(0, 0.04f, 0.08f, 0.88f);
            var box = UIKit.Img(p, UIKit.PanelC); UIKit.Stretch(box.rectTransform, 24, 16, 24, 16);
            bar = TopBar.Build(box.transform, Close, () => Say("Пополнение золота пока недоступно"));
            body = UIKit.R("Область", box.transform); UIKit.Stretch(body, 24, 16, 24, 136);
            return p;
        }

        void BuildToast()
        {
            toastBox = UIKit.R("Сообщение", gui.SafeRoot);
            var bg = toastBox.gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0.78f); bg.raycastTarget = false;
            UIKit.At(toastBox, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(1300, 80));
            toast = UIParts.Label(toastBox, "", 32, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1260, 76), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            toastBox.gameObject.SetActive(false);
        }

        public void Say(string s)
        {
            if (toast == null) return;
            toast.text = s; toastT = 3.5f;
            toastBox.gameObject.SetActive(true); toastBox.SetAsLastSibling();
        }

        void Update()
        {
            if (toastT > 0f)
            {
                toastT -= Time.unscaledDeltaTime;
                if (toastT <= 0f) toastBox.gameObject.SetActive(false);
            }
            if (openPanel == cafeP) cafe.Tick();
        }

        // ---------------- ОТКРЫТИЕ И ЗАКРЫТИЕ ----------------
        public void Open(BuildingType t)
        {
            if (IsOpen) return;
            gui.BeginExternal();
            openType = t;
            openPanel = t == BuildingType.Shop ? shopP : t == BuildingType.Market ? marketP : cafeP;
            if (t == BuildingType.Shop) shop.OnOpen(); else if (t == BuildingType.Market) market.OnOpen();
            openPanel.gameObject.SetActive(true); openPanel.SetAsLastSibling();
            toastBox.gameObject.SetActive(false);
            RefreshOpen();
        }

        public void RefreshOpen()
        {
            if (openPanel == shopP) shop.Refresh();
            else if (openPanel == marketP) market.Refresh();
            else if (openPanel == cafeP) cafe.Refresh();
        }

        // Кнопка «Назад» и крестик. Сначала закрывается вложенное окно (карточка товара), потом само здание
        public void Close()
        {
            if (!IsOpen) return;
            if (openPanel == shopP && shop.CloseInner()) return;
            openPanel.gameObject.SetActive(false);
            openPanel = null;
            toastBox.gameObject.SetActive(false);
            Game.Save();
            gui.EndExternal();
        }
    }
}
