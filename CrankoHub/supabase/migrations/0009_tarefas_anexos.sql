-- CrankoHub: anexos nas tarefas.
-- Rodar no SQL Editor do Supabase, depois do 0008_tarefas_ocultar_concluidas.sql.
-- Enquanto não rodar, o app sincroniza todo o resto (os anexos ficam só no aparelho) e avisa no card da Conta.
--
-- A linha do card guarda só os dados de cada anexo (nome, tamanhos, tipo, arquivo). O arquivo em si, já compactado
-- pelo app (imagem em WebP, o resto em Brotli), fica no bucket privado "anexos", em "<user_id>/<arquivo>".

alter table public.cards
  add column if not exists anexos jsonb not null default '[]';  -- [{ "id", "nome", "tipo", "tamanho", "tamanho_guardado", "arquivo", "formato" }]

-- ---------------------------------------------------------------------------
-- Bucket: até 20 MB por arquivo, qualquer tipo
-- ---------------------------------------------------------------------------
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('anexos', 'anexos', false, 20971520, null)
on conflict (id) do update
  set public = excluded.public,
      file_size_limit = excluded.file_size_limit,
      allowed_mime_types = excluded.allowed_mime_types;

-- ---------------------------------------------------------------------------
-- Políticas: só o dono, só na pasta com o próprio user_id. Nada para anon.
-- ---------------------------------------------------------------------------
create policy "anexos: dono le" on storage.objects for select to authenticated
  using (bucket_id = 'anexos' and (storage.foldername(name))[1] = (select auth.uid())::text);

create policy "anexos: dono insere" on storage.objects for insert to authenticated
  with check (bucket_id = 'anexos' and (storage.foldername(name))[1] = (select auth.uid())::text);

-- Upsert (x-upsert: true) precisa de update além de insert
create policy "anexos: dono altera" on storage.objects for update to authenticated
  using (bucket_id = 'anexos' and (storage.foldername(name))[1] = (select auth.uid())::text)
  with check (bucket_id = 'anexos' and (storage.foldername(name))[1] = (select auth.uid())::text);

-- Anexo removido ou tarefa excluída: o arquivo sai do bucket
create policy "anexos: dono exclui" on storage.objects for delete to authenticated
  using (bucket_id = 'anexos' and (storage.foldername(name))[1] = (select auth.uid())::text);
