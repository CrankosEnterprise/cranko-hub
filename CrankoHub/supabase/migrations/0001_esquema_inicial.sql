-- CrankoHub: esquema inicial (item 14 do README).
-- Rodar no SQL Editor do Supabase. Cada usuário só enxerga as próprias linhas (RLS).
--
-- Padrão das tabelas de dados:
--   user_id + id (o mesmo id curto do app, NovoId()) formam a PK;
--   atualizado_em é preenchido pelo servidor e serve de cursor para a sincronização;
--   excluido_em marca exclusões (não há delete), para os outros aparelhos saberem o que sumiu.
-- Colunas em snake_case, iguais aos modelos do app (Models/*.cs).

-- ---------------------------------------------------------------------------
-- Trigger: atualizado_em sempre vem do servidor
-- ---------------------------------------------------------------------------
create or replace function public.definir_atualizado_em()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
  new.atualizado_em := now();
  return new;
end;
$$;

-- ---------------------------------------------------------------------------
-- Perfil (uma linha por usuário)
-- ---------------------------------------------------------------------------
create table public.perfis (
  user_id uuid primary key default auth.uid() references auth.users on delete cascade,
  usuario text unique,               -- nome de login (sem o @crankohub.local)
  altura numeric,                    -- Dieta.Perfil.Altura (cm)
  meta numeric,                      -- Dieta.Perfil.Meta (kg)
  tema text,                         -- "light", "dark" ou null
  cor_destaque text,                 -- Config.CorDestaque
  modulos_ocultos text[] not null default '{}',
  atualizado_em timestamptz not null default now()
);

-- ---------------------------------------------------------------------------
-- Dieta
-- ---------------------------------------------------------------------------
create table public.registros_peso (
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  id text not null,
  data date not null,
  peso numeric not null,
  gordura numeric,
  cintura numeric,
  obs text,
  atualizado_em timestamptz not null default now(),
  excluido_em timestamptz,
  primary key (user_id, id)
);

-- ---------------------------------------------------------------------------
-- Carro
-- ---------------------------------------------------------------------------
create table public.veiculos (
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  id text not null,
  nome text not null default '',
  atualizado_em timestamptz not null default now(),
  excluido_em timestamptz,
  primary key (user_id, id)
);

create table public.abastecimentos (
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  id text not null,
  veiculo_id text not null,
  data date not null,
  combustivel text not null default 'gasolina',
  odometro numeric,
  km numeric not null,
  litros numeric not null,
  valor numeric,
  posto text,
  obs text,
  atualizado_em timestamptz not null default now(),
  excluido_em timestamptz,
  primary key (user_id, id),
  foreign key (user_id, veiculo_id) references public.veiculos (user_id, id)
);

create table public.manutencoes (
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  id text not null,
  veiculo_id text not null,
  tipo text not null default 'oleo',
  data date not null,
  odometro numeric,
  valor numeric,
  descricao text,
  local text,
  obs text,
  intervalo_km numeric,
  intervalo_meses integer,
  atualizado_em timestamptz not null default now(),
  excluido_em timestamptz,
  primary key (user_id, id),
  foreign key (user_id, veiculo_id) references public.veiculos (user_id, id)
);

create table public.documentos_carro (
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  id text not null,
  veiculo_id text,                   -- nulo para a CNH (é do motorista, não do carro)
  tipo text not null default 'ipva',
  descricao text,
  vencimento date not null,
  valor numeric,
  pago boolean not null default false,
  obs text,
  atualizado_em timestamptz not null default now(),
  excluido_em timestamptz,
  primary key (user_id, id),
  foreign key (user_id, veiculo_id) references public.veiculos (user_id, id)
);

-- ---------------------------------------------------------------------------
-- Assinaturas e Entretenimento
-- ---------------------------------------------------------------------------
create table public.assinaturas (
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  id text not null,
  nome text not null default '',
  valor numeric not null,
  ciclo text not null default 'mensal',
  vencimento date not null,
  categoria text,
  pagamento text,
  ativa boolean not null default true,
  obs text,
  atualizado_em timestamptz not null default now(),
  excluido_em timestamptz,
  primary key (user_id, id)
);

create table public.entretenimento (
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  id text not null,
  categoria text not null default 'filme',
  titulo text not null default '',
  ano integer,
  data date,
  nota numeric,
  status text not null default 'concluido',
  capa text,                         -- nome do arquivo (bucket "capas" na fase 5)
  sinopse text,
  criadores text,
  generos text,
  fonte text,
  fonte_id text,
  obs text,
  atualizado_em timestamptz not null default now(),
  excluido_em timestamptz,
  primary key (user_id, id)
);

-- ---------------------------------------------------------------------------
-- Tarefas
-- ---------------------------------------------------------------------------
create table public.quadros (
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  id text not null,
  nome text not null default '',
  emoji text,
  ordem integer not null default 0,  -- posição na lista de quadros
  atualizado_em timestamptz not null default now(),
  excluido_em timestamptz,
  primary key (user_id, id)
);

create table public.colunas (
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  id text not null,
  quadro_id text not null,
  nome text not null default '',
  ordem integer not null default 0,  -- a última coluna do quadro é a de concluídos
  atualizado_em timestamptz not null default now(),
  excluido_em timestamptz,
  primary key (user_id, id),
  foreign key (user_id, quadro_id) references public.quadros (user_id, id)
);

create table public.cards (
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  id text not null,
  quadro_id text not null,
  coluna_id text not null,
  titulo text not null default '',
  descricao text,
  prazo date,
  prioridade text not null default 'media',
  etiquetas text[] not null default '{}',
  checklist jsonb not null default '[]',  -- [{ "texto": "...", "feito": false }]
  foco boolean not null default false,
  ordem numeric not null default 0,
  criado date not null,
  concluido date,
  atualizado_em timestamptz not null default now(),
  excluido_em timestamptz,
  primary key (user_id, id),
  foreign key (user_id, quadro_id) references public.quadros (user_id, id),
  foreign key (user_id, coluna_id) references public.colunas (user_id, id)
);

-- ---------------------------------------------------------------------------
-- Trigger, índice do cursor, RLS e permissões (iguais em todas as tabelas)
-- ---------------------------------------------------------------------------
do $$
declare
  t text;
begin
  foreach t in array array[
    'perfis', 'registros_peso', 'veiculos', 'abastecimentos', 'manutencoes', 'documentos_carro',
    'assinaturas', 'entretenimento', 'quadros', 'colunas', 'cards'
  ]
  loop
    execute format(
      'create trigger definir_atualizado_em before insert or update on public.%I
         for each row execute function public.definir_atualizado_em()', t);

    execute format('create index %I on public.%I (user_id, atualizado_em)', t || '_cursor', t);

    execute format('alter table public.%I enable row level security', t);

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
