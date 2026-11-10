-- CrankoHub: configuração da Dieta (engrenagem na página Dieta) — lembrete de pesar depois de N dias sem registro.
-- Rodar no SQL Editor do Supabase, depois do 0006_veiculo_ficha.sql.
-- Enquanto não rodar, o app sincroniza todo o resto (o prazo fica só no aparelho) e avisa no card da Conta.
alter table public.perfis
  add column if not exists dias_lembrete_peso integer check (dias_lembrete_peso >= 0);
