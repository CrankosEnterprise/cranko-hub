-- CrankoHub: temporadas e episódios no Entretenimento (séries, animes e animações em série).
-- Rodar no SQL Editor do Supabase, depois do 0011_abastecimento_marcador.sql.
-- Enquanto não rodar, o app sincroniza todo o resto (os episódios ficam só no aparelho) e avisa no card da Conta.
--
-- "temporadas" é a cópia da lista vinda da fonte (TMDB, TVmaze, AniList/Jikan), para abrir sem internet em todo aparelho.
-- "episodios_vistos" são as chaves "temporada x episódio" marcadas ("1x3").
-- "nomes_episodios" é a opção da engrenagem do Entretenimento (trazer e mostrar o nome de cada episódio).

alter table public.entretenimento
  add column if not exists temporadas jsonb not null default '[]',        -- [{ "numero", "nome", "episodios": [{ "numero", "nome", "data" }] }]
  add column if not exists episodios_vistos jsonb not null default '[]';  -- ["1x1", "1x2", ...]

alter table public.perfis
  add column if not exists nomes_episodios boolean;
