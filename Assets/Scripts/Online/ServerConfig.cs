using System;
using UnityEngine;

namespace ChinaFishing
{
    // Адрес сервера и публичный ключ читаются из Assets/Resources/server_config.json.
    // Здесь только ПУБЛИЧНЫЕ значения (Project URL и anon/publishable key). service_role-ключ сюда нельзя.
    public static class ServerConfig
    {
        [Serializable] class Raw { public string url; public string anonKey; public bool requireTerms; }

        static bool loaded;
        static string url = "", key = "";
        static bool terms;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                var ta = Resources.Load<TextAsset>("server_config");
                if (ta == null) return;
                var r = JsonUtility.FromJson<Raw>(ta.text);
                if (r == null) return;
                url = (r.url ?? "").Trim().TrimEnd('/');
                key = (r.anonKey ?? "").Trim();
                terms = r.requireTerms;
            }
            catch (Exception e) { Debug.LogWarning("server_config.json не прочитан: " + e.Message); }
        }

        public static string Url { get { Load(); return url; } }
        public static string AnonKey { get { Load(); return key; } }
        public static bool RequireTerms { get { Load(); return terms; } }
        public static bool IsConfigured
        {
            get { Load(); return url.StartsWith("https://") && key.Length > 20; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { loaded = false; url = ""; key = ""; terms = false; }
    }
}
