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
        static readonly string[] Cols = { "А", "Б", "В", "Г", "Д", "Е", "Ж", "З", "И", "К" };
        static readonly Color[] NickColors =
        {
            new Color(0.49f, 0.82f, 0.49f), new Color(0.91f, 0.83f, 0.42f), new Color(0.44f, 0.76f, 0.91f),
            new Color(0.94f, 0.63f, 0.35f), new Color(0.91f, 0.54f, 0.72f)
        };

        static Sprite circle;

        RawImage mini; Text clockT, tempT, coordT, chatT, northT;
        CanvasGroup chatGroup; RectTransform chatBox, inputRow;
        InputField input;
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

        public static HudRef Build(RectTransform hud)
        {
            var go = UIKit.R("HUD как на референсе", hud);
            UIKit.Stretch(go);
            var h = go.gameObject.AddComponent<HudRef>();
            I = h;
            h.BuildMini(go);
            h.BuildChat(go);
            h.Post("Система", "Добро пожаловать на озеро!");
            return h;
        }

        // ---------- миникарта ----------
        void BuildMini(RectTransform p)
        {
            const float S = 230f;
            var ring = UIKit.Img(p, new Color(0, 0, 0, 0.55f), false); ring.sprite = Circle;
            UIKit.At(ring.rectTransform, new Vector2(0, 1), new Vector2(20, -20), new Vector2(S + 12, S + 12));

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
                Post("Вы", s);
            }
            inputRow.gameObject.SetActive(false);
        }

        public void Post(string nick, string text)
        {
            Color c = nick == "Вы" ? Color.white : NickColors[Mathf.Abs(nick.GetHashCode()) % NickColors.Length];
            lines.Add("<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + nick + ":</color> " + text);
            while (lines.Count > MaxLines) lines.RemoveAt(0);
            lastMsg = Time.unscaledTime;
            if (chatT != null) chatT.text = string.Join("\n", lines.ToArray());
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

            // сектор: 10×10 клеток карты, внутри клетки ещё 10×10 → «Д4-37»
            float fx = Mathf.Clamp01(uv.x) * 10f, fy = Mathf.Clamp01(uv.y) * 10f;
            int cx = Mathf.Min(9, Mathf.FloorToInt(fx)), cy = Mathf.Min(9, Mathf.FloorToInt(fy));
            int sx = Mathf.Min(9, Mathf.FloorToInt((fx - cx) * 10f)), sy = Mathf.Min(9, Mathf.FloorToInt((fy - cy) * 10f));
            coordT.text = Cols[cx] + (cy + 1) + "-" + sx + sy;
        }
    }
}
