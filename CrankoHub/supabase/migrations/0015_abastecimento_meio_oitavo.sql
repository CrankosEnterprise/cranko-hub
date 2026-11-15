-- CrankoHub: o ponteiro do marcador de combustível para também no meio de cada oitavo (ex.: 0,5 = entre o vazio e 1/8).
-- Rodar no SQL Editor do Supabase, depois do 0014_tarefas_etiquetas.sql.
-- Enquanto não rodar, o "nivel_antes" fica só no aparelho (não sincroniza) e o card da Conta avisa.
-- Os valores já gravados (0 a 8, em oitavos) continuam valendo como estão.

alter table public.abastecimentos
  drop constraint if exists abastecimentos_nivel_antes_check;

alter table public.abastecimentos
  alter column nivel_antes type numeric using nivel_antes::numeric;

alter table public.abastecimentos
  add constraint abastecimentos_nivel_antes_check
  check (nivel_antes between 0 and 8 and nivel_antes * 2 = trunc(nivel_antes * 2));
