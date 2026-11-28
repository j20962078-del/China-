# Онлайн China Fishing — настройка (этап 1: аккаунты)

Платформа: Supabase (бесплатно, без карты). Всё делается с телефона.

1. supabase.com → New project (регион Frankfurt, пароль базы храните у себя).
2. Authentication → Providers → Email: на время тестов выключите «Confirm email».
3. Authentication → Email Templates → «Reset Password»: в тело письма добавьте строку `Код: {{ .Token }}`
   (игра принимает 6-значный код из письма, ссылка не нужна).
4. SQL Editor → New query → вставьте весь файл `supabase/01_accounts.sql` → Run. Должно быть «Success».
5. Project Settings → API: скопируйте Project URL и anon/publishable key (публичные значения).
6. В репозитории откройте `Assets/Resources/server_config.json` и впишите их в `url` и `anonKey`.
   Ключ service_role НЕ вписывать никуда.
7. Запустите облачную сборку APK.

Пока `server_config.json` пуст, игра показывает «Онлайн-сервер не настроен» и позволяет играть без аккаунта.

## Этап 2: общий чат

1. Supabase → SQL Editor → New query → вставьте весь файл `supabase/02_chat.sql` → Run («Success»).
2. Больше ничего настраивать не нужно: чат использует тот же `server_config.json`.
3. В игре писать в общий чат могут только вошедшие в аккаунт. Без входа сообщения остаются локальными.
4. Ник сообщения ставит сервер из профиля, спам режется (1 сообщение в 2 секунды, до 120 символов).
5. Старые сообщения можно чистить командой `select public.chat_cleanup();` (удаляет старше 3 суток).
