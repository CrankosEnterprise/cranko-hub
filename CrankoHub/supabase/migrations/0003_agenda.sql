-- CrankoHub: Agenda (calendário com agendas coloridas, eventos, repetição e lembretes).
-- Rodar no SQL Editor do Supabase, depois do 0001_esquema_inicial.sql e do 0002_capas.sql.
-- Enquanto não rodar, o app sincroniza todo o resto e avisa no card da Conta que faltam estas tabelas.
--
-- Mesmo padrão das outras tabelas (ver 0001):
--   user_id + id (o id curto do app) formam a PK; a agenda criada sozinha tem o id fixo "principal";
--   atualizado_em vem do servidor e é o cursor da sincronização; excluido_em marca exclusões (não há delete).
-- Os horários são do relógio local (timestamp sem fuso), como numa agenda de papel.

-- ---------------------------------------------------------------------------
-- Agenda
-- ---------------------------------------------------------------------------
create table public.agendas (
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  id text not null,
  nome text not null default '',
  cor text not null default '#007aff',
  ordem integer not null default 0,     -- posição na lateral da Agenda
  atualizado_em timestamptz not null default now(),
  excluido_em timestamptz,
  primary key (user_id, id)
);
alter table public.agendas enable row level security;

create table public.eventos (
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  id text not null,
  agenda_id text not null,
  titulo text not null default '',
  descricao text,
  local text,
  inicio timestamp not null,            -- início da primeira ocorrência (dia inteiro: 00:00)
  fim timestamp not null,               -- fim da primeira ocorrência (dia inteiro: 00:00 do dia seguinte)
  dia_inteiro boolean not null default false,
  repeticao text not null default '',   -- '', 'diaria', 'semanal', 'mensal' ou 'anual'
  intervalo integer not null default 1, -- a cada N dias/semanas/meses/anos
  dias_semana integer[] not null default '{}', -- repetição semanal: 0 = domingo ... 6 = sábado
  repetir_ate date,                     -- último dia da repetição (null = para sempre)
  excecoes date[] not null default '{}',-- ocorrências excluídas ou editadas "só este evento"
  lembrete integer,                     -- minutos antes (null = sem lembrete)
  criado date not null,
  atualizado_em timestamptz not null default now(),
  excluido_em timestamptz,
  primary key (user_id, id),
  foreign key (user_id, agenda_id) references public.agendas (user_id, id)
);
alter table public.eventos enable row level security;

-- ---------------------------------------------------------------------------
-- Trigger, índice do cursor, políticas e permissões (iguais às do 0001)
-- ---------------------------------------------------------------------------
do $$
declare
  t text;
begin
  foreach t in array array['agendas', 'eventos']
  loop
    execute format(
      'create trigger definir_atualizado_em before insert or update on public.%I
         for each row execute function public.definir_atualizado_em()', t);

    execute format('create index %I on public.%I (user_id, atualizado_em)', t || '_cursor', t);


    execute format(
      'create policy "dono le" on public.%I for select to authenticated
         using ((select auth.uid()) = user_id)', t);
    execute format(
      'create policy "dono insere" on public.%I for insert to authenticated
         with check ((select auth.uid()) = user_id)', t);
    execute format(
      'create policy "dono altera" on public.%I for update to authenticated
         using ((select auth.uid()) = user_id)
         with check ((select auth.uid()) = user_id)', t);

    -- Sem política nem permissão de delete: exclusão é feita com excluido_em
    execute format('revoke all on public.%I from anon, authenticated', t);
    execute format('grant select, insert, update on public.%I to authenticated', t);
  end loop;
end;
$$;
