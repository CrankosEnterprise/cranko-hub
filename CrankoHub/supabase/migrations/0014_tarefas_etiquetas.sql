-- CrankoHub: etiquetas das Tarefas criadas antes de usar (botão de etiqueta no topo das Tarefas e menu de etiquetas do card).
-- Rodar no SQL Editor do Supabase, depois do 0013_modulos_personalizados.sql.
-- Enquanto não rodar, o app sincroniza todo o resto (etiquetas sem nenhuma tarefa ficam só no aparelho) e avisa no card da Conta.
--
-- Os cards continuam guardando as etiquetas deles em cards.etiquetas; aqui fica a lista de todas as criadas.

alter table public.perfis
  add column if not exists etiquetas_tarefas text[];   -- ["cliente", "urgente", ...]
