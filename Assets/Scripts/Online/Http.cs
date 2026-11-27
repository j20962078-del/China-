using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ChinaFishing
{
    public class ApiResult
    {
        public bool ok;          // HTTP 2xx
        public bool network;     // нет связи / таймаут (сервер не ответил)
        public long code;        // HTTP-код
        public string body = "";
    }

    // Носитель корутин для сетевых запросов (создаётся один раз, живёт между сценами)
    public class HttpRunner : MonoBehaviour { }

    // Единая точка HTTP-запросов к серверу. Всегда с таймаутом, без блокировки главного потока.
    public static class Http
    {
        static HttpRunner runner;

        static HttpRunner R
        {
            get
            {
                if (runner == null)
                {
                    var g = new GameObject("Сеть");
                    UnityEngine.Object.DontDestroyOnLoad(g);
                    runner = g.AddComponent<HttpRunner>();
                }
                return runner;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { runner = null; }

        public static void Send(string method, string path, string json, string bearer, Action<ApiResult> done, int timeoutSec = 15)
        {
            R.StartCoroutine(Do(method, path, json, bearer, done, timeoutSec));
        }

        static IEnumerator Do(string method, string path, string json, string bearer, Action<ApiResult> done, int timeoutSec)
        {
            var res = new ApiResult();
            if (!ServerConfig.IsConfigured) { res.network = true; if (done != null) done(res); yield break; }

            using (var req = new UnityWebRequest(ServerConfig.Url + path, method))
            {
                if (!string.IsNullOrEmpty(json))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = timeoutSec;
                string key = ServerConfig.AnonKey;
                req.SetRequestHeader("apikey", key);
                // Новые ключи вида sb_publishable_... не являются JWT и в Authorization не передаются
                if (!string.IsNullOrEmpty(bearer)) req.SetRequestHeader("Authorization", "Bearer " + bearer);
                else if (!key.StartsWith("sb_")) req.SetRequestHeader("Authorization", "Bearer " + key);

                yield return req.SendWebRequest();

                res.code = req.responseCode;
                res.body = req.downloadHandler != null ? (req.downloadHandler.text ?? "") : "";
                res.ok = req.result == UnityWebRequest.Result.Success;
                res.network = req.result == UnityWebRequest.Result.ConnectionError;
                if (!res.ok) Debug.LogWarning("HTTP " + method + " " + path + " -> " + res.code + " " + req.error + " " + res.body);
            }
            if (done != null) done(res);
        }
    }
}
