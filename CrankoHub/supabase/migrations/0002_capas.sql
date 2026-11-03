-- CrankoHub: capas do Entretenimento no Storage (item 14 do README, fase 5).
-- Rodar no SQL Editor do Supabase, depois do 0001_esquema_inicial.sql.
--
-- Bucket privado "capas". Cada arquivo fica em "<user_id>/<arquivo>", com o mesmo nome
-- guardado em entretenimento.capa (único e nunca muda), e cada usuário só mexe na própria pasta.

-- ---------------------------------------------------------------------------
-- Bucket
-- ---------------------------------------------------------------------------
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('capas', 'capas', false, 2097152, array['image/jpeg', 'image/png', 'image/webp', 'image/gif'])
on conflict (id) do update
  set public = excluded.public,
      file_size_limit = excluded.file_size_limit,
      allowed_mime_types = excluded.allowed_mime_types;

-- ---------------------------------------------------------------------------
-- Políticas: só o dono, só na pasta com o próprio user_id. Nada para anon.
-- ---------------------------------------------------------------------------
create policy "capas: dono le" on storage.objects for select to authenticated
  using (bucket_id = 'capas' and (storage.foldername(name))[1] = (select auth.uid())::text);

create policy "capas: dono insere" on storage.objects for insert to authenticated
  with check (bucket_id = 'capas' and (storage.foldername(name))[1] = (select auth.uid())::text);

-- Upsert (x-upsert: true) precisa de update além de insert
create policy "capas: dono altera" on storage.objects for update to authenticated
  using (bucket_id = 'capas' and (storage.foldername(name))[1] = (select auth.uid())::text)
  with check (bucket_id = 'capas' and (storage.foldername(name))[1] = (select auth.uid())::text);

-- Capa trocada ou item excluído: o arquivo sai do bucket
create policy "capas: dono exclui" on storage.objects for delete to authenticated
  using (bucket_id = 'capas' and (storage.foldername(name))[1] = (select auth.uid())::text);
