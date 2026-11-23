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
            string line = msg.Length > 420 ? msg.Substring(0, 420) + "…" : msg;
            if (type == LogType.Exception && !string.IsNullOrEmpty(stack))
            {
                int nl = stack.IndexOf('\n');
                line += "\n" + (nl > 0 ? stack.Substring(0, nl) : stack);
            }
            errors.Add(line);
            if (errors.Count > 5) errors.RemoveAt(0);
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
