-- CrankoHub: orçamentos e notas fiscais (PDF ou foto) nos serviços de manutenção do Carro.
-- Rodar no SQL Editor do Supabase, depois do 0009_tarefas_anexos.sql (que cria o bucket "anexos", usado aqui também).
-- Enquanto não rodar, o app sincroniza todo o resto (esses anexos ficam só no aparelho) e avisa no card da Conta.
alter table public.manutencoes
  add column if not exists anexos jsonb not null default '[]';  -- mesmo formato de cards.anexos, com "categoria": "orcamento" | "nota_fiscal" | null
