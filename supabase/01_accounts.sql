-- China Fishing — этап 1: профили игроков.
-- Выполнить ОДИН раз: Supabase → SQL Editor → New query → вставить всё → Run.

create table if not exists public.profiles (
  id          uuid primary key references auth.users(id) on delete cascade,
  player_no   bigint generated always as identity (start with 100000) unique not null, -- Player ID, не меняется
  nickname    text not null,
  avatar      int  not null default 0,
  level       int  not null default 1,
  xp          bigint not null default 0,
  status      text not null default 'offline',
  created_at  timestamptz not null default now(),
  last_login  timestamptz,
  last_seen   timestamptz,
  save_version int not null default 0,
  constraint nickname_format check (nickname ~ '^[A-Za-zА-Яа-яЁё0-9_]{3,16}$')
);

create unique index if not exists profiles_nickname_lower on public.profiles (lower(nickname));

alter table public.profiles enable row level security;

-- Клиент может ТОЛЬКО читать свою строку. Менять уровень, опыт, баланс напрямую нельзя.
revoke all on public.profiles from anon, authenticated;
grant select on public.profiles to authenticated;
drop policy if exists "own profile read" on public.profiles;
create policy "own profile read" on public.profiles for select to authenticated using (id = auth.uid());

-- Зарезервированные никнеймы
create or replace function public.nick_reserved(n text) returns boolean
language sql immutable as $$
  select lower(n) in ('admin','administrator','moderator','system','support',
                      'админ','администратор','модератор','система','поддержка');
$$;

-- Профиль создаётся автоматически при регистрации (никнейм берётся из данных регистрации)
create or replace function public.handle_new_user() returns trigger
language plpgsql security definer set search_path = public as $$
declare nick text := coalesce(new.raw_user_meta_data->>'nickname', '');
begin
  if public.nick_reserved(nick) then raise exception 'reserved_nickname'; end if;
  insert into public.profiles (id, nickname) values (new.id, nick);
  return new;
end $$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created after insert on auth.users
  for each row execute function public.handle_new_user();

-- Проверка занятости никнейма до регистрации
create or replace function public.nickname_available(nick text) returns boolean
language sql security definer set search_path = public stable as $$
  select nick ~ '^[A-Za-zА-Яа-яЁё0-9_]{3,16}$'
     and not public.nick_reserved(nick)
     and not exists (select 1 from public.profiles where lower(nickname) = lower(nick));
$$;

-- Отметка входа
create or replace function public.touch_login() returns void
language sql security definer set search_path = public as $$
  update public.profiles set last_login = now(), last_seen = now() where id = auth.uid();
$$;

revoke execute on function public.nickname_available(text) from public;
revoke execute on function public.touch_login() from public;
grant execute on function public.nickname_available(text) to anon, authenticated;
grant execute on function public.touch_login() to authenticated;
