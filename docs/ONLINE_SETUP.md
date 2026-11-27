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
