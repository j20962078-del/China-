using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ChinaFishing
{
    public class AuthResult
    {
        public bool ok;
        public bool offline;       // вошли по сохранённой сессии без связи
        public string message = "";
    }

    [Serializable]
    public class PlayerProfile
    {
        public long player_no;
        public string nickname;
        public int level;
        public long xp;
        public string created_at;
    }

    // Аккаунты: регистрация, вход, восстановление сессии, выход, смена пароля, сброс пароля по коду из письма.
    // Пароль нигде не сохраняется. На устройстве хранится только refresh-токен сессии.
    public class AuthManager : MonoBehaviour
    {
        public static AuthManager I;

        [Serializable] class Session { public string access, refresh, userId, email, nick; public long expires, playerNo; }
        [Serializable] class UserObj { public string id, email; }
        [Serializable] class TokenResp { public string access_token, refresh_token; public long expires_in, expires_at; public UserObj user; }
        [Serializable] class ErrResp { public string error_code, msg, message, error, error_description; }
        [Serializable] class ProfileList { public PlayerProfile[] items; }

        const string PrefKey = "cf_auth_session";

        public PlayerProfile Profile;
        public bool Offline;
        public event Action Changed;

        Session s;
        bool busy, refreshing;
        readonly List<Action<string>> waiters = new List<Action<string>>();

        public bool LoggedIn { get { return s != null && !string.IsNullOrEmpty(s.refresh); } }
        public string Nick { get { return Profile != null ? Profile.nickname : (s != null ? s.nick : ""); } }
        public long PlayerNo { get { return Profile != null ? Profile.player_no : (s != null ? s.playerNo : 0); } }
        public string UserId { get { return s != null ? s.userId : ""; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        public static AuthManager Create()
        {
            if (I != null) return I;
            var g = new GameObject("Аккаунт-сервис");
            DontDestroyOnLoad(g);
            I = g.AddComponent<AuthManager>();
            I.LoadSession();
            return I;
        }

        void OnDestroy() { if (I == this) I = null; }

        // ---------------- ПРОВЕРКА ВВОДА ----------------
        static readonly Regex NickRx = new Regex("^[A-Za-zА-Яа-яЁё0-9_]{3,16}$");
        static readonly Regex MailRx = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$");

        public static string ValidateNick(string n)
        {
            if (string.IsNullOrEmpty(n) || n.Length < 3 || n.Length > 16) return "Никнейм: от 3 до 16 символов.";
            if (!NickRx.IsMatch(n)) return "В никнейме можно только буквы, цифры и «_».";
            return null;
        }
        public static string ValidateEmail(string e)
        {
            if (string.IsNullOrEmpty(e) || e.Length > 100 || !MailRx.IsMatch(e)) return "Введите корректную почту.";
            return null;
        }
        public static string ValidatePassword(string p)
        {
            if (string.IsNullOrEmpty(p) || p.Length < 8) return "Пароль: минимум 8 символов.";
            if (!Regex.IsMatch(p, "[A-Za-zА-Яа-я]") || !Regex.IsMatch(p, "[0-9]")) return "В пароле нужны буквы и цифры.";
            if (p.Length > 64) return "Пароль слишком длинный.";
            return null;
        }

        // ---------------- ОШИБКИ ПО-РУССКИ ----------------
        static string Err(ApiResult r)
        {
            if (r.network) return "Нет соединения с сервером. Проверьте интернет.";
            ErrResp e = null;
            try { e = JsonUtility.FromJson<ErrResp>(r.body); } catch (Exception) { }
            string c = e != null ? (e.error_code ?? "") : "";
            string m = e != null ? ((e.msg ?? e.message ?? e.error_description ?? "")) : "";
            if (c == "invalid_credentials") return "Неверная почта или пароль.";
            if (c == "user_already_exists" || c == "email_exists") return "Эта почта уже зарегистрирована.";
            if (c == "weak_password") return "Слишком простой пароль.";
            if (c == "email_not_confirmed") return "Подтвердите почту по ссылке из письма.";
            if (c == "validation_failed" || c == "email_address_invalid") return "Проверьте правильность почты и пароля.";
            if (c == "over_request_rate_limit" || c == "over_email_send_rate_limit" || r.code == 429) return "Слишком много попыток. Подождите немного.";
            if (c == "otp_expired" || c == "bad_code") return "Код неверный или устарел.";
            if (c == "same_password") return "Новый пароль совпадает со старым.";
            if (c == "unexpected_failure" || m.Contains("Database error")) return "Этот никнейм занят или недопустим. Выберите другой.";
            if (r.code >= 500) return "Сервер временно недоступен. Попробуйте позже.";
            return "Не удалось выполнить действие (код " + r.code + ").";
        }

        // ---------------- СЕССИЯ НА УСТРОЙСТВЕ ----------------
        void LoadSession()
        {
            try
            {
                var j = PlayerPrefs.GetString(PrefKey, "");
                if (!string.IsNullOrEmpty(j)) s = JsonUtility.FromJson<Session>(j);
            }
            catch (Exception) { s = null; }
        }

        void SaveSession()
        {
            if (s == null) { PlayerPrefs.DeleteKey(PrefKey); }
            else PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(s));
            PlayerPrefs.Save();
        }

        void ClearSession()
        {
            s = null; Profile = null; Offline = false;
            SaveSession();
            if (Changed != null) Changed();
        }

        bool ApplyToken(string body)
        {
            TokenResp t = null;
            try { t = JsonUtility.FromJson<TokenResp>(body); } catch (Exception) { }
            if (t == null || string.IsNullOrEmpty(t.access_token) || string.IsNullOrEmpty(t.refresh_token)) return false;
            if (s == null) s = new Session();
            s.access = t.access_token; s.refresh = t.refresh_token;
            s.expires = t.expires_at > 0 ? t.expires_at : DateTimeOffset.UtcNow.ToUnixTimeSeconds() + t.expires_in;
            if (t.user != null) { if (!string.IsNullOrEmpty(t.user.id)) s.userId = t.user.id; if (!string.IsNullOrEmpty(t.user.email)) s.email = t.user.email; }
            SaveSession();
            return true;
        }

        // Действующий токен для других систем (сохранения, чат). Сам обновляется при необходимости.
        public void GetToken(Action<string> cb)
        {
            if (!LoggedIn) { cb(null); return; }
            if (!string.IsNullOrEmpty(s.access) && s.expires - DateTimeOffset.UtcNow.ToUnixTimeSeconds() > 60) { cb(s.access); return; }
            waiters.Add(cb);
            if (refreshing) return;
            refreshing = true;
            Http.Send("POST", "/auth/v1/token?grant_type=refresh_token", "{\"refresh_token\":\"" + s.refresh + "\"}", null, r =>
            {
                refreshing = false;
                string tok = null;
                if (r.ok && ApplyToken(r.body)) tok = s.access;
                else if (!r.network && (r.code == 400 || r.code == 401 || r.code == 403)) ClearSession(); // сессия отозвана
                var list = new List<Action<string>>(waiters); waiters.Clear();
                foreach (var w in list) w(tok);
            });
        }

        // ---------------- ПРОФИЛЬ ----------------
        void LoadProfile(Action<bool, bool> cb) // (успех, нет связи)
        {
            GetToken(tok =>
            {
                if (tok == null) { cb(false, LoggedIn); return; }
                Http.Send("GET", "/rest/v1/profiles?select=player_no,nickname,level,xp,created_at&limit=1", null, tok, r =>
                {
                    if (!r.ok) { cb(false, r.network); return; }
                    try
                    {
                        var l = JsonUtility.FromJson<ProfileList>("{\"items\":" + r.body + "}");
                        if (l != null && l.items != null && l.items.Length > 0)
                        {
                            Profile = l.items[0];
                            s.nick = Profile.nickname; s.playerNo = Profile.player_no; SaveSession();
                            Http.Send("POST", "/rest/v1/rpc/touch_login", "{}", tok, null);
                            cb(true, false); return;
                        }
                    }
                    catch (Exception e) { Debug.LogWarning("Профиль не разобран: " + e.Message); }
                    cb(false, false);
                });
            });
        }

        void Finish(Action<AuthResult> cb, AuthResult res) { busy = false; if (Changed != null) Changed(); if (cb != null) cb(res); }

        // ---------------- РЕГИСТРАЦИЯ ----------------
        public void Register(string nick, string email, string pass, Action<AuthResult> cb)
        {
            if (busy) return;
            string v = ValidateNick(nick) ?? ValidateEmail(email) ?? ValidatePassword(pass);
            if (v != null) { cb(new AuthResult { message = v }); return; }
            busy = true;
            Http.Send("POST", "/rest/v1/rpc/nickname_available", "{\"nick\":\"" + nick + "\"}", null, r1 =>
            {
                if (!r1.ok) { Finish(cb, new AuthResult { message = Err(r1) }); return; }
                if (r1.body.Trim() != "true") { Finish(cb, new AuthResult { message = "Этот никнейм занят. Выберите другой." }); return; }
                string body = "{\"email\":\"" + Esc(email) + "\",\"password\":\"" + Esc(pass) + "\",\"data\":{\"nickname\":\"" + nick + "\"}}";
                Http.Send("POST", "/auth/v1/signup", body, null, r2 =>
                {
                    if (!r2.ok) { Finish(cb, new AuthResult { message = Err(r2) }); return; }
                    if (!ApplyToken(r2.body)) // подтверждение почты включено на сервере
                    { Finish(cb, new AuthResult { ok = true, message = "Аккаунт создан. Подтвердите почту по ссылке из письма и войдите." }); return; }
                    LoadProfile((ok, net) =>
                    {
                        if (ok) Finish(cb, new AuthResult { ok = true, message = "Аккаунт создан!" });
                        else Finish(cb, new AuthResult { message = net ? "Аккаунт создан, но нет связи. Войдите позже." : "Аккаунт создан, но профиль не загрузился. Войдите заново." });
                    });
                });
            });
        }

        // ---------------- ВХОД ----------------
        public void Login(string email, string pass, Action<AuthResult> cb)
        {
            if (busy) return;
            string v = ValidateEmail(email);
            if (v == null && string.IsNullOrEmpty(pass)) v = "Введите пароль.";
            if (v != null) { cb(new AuthResult { message = v }); return; }
            busy = true;
            Http.Send("POST", "/auth/v1/token?grant_type=password", "{\"email\":\"" + Esc(email) + "\",\"password\":\"" + Esc(pass) + "\"}", null, r =>
            {
                if (!r.ok || !ApplyToken(r.body)) { Finish(cb, new AuthResult { message = Err(r) }); return; }
                Offline = false;
                LoadProfile((ok, net) =>
                {
                    if (ok) Finish(cb, new AuthResult { ok = true });
                    else Finish(cb, new AuthResult { message = net ? "Нет соединения с сервером." : "Профиль не найден. Обратитесь в поддержку." });
                });
            });
        }

        // ---------------- АВТО-ВХОД ПРИ ЗАПУСКЕ ----------------
        public void Restore(Action<AuthResult> cb)
        {
            if (!LoggedIn) { cb(new AuthResult()); return; }
            busy = true;
            s.expires = 0; // принудительно проверить сессию на сервере
            LoadProfile((ok, net) =>
            {
                if (ok) { Offline = false; Finish(cb, new AuthResult { ok = true }); return; }
                if (net && LoggedIn) { Offline = true; Finish(cb, new AuthResult { ok = true, offline = true, message = "Нет связи: офлайн-режим." }); return; }
                Finish(cb, new AuthResult { message = LoggedIn ? "Не удалось загрузить профиль." : "Сессия истекла. Войдите снова." });
            });
        }

        // ---------------- ВЫХОД ----------------
        public void Logout()
        {
            string tok = s != null ? s.access : null;
            if (!string.IsNullOrEmpty(tok)) Http.Send("POST", "/auth/v1/logout", "{}", tok, null);
            ClearSession();
        }

        // ---------------- ПАРОЛЬ ----------------
        public void ChangePassword(string newPass, Action<AuthResult> cb)
        {
            if (busy) return;
            string v = ValidatePassword(newPass);
            if (v != null) { cb(new AuthResult { message = v }); return; }
            busy = true;
            GetToken(tok =>
            {
                if (tok == null) { Finish(cb, new AuthResult { message = "Нужно войти заново." }); return; }
                Http.Send("PUT", "/auth/v1/user", "{\"password\":\"" + Esc(newPass) + "\"}", tok, r =>
                    Finish(cb, r.ok ? new AuthResult { ok = true, message = "Пароль изменён." } : new AuthResult { message = Err(r) }));
            });
        }

        // Шаг 1 сброса: на почту приходит письмо с кодом
        public void SendResetCode(string email, Action<AuthResult> cb)
        {
            if (busy) return;
            string v = ValidateEmail(email);
            if (v != null) { cb(new AuthResult { message = v }); return; }
            busy = true;
            Http.Send("POST", "/auth/v1/recover", "{\"email\":\"" + Esc(email) + "\"}", null, r =>
                Finish(cb, r.ok || r.code == 200 ? new AuthResult { ok = true, message = "Если почта зарегистрирована, письмо с кодом отправлено." } : new AuthResult { message = Err(r) }));
        }

        // Шаг 2 сброса: код из письма + новый пароль
        public void ResetWithCode(string email, string code, string newPass, Action<AuthResult> cb)
        {
            if (busy) return;
            string v = ValidateEmail(email) ?? ValidatePassword(newPass);
            if (v == null && (string.IsNullOrEmpty(code) || code.Trim().Length < 6)) v = "Введите код из письма.";
            if (v != null) { cb(new AuthResult { message = v }); return; }
            busy = true;
            Http.Send("POST", "/auth/v1/verify", "{\"type\":\"recovery\",\"email\":\"" + Esc(email) + "\",\"token\":\"" + Esc(code.Trim()) + "\"}", null, r =>
            {
                if (!r.ok || !ApplyToken(r.body)) { Finish(cb, new AuthResult { message = Err(r) }); return; }
                Http.Send("PUT", "/auth/v1/user", "{\"password\":\"" + Esc(newPass) + "\"}", s.access, r2 =>
                {
                    if (!r2.ok) { ClearSession(); Finish(cb, new AuthResult { message = Err(r2) }); return; }
                    ClearSession(); // после сброса просим войти с новым паролем
                    Finish(cb, new AuthResult { ok = true, message = "Пароль изменён. Войдите с новым паролем." });
                });
            });
        }

        static string Esc(string x)
        {
            return (x ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "").Replace("\r", "").Replace("\t", " ");
        }
    }
}
