using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChinaFishing
{
    // Экраны аккаунта: вход, регистрация, сброс пароля, профиль. Стиль и размеры — как у остального интерфейса (UIKit).
    public class AccountUI : MonoBehaviour
    {
        public static AccountUI I;
        enum Scr { Login, Register, Forgot, Profile, Offline }

        RectTransform root, overlay, panel, chip;
        Text chipText, status;
        Scr cur;
        bool busy, termsOk;
        InputField fNick, fMail, fPass, fPass2, fCode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        public static void Create()
        {
            if (I != null) return;
            new GameObject("Аккаунт-интерфейс").AddComponent<AccountUI>();
        }

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }

        void Start()
        {
            UIKit.Init();
            BuildCanvas();
            var auth = AuthManager.Create();
            if (!ServerConfig.IsConfigured) { Open(Scr.Offline); return; }
            Open(Scr.Login); // пока идёт проверка сессии — закрыто экраном входа
            if (!auth.LoggedIn) return;
            SetBusy(true, "Вход в аккаунт...");
            auth.Restore(r =>
            {
                SetBusy(false, "");
                if (r.ok) { Hide(); return; }
                Msg(r.message, false);
            });
        }

        void BuildCanvas()
        {
            var cg = new GameObject("Канвас аккаунта");
            cg.transform.SetParent(transform, false);
            var canvas = cg.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            var sc = cg.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 1f;
            cg.AddComponent<GraphicRaycaster>();
            root = cg.GetComponent<RectTransform>();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
            }

            // Фон-экран, блокирует всё, что под ним
            overlay = UIKit.R("Экран", root); UIKit.Stretch(overlay);
            var bg = overlay.gameObject.AddComponent<Image>(); bg.color = new Color(0.02f, 0.08f, 0.11f, 0.97f);
            overlay.gameObject.SetActive(false);

            // Плашка «Ник · ID» в меню
            var ci = UIKit.Btn(root, "", UIKit.Panel2, () => Open(Scr.Profile), 30);
            chip = ci.GetComponent<RectTransform>();
            UIKit.At(chip, new Vector2(0, 1), new Vector2(40, -30), new Vector2(560, 76));
            chipText = ci.GetComponentInChildren<Text>();
            chip.gameObject.SetActive(false);
        }

        // ---------------- ПОКАЗ / СКРЫТИЕ ----------------
        void Open(Scr s)
        {
            cur = s; busy = false; termsOk = false;
            overlay.gameObject.SetActive(true);
            Inp.UiBlocking = true; Inp.Move = Vector2.zero;
            if (panel != null) Destroy(panel.gameObject);
            var img = UIKit.Img(overlay, UIKit.PanelC);
            panel = img.rectTransform;
            UIKit.At(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 100));
            panel.pivot = new Vector2(0.5f, 0.5f);
            var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 14; v.padding = new RectOffset(48, 48, 36, 36);
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fNick = fMail = fPass = fPass2 = fCode = null;

            Label("China Fishing", 64, UIKit.Accent, 80, FontStyle.Bold);
            switch (s)
            {
                case Scr.Login: BuildLogin(); break;
                case Scr.Register: BuildRegister(); break;
                case Scr.Forgot: BuildForgot(); break;
                case Scr.Profile: BuildProfile(); break;
                default: BuildOffline(); break;
            }
        }

        void Hide()
        {
            overlay.gameObject.SetActive(false);
            if (panel != null) { Destroy(panel.gameObject); panel = null; }
        }

        // Кнопка «Назад» Android. true — нажатие обработано здесь
        public bool HandleBack()
        {
            if (overlay == null || !overlay.gameObject.activeSelf) return false;
            if (busy) return true;
            if (cur == Scr.Profile) Hide();
            else if (cur == Scr.Register || cur == Scr.Forgot) Open(Scr.Login);
            return true;
        }

        // ---------------- ЭКРАНЫ ----------------
        void BuildLogin()
        {
            Label("Вход в аккаунт", 40, UIKit.Dim, 56, FontStyle.Normal);
            fMail = Field("Почта", false, true);
            fPass = Field("Пароль", true, false);
            fMail.text = PlayerPrefs.GetString("cf_last_mail", "");
            StatusLine();
            Act("Войти", UIKit.Green, DoLogin);
            var row = Row();
            UIKit.Btn(row, "Регистрация", UIKit.Blue, () => Open(Scr.Register), 34);
            UIKit.Btn(row, "Забыли пароль?", UIKit.Gray, () => Open(Scr.Forgot), 34);
        }

        void BuildRegister()
        {
            Label("Создание аккаунта", 40, UIKit.Dim, 56, FontStyle.Normal);
            fNick = Field("Никнейм (3–16: буквы, цифры, _)", false, false);
            fMail = Field("Почта", false, true);
            fPass = Field("Пароль (от 8: буквы и цифры)", true, false);
            fPass2 = Field("Повторите пароль", true, false);
            if (ServerConfig.RequireTerms)
            {
                var row = Row(70);
                var box = UIKit.Btn(row, "", UIKit.Gray, null, 30);
                var le = box.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 70; le.flexibleWidth = 0;
                var tx = UIKit.Btn(row, "Я принимаю правила игры", new Color(0, 0, 0, 0.001f), null, 32);
                UnityEngine.Events.UnityAction toggle = () => { termsOk = !termsOk; box.GetComponent<Image>().color = termsOk ? UIKit.Green : UIKit.Gray; UIKit.SetLabel(box, termsOk ? "✓" : ""); };
                box.onClick.AddListener(toggle); tx.onClick.AddListener(toggle);
            }
            StatusLine();
            Act("Создать аккаунт", UIKit.Green, DoRegister);
            Act("Назад", UIKit.Gray, () => Open(Scr.Login));
        }

        void BuildForgot()
        {
            Label("Сброс пароля", 40, UIKit.Dim, 56, FontStyle.Normal);
            fMail = Field("Почта", false, true);
            Act("Отправить код на почту", UIKit.Blue, DoSendCode);
            fCode = Field("Код из письма", false, false);
            fPass = Field("Новый пароль (от 8: буквы и цифры)", true, false);
            StatusLine();
            Act("Сменить пароль", UIKit.Green, DoReset);
            Act("Назад", UIKit.Gray, () => Open(Scr.Login));
        }

        void BuildProfile()
        {
            var a = AuthManager.I;
            Label(a.Nick, 48, Color.white, 64, FontStyle.Bold);
            Label("Player ID: " + a.PlayerNo, 38, UIKit.Accent, 52, FontStyle.Bold);
            if (a.Profile != null && !string.IsNullOrEmpty(a.Profile.created_at) && a.Profile.created_at.Length >= 10)
                Label("Регистрация: " + a.Profile.created_at.Substring(0, 10), 32, UIKit.Dim, 44, FontStyle.Normal);
            if (Game.S != null)
                Label("Уровень " + Game.S.level + "  ·  Опыт " + Game.S.xp + "  ·  Поймано рыб: " + Game.S.totalCaught, 32, UIKit.Dim, 44, FontStyle.Normal);
            if (a.Offline) Label("Нет связи с сервером (офлайн-режим)", 30, UIKit.Red, 40, FontStyle.Normal);
            fPass = Field("Новый пароль (от 8: буквы и цифры)", true, false);
            StatusLine();
            Act("Сменить пароль", UIKit.Blue, DoChangePass);
            Act("Выйти из аккаунта", UIKit.Red, () => { AuthManager.I.Logout(); Open(Scr.Login); });
            Act("Закрыть", UIKit.Gray, Hide);
        }

        void BuildOffline()
        {
            Label("Онлайн-сервер не настроен", 40, UIKit.Dim, 56, FontStyle.Normal);
            Label("Заполните url и anonKey в Assets/Resources/server_config.json. Пока можно играть без аккаунта.", 30, UIKit.Dim, 110, FontStyle.Normal);
            Act("Играть без аккаунта", UIKit.Green, Hide);
        }

        // ---------------- ДЕЙСТВИЯ ----------------
        void DoLogin()
        {
            if (busy) return;
            string mail = fMail.text.Trim();
            SetBusy(true, "Вход...");
            AuthManager.I.Login(mail, fPass.text, r =>
            {
                SetBusy(false, "");
                if (r.ok) { PlayerPrefs.SetString("cf_last_mail", mail); Hide(); } else Msg(r.message, false);
            });
        }

        void DoRegister()
        {
            if (busy) return;
            if (fPass.text != fPass2.text) { Msg("Пароли не совпадают.", false); return; }
            if (ServerConfig.RequireTerms && !termsOk) { Msg("Нужно принять правила игры.", false); return; }
            SetBusy(true, "Создаём аккаунт...");
            AuthManager.I.Register(fNick.text.Trim(), fMail.text.Trim(), fPass.text, r =>
            {
                SetBusy(false, "");
                if (!r.ok) { Msg(r.message, false); return; }
                if (AuthManager.I.LoggedIn) Hide(); else { Open(Scr.Login); Msg(r.message, true); }
            });
        }

        void DoSendCode()
        {
            if (busy) return;
            SetBusy(true, "Отправляем...");
            AuthManager.I.SendResetCode(fMail.text.Trim(), r => { SetBusy(false, ""); Msg(r.message, r.ok); });
        }

        void DoReset()
        {
            if (busy) return;
            SetBusy(true, "Меняем пароль...");
            AuthManager.I.ResetWithCode(fMail.text.Trim(), fCode.text, fPass.text, r =>
            {
                SetBusy(false, "");
                if (r.ok) { Open(Scr.Login); Msg(r.message, true); } else Msg(r.message, false);
            });
        }

        void DoChangePass()
        {
            if (busy) return;
            SetBusy(true, "Меняем пароль...");
            AuthManager.I.ChangePassword(fPass.text, r => { SetBusy(false, ""); if (r.ok) fPass.text = ""; Msg(r.message, r.ok); });
        }

        // ---------------- ЭЛЕМЕНТЫ ----------------
        void SetBusy(bool b, string text)
        {
            busy = b;
            if (panel != null) foreach (var x in panel.GetComponentsInChildren<Button>()) x.interactable = !b;
            if (b) Msg(text, true);
        }

        void Msg(string t, bool good)
        {
            if (status == null) return;
            status.text = t; status.color = good ? UIKit.Dim : new Color(1f, 0.55f, 0.5f);
        }

        void StatusLine()
        {
            status = Label("", 30, UIKit.Dim, 66, FontStyle.Normal);
        }

        Text Label(string s, int size, Color c, float h, FontStyle st)
        {
            var t = UIKit.Txt(panel, s, size, c, TextAnchor.MiddleCenter, st);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
            return t;
        }

        void Act(string label, Color c, UnityEngine.Events.UnityAction a)
        {
            var b = UIKit.Btn(panel, label, c, a, 36);
            b.gameObject.AddComponent<LayoutElement>().preferredHeight = 88;
        }

        RectTransform Row(float h = 88)
        {
            var r = UIKit.R("Ряд", panel);
            var hl = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 16; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
            r.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
            return r;
        }

        InputField Field(string hint, bool pwd, bool mail)
        {
            var bg = UIKit.Img(panel, UIKit.Panel2); bg.raycastTarget = true;
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = 88;
            var txt = UIKit.Txt(bg.transform, "", 36, Color.white, TextAnchor.MiddleLeft);
            UIKit.Stretch(txt.rectTransform, 24, 4, 24, 4);
            txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.supportRichText = false;
            var ph = UIKit.Txt(bg.transform, hint, 34, new Color(0.72f, 0.84f, 0.88f, 0.55f), TextAnchor.MiddleLeft, FontStyle.Italic);
            UIKit.Stretch(ph.rectTransform, 24, 4, 24, 4);
            ph.horizontalOverflow = HorizontalWrapMode.Overflow;
            var f = bg.gameObject.AddComponent<InputField>();
            f.targetGraphic = bg; f.textComponent = txt; f.placeholder = ph;
            f.lineType = InputField.LineType.SingleLine; f.characterLimit = 100;
            f.contentType = pwd ? InputField.ContentType.Password : (mail ? InputField.ContentType.EmailAddress : InputField.ContentType.Standard);
            return f;
        }

        // ---------------- ПЛАШКА В МЕНЮ ----------------
        void Update()
        {
            if (chip == null) return;
            var gm = GameManager.I; var a = AuthManager.I;
            bool vis = gm != null && gm.Ready && gm.InMenu && a != null && a.LoggedIn
                       && !overlay.gameObject.activeSelf && (gm.ui == null || !gm.ui.HasPanel);
            if (chip.gameObject.activeSelf != vis) chip.gameObject.SetActive(vis);
            if (vis)
            {
                float k = root.rect.width / Mathf.Max(1f, Screen.width);
                chip.anchoredPosition = new Vector2(40 + Screen.safeArea.xMin * k, -30);
                chipText.text = a.Nick + "  ·  ID " + a.PlayerNo + (a.Offline ? "  (офлайн)" : "");
            }
        }
    }
}
