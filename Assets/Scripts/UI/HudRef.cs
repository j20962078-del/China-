using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ChinaFishing
{
    // Элементы HUD «как на референсе»: круглая миникарта (север сверху), время и температура,
    // координаты-сектор под миникартой и лента чата слева. Всё строится кодом, как и остальной интерфейс.
    public class HudRef : MonoBehaviour
    {
        public static HudRef I;

        // Подключение к серверу (Supabase) делается подпиской на это событие; пока чат локальный.
        public static Action<string> OnSend;

        const int MaxLines = 5;
        const float FadeAfter = 8f, HideAfter = 15f;
        static readonly Color[] NickColors =
        {
            new Color(0.49f, 0.82f, 0.49f), new Color(0.91f, 0.83f, 0.42f), new Color(0.44f, 0.76f, 0.91f),
            new Color(0.94f, 0.63f, 0.35f), new Color(0.91f, 0.54f, 0.72f)
        };

        static Sprite circle;

        RawImage mini; Text clockT, tempT, coordT, chatT, northT;
        CanvasGroup chatGroup; RectTransform chatBox, inputRow;
        InputField input;
        Image weatherImg, xpRing; Text levelT, bagCount, ringLvl, ringXp;
        Button standBtn; Text guideT; RectTransform arrowR; Image[] slotImg = new Image[RodStands.Max]; Text[] slotTxt = new Text[RodStands.Max];
        readonly List<string> lines = new List<string>();
        float lastMsg = -100f;

        // Круглый спрайт для миникарты и джойстика (создаётся один раз)
        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int n = 128;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                float r = n * 0.5f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                        t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d)));
                    }
                t.Apply();
                circle = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return circle;
            }
        }


        // ---------- иконки (свои, белые линейные; лежат в Resources/UI/Icons как PNG-байты) ----------
        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        public static Sprite Icon(string n)
        {
            Sprite sp;
            if (icons.TryGetValue(n, out sp)) return sp;
            var ta = Resources.Load<TextAsset>("UI/Icons/" + n);
            if (ta == null) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            t.LoadImage(ta.bytes);
            sp = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
            icons[n] = sp;
            return sp;
        }

        // Круглая (или скруглённая) подложка + белый значок; возвращает кнопку
        static Button IconBtn(Transform parent, string icon, bool round, Vector2 anchor, Vector2 pos, float size, UnityEngine.Events.UnityAction click, out Image glyph)
        {
            var back = UIKit.Img(parent, new Color(0, 0, 0, 0.38f), !round);
            if (round) back.sprite = Circle;
            back.raycastTarget = true;
            UIKit.At(back.rectTransform, anchor, pos, new Vector2(size, size));
            var b = back.gameObject.AddComponent<Button>(); b.targetGraphic = back;
            if (click != null) b.onClick.AddListener(click);
            b.onClick.AddListener(() => { if (AudioManager.I != null) AudioManager.I.Click(); });
            glyph = UIKit.Img(back.transform, Color.white, false);
            glyph.sprite = Icon(icon); glyph.preserveAspect = true;
            UIKit.Stretch(glyph.rectTransform, size * 0.16f, size * 0.16f, size * 0.16f, size * 0.16f);
            return b;
        }

        public static HudRef Build(RectTransform hud)
        {
            var go = UIKit.R("HUD как на референсе", hud);
            UIKit.Stretch(go);
            var h = go.gameObject.AddComponent<HudRef>();
            I = h;
            h.BuildMini(go);
            h.BuildChat(go);
            ChatClient.Create(h);
            RodStands.Create();
            h.BuildStands(go);
            h.BuildTop(go);
            h.Post("Система", "Добро пожаловать на озеро!");
            return h;
        }

        // ---------- миникарта ----------
        void BuildMini(RectTransform p)
        {
            const float S = 230f;
            var ring = UIKit.Img(p, new Color(0, 0, 0, 0.55f), false); ring.sprite = Circle;
            UIKit.At(ring.rectTransform, new Vector2(0, 1), new Vector2(20, -20), new Vector2(S + 12, S + 12));

            ring.raycastTarget = true;
            var rb = ring.gameObject.AddComponent<Button>(); rb.targetGraphic = ring;
            rb.onClick.AddListener(() => { if (GameUI.I != null) GameUI.I.OpenMap(); });
            var mask = UIKit.Img(ring.transform, Color.white, false); mask.sprite = Circle;
            mask.rectTransform.anchorMin = mask.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            mask.rectTransform.sizeDelta = new Vector2(S, S);
            var m = mask.gameObject.AddComponent<Mask>(); m.showMaskGraphic = false;

            var mr = UIKit.R("Карта", mask.transform); UIKit.Stretch(mr);
            mini = mr.gameObject.AddComponent<RawImage>(); mini.raycastTarget = false;

            var dot = UIKit.Img(mask.transform, UIKit.Accent, false); dot.sprite = Circle;
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dot.rectTransform.sizeDelta = new Vector2(20, 20);

            northT = UIKit.Txt(ring.transform, "С", 24, new Color(0.55f, 0.8f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.At(northT.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(40, 30));
            northT.rectTransform.pivot = new Vector2(0.5f, 1);

            // время поверх нижней части карты, температура и сектор под ней
            clockT = UIKit.Txt(ring.transform, "", 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.At(clockT.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(160, 36));
            clockT.rectTransform.pivot = new Vector2(0.5f, 0);
            clockT.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);

            tempT = UIKit.Txt(p, "", 28, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.At(tempT.rectTransform, new Vector2(0, 1), new Vector2(34, -262), new Vector2(210, 34));
            tempT.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);

            coordT = UIKit.Txt(p, "", 24, new Color(1, 1, 1, 0.65f), TextAnchor.MiddleLeft);
            UIKit.At(coordT.rectTransform, new Vector2(0, 1), new Vector2(34, -296), new Vector2(210, 30));
            coordT.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);
        }

        // ---------- чат ----------
        void BuildChat(RectTransform p)
        {
            var box = UIKit.R("Чат", p);
            UIKit.At(box, new Vector2(0, 1), new Vector2(20, -340), new Vector2(560, 200));
            chatBox = box;
            chatGroup = box.gameObject.AddComponent<CanvasGroup>();
            chatGroup.blocksRaycasts = false;      // сам чат не перехватывает касания (под ним свайп камеры и джойстик)

            var bg = UIKit.Img(box, new Color(0, 0, 0, 0.30f)); UIKit.Stretch(bg.rectTransform);
            chatT = UIKit.Txt(box, "", 24, Color.white, TextAnchor.LowerLeft);
            chatT.supportRichText = true; chatT.verticalOverflow = VerticalWrapMode.Truncate;
            UIKit.Stretch(chatT.rectTransform, 12, 8, 12, 8);

            // маленькая кнопка «Чат» открывает ввод
            var open = UIKit.Btn(p, "Чат", new Color(0, 0, 0, 0.45f), ToggleInput, 24);
            UIKit.At(open.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(20, -546), new Vector2(110, 52));

            inputRow = UIKit.R("Ввод чата", p);
            UIKit.At(inputRow, new Vector2(0, 1), new Vector2(140, -546), new Vector2(600, 52));
            var ibg = UIKit.Img(inputRow, new Color(0, 0, 0, 0.7f)); ibg.raycastTarget = true; UIKit.Stretch(ibg.rectTransform);
            var txt = UIKit.Txt(inputRow, "", 26, Color.white, TextAnchor.MiddleLeft); UIKit.Stretch(txt.rectTransform, 14, 0, 14, 0);
            txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.supportRichText = false;
            var ph = UIKit.Txt(inputRow, "Сообщение...", 26, new Color(1, 1, 1, 0.4f), TextAnchor.MiddleLeft); UIKit.Stretch(ph.rectTransform, 14, 0, 14, 0);
            input = ibg.gameObject.AddComponent<InputField>();
            input.targetGraphic = ibg; input.textComponent = txt; input.placeholder = ph;
            input.characterLimit = 120; input.lineType = InputField.LineType.SingleLine;
            input.onEndEdit.AddListener(OnEnd);
            inputRow.gameObject.SetActive(false);
        }

        void ToggleInput()
        {
            bool on = !inputRow.gameObject.activeSelf;
            inputRow.gameObject.SetActive(on);
            if (on) { input.text = ""; input.ActivateInputField(); lastMsg = Time.unscaledTime; }
        }

        void OnEnd(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length > 0)
            {
                if (OnSend != null) OnSend(s);
                if (ChatClient.I == null || !ChatClient.I.Send(s))
                {
                    Post("Вы", s);
                    Post("Система", "Войдите в аккаунт, чтобы писать в общий чат.");
                }
            }
            inputRow.gameObject.SetActive(false);
        }

        public void Post(string nick, string text, bool mine = false)
        {
            Color c = (mine || nick == "Вы") ? Color.white : NickColors[Mathf.Abs(nick.GetHashCode()) % NickColors.Length];
            lines.Add("<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + nick + ":</color> " + text);
            while (lines.Count > MaxLines) lines.RemoveAt(0);
            lastMsg = Time.unscaledTime;
            if (chatT != null) chatT.text = string.Join("\n", lines.ToArray());
        }



        // ---------- верхняя правая часть как на референсе ----------
        void BuildTop(RectTransform p)
        {
            Image g;
            var ui = GameUI.I;
            var A = new Vector2(1, 1);
            IconBtn(p, "gear", true, A, new Vector2(-20, -20), 90, () => ui.OpenSettings(), out g);
            weatherImg = UIKit.Img(p, Color.white, false); weatherImg.sprite = Icon("sun"); weatherImg.preserveAspect = true;
            UIKit.At(weatherImg.rectTransform, A, new Vector2(-124, -20), new Vector2(90, 90));
            // уровень в ромбе
            var dia = UIKit.Img(p, Color.white, false); dia.sprite = Icon("level"); dia.preserveAspect = true;
            UIKit.At(dia.rectTransform, A, new Vector2(-232, -14), new Vector2(104, 104));
            levelT = UIKit.Txt(dia.transform, "1", 46, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); UIKit.Stretch(levelT.rectTransform);
            levelT.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);

            // нижний ряд: меню, садок (открывает рюкзак), рюкзак
            IconBtn(p, "backpack", true, A, new Vector2(-20, -130), 90, () => ui.OpenInventory(), out g);
            IconBtn(p, "keepnet", true, A, new Vector2(-124, -130), 90, () => ui.OpenInventory(), out g);
            bagCount = UIKit.Txt(p, "0", 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.At(bagCount.rectTransform, A, new Vector2(-124, -222), new Vector2(90, 32));
            bagCount.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);
            IconBtn(p, "menu", true, A, new Vector2(-228, -130), 90, () => GameManager.I.ToMenu(), out g);

            IconBtn(p, "rodhand", true, A, new Vector2(-332, -130), 90, () => ui.NextBait(), out g);

            // кольцо уровня и опыта слева внизу
            var back = UIKit.Img(p, new Color(0, 0, 0, 0.38f), false); back.sprite = Circle;
            UIKit.At(back.rectTransform, new Vector2(0, 0), new Vector2(30, 30), new Vector2(130, 130));
            var track = UIKit.Img(back.transform, new Color(1, 1, 1, 0.25f), false); track.sprite = Icon("ring"); UIKit.Stretch(track.rectTransform, 4, 4, 4, 4);
            xpRing = UIKit.Img(back.transform, Color.white, false); xpRing.sprite = Icon("ring"); UIKit.Stretch(xpRing.rectTransform, 4, 4, 4, 4);
            xpRing.type = Image.Type.Filled; xpRing.fillMethod = Image.FillMethod.Radial360; xpRing.fillOrigin = 2; xpRing.fillClockwise = true;
            ringLvl = UIKit.Txt(back.transform, "1", 44, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.At(ringLvl.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(110, 50)); ringLvl.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ringXp = UIKit.Txt(back.transform, "", 17, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter);
            UIKit.At(ringXp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -26), new Vector2(120, 24)); ringXp.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // «глаз» — вид от первого/третьего лица, справа по центру
            IconBtn(p, "eye", true, new Vector2(1, 0.5f), new Vector2(-30, 40), 100, () =>
            {
                var rg = GameManager.I != null ? GameManager.I.rig : null;
                if (rg != null) rg.SetFirstPerson(!rg.firstPerson);
            }, out g);
        }

        // ---------- подставки: слоты 1-3, кнопка, стрелка «вернуться к удочке» ----------
        void BuildStands(RectTransform p)
        {
            for (int i = 0; i < RodStands.Max; i++)
            {
                int slot = i + 1;
                Image gl;
                var b = IconBtn(p, "slot", false, new Vector2(1, 1), new Vector2(-352 - (RodStands.Max - 1 - i) * 92, -20), 84, () => GuideTo(slot), out gl);
                slotImg[i] = b.GetComponent<Image>();
                var nt = UIKit.Txt(b.transform, (i + 1).ToString(), 26, Color.white, TextAnchor.LowerRight, FontStyle.Bold);
                UIKit.Stretch(nt.rectTransform, 4, 2, 8, 2);
                slotTxt[i] = nt;
            }

            standBtn = UIKit.Btn(p, "Поставить подставку", UIKit.Panel2, OnStand, 28);
            UIKit.At(standBtn.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(-40, 210), new Vector2(300, 90));
            standBtn.gameObject.SetActive(false);

            var ar = UIKit.Txt(p, "▲", 54, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.At(ar.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -260), new Vector2(70, 70));
            ar.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ar.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);
            arrowR = ar.rectTransform; arrowR.gameObject.SetActive(false);

            guideT = UIKit.Txt(p, "", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.At(guideT.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(700, 40));
            guideT.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);
        }

        void GuideTo(int slot)
        {
            var st = RodStands.I.Get(slot);
            if (st == null) { if (GameUI.I != null) GameUI.I.Toast("Подставка " + slot + " не установлена"); return; }
            RodStands.I.guideSlot = RodStands.I.guideSlot == slot ? 0 : slot;   // повторное нажатие выключает стрелку
            if (GameUI.I != null && RodStands.I.guideSlot != 0) GameUI.I.Toast(RodStands.NameOf(slot) + " · " + GridCoord.Format(RodStands.I.PosOf(st)));
        }

        void OnStand()
        {
            var gm = GameManager.I; var rs = RodStands.I;
            var near = rs.Nearest(gm.player.transform.position, 2.5f);
            if (near != null)
            {
                rs.Take(near);
                if (GameUI.I != null) GameUI.I.Toast(RodStands.NameOf(near.slot) + " взята");
            }
            else
            {
                float yaw = gm.rig != null ? gm.rig.yaw : gm.player.transform.eulerAngles.y;
                var st = rs.Place(gm.player.transform.position, yaw);
                if (st != null && GameUI.I != null) GameUI.I.Toast(RodStands.NameOf(st.slot) + " сохранена · " + GridCoord.Format(rs.PosOf(st)));
            }
        }

        void UpdateStands()
        {
            var gm = GameManager.I; var rs = RodStands.I;
            if (rs == null) return;
            var f = FishingController.I;
            var pp = gm.player.transform.position;

            // контекстная кнопка — только когда не рыбачим
            bool idle = f == null || f.state == FState.Нет;
            var near = rs.Nearest(pp, 2.5f);
            bool show = idle && (near != null || rs.CanPlace);
            if (standBtn.gameObject.activeSelf != show) standBtn.gameObject.SetActive(show);
            if (show) UIKit.SetLabel(standBtn, near != null ? "Взять подставку" : "Поставить подставку");

            // слоты 1-3
            for (int i = 0; i < RodStands.Max; i++)
            {
                bool on = rs.Get(i + 1) != null;
                slotImg[i].color = rs.guideSlot == i + 1 ? UIKit.Accent : (on ? new Color(0.2f, 0.7f, 0.42f, 0.8f) : new Color(0, 0, 0, 0.45f));
                slotTxt[i].text = (i + 1).ToString();
            }

            // стрелка к цели
            var g = rs.guideSlot != 0 ? rs.Get(rs.guideSlot) : null;
            if (g == null) { arrowR.gameObject.SetActive(false); guideT.text = ""; return; }
            Vector3 to = rs.PosOf(g) - pp; to.y = 0;
            float dist = to.magnitude;
            if (dist < 4f) { rs.guideSlot = 0; if (GameUI.I != null) GameUI.I.Toast("Вы у подставки " + g.slot); return; }
            float camYaw = gm.cam != null ? gm.cam.transform.eulerAngles.y : 0f;
            float bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            arrowR.gameObject.SetActive(true);
            arrowR.localEulerAngles = new Vector3(0, 0, -Mathf.DeltaAngle(camYaw, bearing));
            guideT.text = RodStands.NameOf(g.slot) + " · " + Mathf.RoundToInt(dist) + " м · " + GridCoord.Format(rs.PosOf(g));
        }

        // ---------- обновление ----------
        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.player == null) return;

            // чат: гаснет сам; пока открыт ввод — виден
            float idle = Time.unscaledTime - lastMsg;
            bool typing = inputRow != null && inputRow.gameObject.activeSelf;
            float a = typing || idle < FadeAfter ? 1f : (idle < HideAfter ? 0.4f : 0f);
            if (chatGroup != null) chatGroup.alpha = Mathf.MoveTowards(chatGroup.alpha, a, Time.unscaledDeltaTime * 2f);

            // миникарта: окно вокруг игрока, север всегда сверху
            var w = gm.world;
            if (w == null) return;
            if (mini.texture == null) mini.texture = w.MakeMap(256);
            Vector2 uv = w.MapUv(gm.player.transform.position);
            const float zoom = 0.22f;
            mini.uvRect = new Rect(uv.x - zoom * 0.5f, uv.y - zoom * 0.5f, zoom, zoom);

            // время суток и температура
            var at = Atmosphere.I;
            if (at != null)
            {
                int hh = Mathf.FloorToInt(at.hour) % 24, mm = Mathf.FloorToInt((at.hour - Mathf.Floor(at.hour)) * 60f);
                clockT.text = hh.ToString("00") + ":" + mm.ToString("00");
                float temp = 14f + 7f * Mathf.Cos((at.hour - 15f) / 24f * Mathf.PI * 2f) - at.Rain * 3f;
                tempT.text = temp.ToString("0.0").Replace('.', ',') + "°C" + (at.IsRaining ? "  дождь" : "");
            }

            UpdateStands();
            if (levelT != null) levelT.text = Game.S.level.ToString();
            if (ringLvl != null)
            {
                int need = Mathf.Max(1, GameData.XpForLevel(Game.S.level));
                ringLvl.text = Game.S.level.ToString();
                ringXp.text = Game.S.xp + "/" + need;
                xpRing.fillAmount = Mathf.Clamp01(Game.S.xp / (float)need);
            }
            if (bagCount != null) bagCount.text = Game.S.fishBag.Count.ToString();
            if (weatherImg != null && at != null) weatherImg.sprite = at.IsRaining ? Icon("rain") : (at.Night > 0.5f ? Icon("moon") : Icon("sun"));

            // сектор: 10×10 клеток карты, внутри клетки ещё 10×10 → «Д4-37»
            coordT.text = GridCoord.Format(uv);
        }
    }
}
