-- China Fishing — этап 2: общий чат.
-- Выполнить ОДИН раз: Supabase → SQL Editor → New query → вставить всё → Run.
-- Нужен уже выполненный 01_accounts.sql (таблица profiles).

create table if not exists public.chat_messages (
  id         bigint generated always as identity primary key,
  user_id    uuid not null default auth.uid() references auth.users(id) on delete cascade,
  nickname   text not null default '',
  channel    text not null default 'global',
  body       text not null,
  created_at timestamptz not null default now(),
  constraint chat_body_len check (char_length(body) between 1 and 120)
);

create index if not exists chat_messages_channel_id on public.chat_messages (channel, id desc);

-- Ник берётся с сервера из профиля (подделать с клиента нельзя), текст чистится, спам режется.
create or replace function public.chat_before_insert() returns trigger
language plpgsql security definer set search_path = public as $$
declare nick text;
begin
  select nickname into nick from public.profiles where id = new.user_id;
  if nick is null then raise exception 'no_profile'; end if;
  new.nickname := nick;
  new.channel  := 'global';
  new.body     := btrim(regexp_replace(new.body, '[\r\n\t]+', ' ', 'g'));
  if char_length(new.body) < 1 then raise exception 'empty'; end if;
  if exists (select 1 from public.chat_messages
             where user_id = new.user_id and created_at > now() - interval '2 seconds') then
    raise exception 'rate_limit';
  end if;
  return new;
end $$;

drop trigger if exists chat_before_insert on public.chat_messages;
create trigger chat_before_insert before insert on public.chat_messages
  for each row execute function public.chat_before_insert();

alter table public.chat_messages enable row level security;

-- Читать могут вошедшие игроки; писать — только от своего имени и только поле body.
revoke all on public.chat_messages from anon, authenticated;
grant select on public.chat_messages to authenticated;
grant insert (body) on public.chat_messages to authenticated;

drop policy if exists "chat read"   on public.chat_messages;
drop policy if exists "chat insert" on public.chat_messages;
create policy "chat read"   on public.chat_messages for select to authenticated using (channel = 'global');
create policy "chat insert" on public.chat_messages for insert to authenticated with check (user_id = auth.uid());

-- Очистка: оставлять сообщения за 3 суток (запускать вручную или повесить на pg_cron).
create or replace function public.chat_cleanup() returns void
language sql security definer set search_path = public as $$
  delete from public.chat_messages where created_at < now() - interval '3 days';
$$;
revoke all on function public.chat_cleanup() from anon, authenticated;
