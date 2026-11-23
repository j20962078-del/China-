using System.Collections.Generic;
using UnityEngine;

namespace ChinaFishing
{
    // Запасной запуск и экран диагностики.
    // 1) Если в сцене по какой-то причине нет GameManager — создаёт его сам.
    // 2) Пока игра грузится, пишет «загрузка», а при любой ошибке показывает её текст на экране
    //    (чтобы по скриншоту сразу было видно, что пошло не так). Когда игра станет стабильной, оверлей можно убрать.
    public class BootOverlay : MonoBehaviour
    {
        static readonly List<string> errors = new List<string>();
        static BootOverlay inst;
        float t0;
        GUIStyle info, err;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (inst == null)
            {
                var g = new GameObject("Диагностика");
                DontDestroyOnLoad(g);
                inst = g.AddComponent<BootOverlay>();
            }
            if (GameManager.I == null && Object.FindAnyObjectByType<GameManager>() == null)
                new GameObject("China Fishing").AddComponent<GameManager>();
        }

        void OnEnable() { Application.logMessageReceived += OnLog; t0 = Time.realtimeSinceStartup; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            int nl0 = msg.IndexOf('\n');
            string line = nl0 > 0 ? msg.Substring(0, nl0) : msg;
            if (line.Length > 260) line = line.Substring(0, 260) + "…";
            if (type == LogType.Exception && !string.IsNullOrEmpty(stack))
            {
                // где именно произошла ошибка: первая строка стека (метод в нашем коде)
                foreach (var l in stack.Split('\n'))
                    if (l.Contains("ChinaFishing")) { line += "\n   → " + (l.Length > 160 ? l.Substring(0, 160) : l); break; }
            }
            if (errors.Count > 0 && errors[errors.Count - 1] == line) return;   // одинаковые подряд не повторяем
            errors.Add(line);
            if (errors.Count > 6) errors.RemoveAt(0);
        }

        void OnGUI()
        {
            if (info == null)
            {
                int fs = Mathf.Max(22, Screen.height / 34);
                info = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true };
                info.normal.textColor = Color.white;
                err = new GUIStyle(info);
                err.normal.textColor = new Color(1f, 0.45f, 0.4f);
            }
            bool ready = GameManager.I != null && GameManager.I.Ready;
            float top = 16f;
            if (!ready && Time.realtimeSinceStartup - t0 < 25f)
            {
                GUI.Label(new Rect(20, top, Screen.width - 40, Screen.height / 8f), "China Fishing: загрузка…", info);
                top += Screen.height / 8f;
            }
            if (errors.Count > 0)
                GUI.Label(new Rect(20, top, Screen.width - 40, Screen.height - top - 10), "Ошибки:\n" + string.Join("\n\n", errors.ToArray()), err);
        }
    }
}
