-- CrankoHub: módulos personalizáveis (Configurações → Módulos).
-- Rodar no SQL Editor do Supabase, depois do 0012_entretenimento_episodios.sql.
-- Enquanto não rodar, o app sincroniza todo o resto (ordem, nomes, cores e ícones ficam só no aparelho) e avisa no card da Conta.
--
-- "ordem_modulos" são os ids dos módulos na ordem escolhida (menu, atalhos e Início).
-- "modulos_personalizados" é o que o usuário mudou em cada módulo; campo ausente/nulo = o padrão.

alter table public.perfis
  add column if not exists ordem_modulos text[],               -- ["agenda", "tarefas", "dieta", ...]
  add column if not exists modulos_personalizados jsonb;       -- { "carro": { "nome", "descricao", "cor": "#RRGGBB", "icone" } }
