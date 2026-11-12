-- CrankoHub: marcador de combustível nos abastecimentos.
-- Rodar no SQL Editor do Supabase, depois do 0010_manutencao_anexos.sql.
-- Enquanto não rodar, o app sincroniza todo o resto (estes campos ficam só no aparelho) e avisa no card da Conta.
--
-- "litros" continua sendo o que a bomba colocou (base do preço por litro). "litros_gastos" é o que foi gasto
-- desde o abastecimento anterior quando difere disso: digitado (computador de bordo) ou estimado pelo ponteiro
-- do marcador ("nivel_antes", em oitavos, antes de abastecer) com o tamanho do tanque da ficha do veículo.

alter table public.abastecimentos
  add column if not exists litros_gastos numeric,
  add column if not exists nivel_antes smallint check (nivel_antes between 0 and 8),
  add column if not exists tanque_cheio boolean not null default false,
  add column if not exists gasto_estimado boolean not null default false;
