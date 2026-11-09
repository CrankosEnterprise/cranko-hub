-- CrankoHub: ficha do veículo (marca, ano-modelo e ficha técnica opcional, preenchida à mão ou pela FIPE).
-- Rodar no SQL Editor do Supabase, depois do 0005_manutencao_servico.sql.
-- Enquanto não rodar, o app sincroniza todo o resto (marca, ano e ficha ficam só no aparelho) e avisa no card da Conta.
alter table public.veiculos
  add column if not exists marca text,
  add column if not exists ano integer,
  add column if not exists ficha jsonb;
