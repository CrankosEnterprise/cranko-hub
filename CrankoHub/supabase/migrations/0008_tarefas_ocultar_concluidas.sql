-- CrankoHub: configuração das Tarefas (engrenagem na página Tarefas) — esconder concluídas há mais de N dias (0 = mostrar todas).
-- Rodar no SQL Editor do Supabase, depois do 0007_dieta_lembrete_peso.sql.
-- Enquanto não rodar, o app sincroniza todo o resto (o prazo fica só no aparelho) e avisa no card da Conta.
alter table public.perfis
  add column if not exists dias_ocultar_concluidas integer check (dias_ocultar_concluidas >= 0);
