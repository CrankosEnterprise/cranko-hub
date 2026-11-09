-- CrankoHub: nome do serviço quando a manutenção é "Outro serviço" (ex.: "Troca da correia dentada").
-- Rodar no SQL Editor do Supabase, depois do 0004_tarefas_hora.sql.
-- Enquanto não rodar, o app sincroniza todo o resto (o nome do serviço fica só no aparelho) e avisa no card da Conta.
alter table public.manutencoes add column if not exists servico text;
