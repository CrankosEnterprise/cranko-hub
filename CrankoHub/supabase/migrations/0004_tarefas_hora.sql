-- CrankoHub: horário de entrega nas tarefas (aparece na grade de horas da Agenda).
-- Rodar no SQL Editor do Supabase, depois do 0003_agenda.sql.
-- Enquanto não rodar, o app sincroniza todo o resto (o horário fica só no aparelho) e avisa no card da Conta.
--
-- A tabela cards já tem RLS, políticas e permissões (0001): só ganha a coluna nova.

alter table public.cards
  add column if not exists prazo_hora time; -- hora da entrega no dia do prazo (null = sem hora marcada)
