using System;
using System.Collections;
using UnityEngine;

namespace ChinaFishing
{
    // Общий чат через Supabase REST (опрос раз в ~3 с, без WebSocket — проще и надёжнее на мобильной сети).
    // Требует: supabase/02_chat.sql выполнен, игрок вошёл в аккаунт.
    public class ChatClient : MonoBehaviour
    {
        [Serializable] class Row { public long id; public string nickname; public string body; }
        [Serializable] class RowList { public Row[] items; }

        public static ChatClient I;
        const float PollSec = 3f, SendCooldown = 2f;

        HudRef hud;
        long lastId;
        bool firstLoad = true, polling, errShown;
        float nextSend;

        public static ChatClient Create(HudRef h)
        {
            var c = h.gameObject.AddComponent<ChatClient>();
            c.hud = h; I = c;
            return c;
        }

        public bool Online
        {
            get
            {
                var a = AuthManager.I;
                return ServerConfig.IsConfigured && a != null && a.LoggedIn && !a.Offline;
            }
        }

        void OnEnable() { StartCoroutine(Loop()); }

        IEnumerator Loop()
        {
            while (true)
            {
                if (Online && !polling) Poll();
                yield return new WaitForSecondsRealtime(PollSec);
            }
        }

        void Poll()
        {
            polling = true;
            var a = AuthManager.I;
            a.GetToken(tok =>
            {
                if (tok == null) { polling = false; return; }
                string q = firstLoad
                    ? "/rest/v1/chat_messages?select=id,nickname,body&channel=eq.global&order=id.desc&limit=20"
                    : "/rest/v1/chat_messages?select=id,nickname,body&channel=eq.global&order=id.asc&limit=50&id=gt." + lastId;
                Http.Send("GET", q, null, tok, r =>
                {
                    polling = false;
                    if (!r.ok)
                    {
                        // 404 — таблица не создана (не выполнен 02_chat.sql); сообщаем один раз
                        if (!errShown && r.code == 404) { hud.Post("Система", "Общий чат ещё не настроен на сервере."); errShown = true; }
                        return;
                    }
                    try
                    {
                        var l = JsonUtility.FromJson<RowList>("{\"items\":" + r.body + "}");
                        if (l == null || l.items == null) return;
                        var rows = l.items;
                        if (firstLoad) Array.Reverse(rows);   // при первой загрузке пришли от новых к старым
                        foreach (var row in rows)
                        {
                            if (row.id > lastId) lastId = row.id;
                            hud.Post(row.nickname, row.body, row.nickname == a.Nick);
                        }
                        firstLoad = false;
                    }
                    catch (Exception e) { Debug.LogWarning("Чат: ответ не разобран: " + e.Message); }
                });
            });
        }

        // Возвращает false, если сообщение не отправлено (нет входа / слишком часто)
        public bool Send(string text)
        {
            if (!Online) return false;
            if (Time.unscaledTime < nextSend) { hud.Post("Система", "Не так быстро, подождите секунду."); return true; }
            nextSend = Time.unscaledTime + SendCooldown;
            string body = text.Replace("\\", "\\\\").Replace("\"", "\\\"");
            AuthManager.I.GetToken(tok =>
            {
                if (tok == null) return;
                Http.Send("POST", "/rest/v1/chat_messages", "{\"body\":\"" + body + "\"}", tok, r =>
                {
                    if (!r.ok) { hud.Post("Система", r.network ? "Нет связи, сообщение не отправлено." : "Сообщение не отправлено."); return; }
                    if (!polling) Poll();   // сразу подтянуть своё сообщение
                });
            });
            return true;
        }
    }
}
