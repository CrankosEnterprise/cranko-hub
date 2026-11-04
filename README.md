# CrankoHub — Painel Pessoal de Parâmetros

[![.NET 10](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Plataforma](https://img.shields.io/badge/Plataforma-Windows%2010%20%7C%2011%20%7C%20Linux%20Mint-0078D6.svg)](https://github.com/CrankosEnterprise/cranko-hub/releases/latest)
[![Arquitetura](https://img.shields.io/badge/Arquitetura-x64-green.svg)]()
[![UI](https://img.shields.io/badge/UI-Blazor%20Hybrid-purple.svg)]()
[![Release](https://img.shields.io/github/v/release/CrankosEnterprise/cranko-hub?label=Vers%C3%A3o%20Oficial&color=8B5CF6)](https://github.com/CrankosEnterprise/cranko-hub/releases/latest)

O **CrankoHub** é um aplicativo desktop para Windows e Linux que reúne num só lugar os parâmetros do dia a dia: dieta e peso, carro (abastecimentos, consumo, manutenção e documentos), assinaturas, entretenimento e tarefas. A interface é feita em Blazor Hybrid, os dados ficam salvos localmente e funcionam offline, e há a opção de entrar numa conta para sincronizar os dados entre computadores. Junto vêm alertas inteligentes, notificações do sistema, backup automático e atualização automática pelo GitHub Releases.

---

## 📥 Download

Baixe a versão oficial do **CrankoHub**:

[![Baixar CrankoHub para Windows](https://img.shields.io/badge/⬇%EF%B8%8F%20Baixar-CrankoHub%20para%20Windows-8B5CF6?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/CrankosEnterprise/cranko-hub/releases/latest/download/CrankoHub.exe)
[![Baixar CrankoHub para Linux](https://img.shields.io/badge/⬇%EF%B8%8F%20Baixar-CrankoHub%20para%20Linux-22C55E?style=for-the-badge&logo=linux&logoColor=white)](https://github.com/CrankosEnterprise/cranko-hub/releases/latest/download/CrankoHub-linux-x64.tar.gz)
[![Baixar CrankoHub para Android](https://img.shields.io/badge/⬇%EF%B8%8F%20Baixar-CrankoHub%20para%20Android-3DDC84?style=for-the-badge&logo=android&logoColor=white)](https://github.com/CrankosEnterprise/cranko-hub/releases/latest/download/CrankoHub-android.apk)

> **Windows:** [`CrankoHub.exe`](https://github.com/CrankosEnterprise/cranko-hub/releases/latest/download/CrankoHub.exe)  
> Executável único e autossuficiente (*self-contained* x64), com o .NET 10 embutido. Não precisa instalar: é só baixar e abrir.
>
> **Linux:** [`CrankoHub-linux-x64.tar.gz`](https://github.com/CrankosEnterprise/cranko-hub/releases/latest/download/CrankoHub-linux-x64.tar.gz)  
> Extraia o pacote e rode `./instalar.sh`. Ele copia o app para `~/.local/share/CrankoHub/app` e cria o atalho no menu.
>
> **Android:** [`CrankoHub-android.apk`](https://github.com/CrankosEnterprise/cranko-hub/releases/latest/download/CrankoHub-android.apk)  
> Abra o arquivo baixado no celular e permita "instalar apps desconhecidos" para o navegador ou o app de arquivos. Não está na Play Store, então o Android pode mostrar um aviso de segurança na primeira vez.
>
> **Compatibilidade do Sistema:**
> - **Windows:** Windows 10 (versão 1809 ou superior) e Windows 11, com o WebView2 (já vem no Windows 11).
> - **Linux:** Linux Mint 21/22 (Cinnamon) ou distribuições com `libwebkit2gtk-4.1-0`.
> - **Android:** Android 7.0 ou superior.
> - **Arquitetura:** x64 (64 bits) no Windows e no Linux; ARM e x86 no Android.
>
> 📦 **Versões Disponíveis:**
> - [Última Versão Estável (Latest)](https://github.com/CrankosEnterprise/cranko-hub/releases/latest)
> - [Todas as Releases Publicadas](https://github.com/CrankosEnterprise/cranko-hub/releases)

---

## 🎯 Principais Funcionalidades

### 1. Início e Alertas
- **Painel inicial** com cards de resumo de cada módulo e a atividade recente.
- **"Precisa de atenção":** uma lista única de pendências, como documento do carro vencendo, manutenção próxima, assinatura para renovar e tarefas atrasadas.
- **Notificações do sistema:** os mesmos alertas aparecem como notificação. Clicar nela abre a tela certa.

### 2. Dieta
- Perfil e meta de peso, registros de peso e medidas corporais.
- Gráficos de **evolução do peso** e de **variação semanal**, além do histórico completo.

### 3. Carro
- **Vários veículos**, organizados em abas: abastecimentos, consumo, manutenção, documentos e calculadoras.
- **Consumo:** km/l e custo por km por período, com gráficos.
- **Manutenção:** histórico, mais lembretes por km ou por data.
- **Documentos:** IPVA, licenciamento, seguro e CNH, com vencimento e opção de marcar como pago.
- **Calculadoras:** *Etanol × Gasolina* e *Custo de viagem*.

### 4. Assinaturas
- Streaming, softwares e serviços com ciclo de cobrança (mensal, anual...), próximo vencimento e custo total por mês.

### 5. Entretenimento
- Estante de **filmes, séries, animações, animes, jogos e livros**, com status (*Concluído*, *Em andamento*, *Quero ver*), nota e data.
- **Busca automática** de título, capa, sinopse e gêneros em TMDB, AniList, RAWG e Open Library.

### 6. Tarefas
- Quadros estilo **Kanban** (Trabalho, Faculdade, Pessoal...) com colunas e cards arrastáveis, prazo, prioridade, etiquetas e checklist.
- **Visão "Hoje"** com o que vence hoje, o que está atrasado e o que foi marcado como foco. À noite chega um resumo do que foi concluído.

### 7. Personalização
- Tema claro/escuro e **cor de destaque**.
- **Módulos:** cada área pode ser escondida do menu, do Início e das notificações sem perder os dados.

### 8. Dados, Backup e Sincronização
- **Offline primeiro:** tudo fica num arquivo local (`%LocalAppData%\CrankoHub\dados.json` no Windows, `~/.local/share/CrankoHub/` no Linux).
- **Backup automático**, exportar e importar o backup, e dados de exemplo para conhecer o app.
- **Pasta sincronizada** (OneDrive, Google Drive, Dropbox) para usar em mais de um PC, sem servidor.
- **Conta com login (Supabase):** os dados ficam centralizados por usuário e sincronizam item a item, inclusive as capas. As tabelas são protegidas por RLS, e a sessão fica guardada criptografada (DPAPI no Windows).

### 9. Aplicativo
- **Atualização automática:** Configurações → Aplicativo → Atualizações consulta os Releases deste repositório, sem precisar de token.
- **Bandeja do sistema**, iniciar com o Windows e instância única.
- **Versão Linux** nativa (Photino + WebKitGTK), com os mesmos dados e telas. Os backups do Windows importam direto.
- **Versão Android** (APK pelo GitHub, fora da Play Store), com as mesmas telas. Os dados vão para o celular pela conta. A atualização baixa o APK novo e abre o instalador do Android, e as notificações aparecem enquanto o app está aberto.

---

## 🏗️ Arquitetura do Projeto

```
cranko-hub/
├── CrankoHub/                       # Projeto principal (WPF + Blazor Hybrid, .NET 10)
│   ├── Pages/                       # Telas .razor (Início, Dieta, Carro, Assinaturas, Entretenimento, Tarefas, Configurações)
│   │   └── CarroAbas/               # Abas do Carro (Abastecimentos, Consumo, Manutenção, Documentos, Calculadoras)
│   ├── Components/                  # Componentes reutilizáveis (gráficos, diálogos, toasts, ícones, conta)
│   ├── Layout/                      # LayoutPrincipal.razor (menu lateral e rodapé da conta)
│   ├── Models/                      # Modelos de dados (EstadoApp é a raiz do dados.json)
│   ├── Services/                    # Regras de negócio, cálculos e serviços
│   │   ├── ArmazenamentoService.cs  # Leitura/gravação do dados.json, pasta sincronizada e pasta por conta
│   │   ├── Alertas.cs               # Fonte única do "Precisa de atenção" e das notificações
│   │   ├── AtualizacaoService.cs    # Atualização automática pelo GitHub Releases
│   │   ├── SincronizacaoService.cs  # Sincronização com o Supabase (Data API + Storage)
│   │   ├── ContaService.cs          # Login, sessão e conta
│   │   ├── EntretenimentoBusca.cs   # Busca em TMDB, AniList, RAWG e Open Library
│   │   └── ...                      # Cálculos de Carro, Dieta, Assinaturas, Tarefas, backup, bandeja...
│   ├── Linux/                       # Projeto Linux (Photino.Blazor), que reaproveita os arquivos acima
│   │   ├── Plataforma/              # Partes exclusivas do Linux (diálogos GTK, notify-send, autostart...)
│   │   ├── Program.cs               # Ponto de entrada do app Linux
│   │   └── instalar.sh              # Instalador para o menu do Cinnamon
│   ├── Android/                     # Projeto Android (.NET MAUI Blazor Hybrid), que também reaproveita os arquivos acima
│   │   ├── Plataforma/              # Partes exclusivas do Android (seletor de arquivos, notificações, atualização por APK...)
│   │   └── Platforms/Android/       # MainActivity, MainApplication e AndroidManifest.xml
│   ├── supabase/migrations/         # SQL do banco (tabelas, RLS e bucket de capas)
│   ├── wwwroot/                     # index.html, CSS, JavaScript e fontes
│   ├── App.xaml / MainWindow.xaml   # Janela WPF com BlazorWebView
│   ├── CrankoHub.csproj             # Projeto Windows (publica um único .exe)
│   └── README.md                    # Roadmap e notas de desenvolvimento
├── .github/workflows/
│   └── crankohub-release.yml        # Gera o .exe, o pacote Linux e o APK Android e publica no Releases
├── cranko-hub.slnx                  # Solução (Windows + Linux + Android)
└── README.md                        # Esta página
```

> As notas de desenvolvimento, as decisões tomadas e os próximos updates ficam em [`CrankoHub/README.md`](CrankoHub/README.md).

---

## 🛠️ Tecnologias Utilizadas

- **Linguagem:** C# / Razor
- **Framework:** .NET 10.0 (`net10.0-windows10.0.19041.0` no Windows, `net10.0` no Linux)
- **Interface Gráfica:**
  - Windows: WPF + `Microsoft.AspNetCore.Components.WebView.Wpf` (Blazor Hybrid no WebView2)
  - Linux: `Photino.Blazor` (janela nativa com WebKitGTK)
  - Android: .NET MAUI + `Microsoft.AspNetCore.Components.WebView.Maui` (`net10.0-android`)
- **Gráficos:** componentes próprios (`GraficoLinha`, `GraficoBarras`)
- **Imagens:** `SkiaSharp`, para reduzir as capas do Entretenimento
- **Nuvem (opcional):** Supabase (Auth, Data API com RLS, Postgres e Storage) via `HttpClient`, sem SDK extra
- **Segurança local:** Windows DPAPI para a sessão da conta
- **Empacotamento:** `dotnet publish` em arquivo único (*single-file*, *self-contained*) e GitHub Actions

---

## 💻 Requisitos do Sistema

- **Para usar:** Windows 10 (1809+) / 11 x64 ou Linux Mint 21/22 x64.
- **Para desenvolver:** .NET SDK 10.0 instalado.

---

## 🚀 Como Compilar e Executar

### 1. Clonar o Repositório
```powershell
git clone https://github.com/CrankosEnterprise/cranko-hub.git
cd cranko-hub
```

### 2. Compilar a Solução
```powershell
dotnet build cranko-hub.slnx
```

### 3. Iniciar o CrankoHub
```powershell
# Windows
dotnet run --project CrankoHub/CrankoHub.csproj

# Linux
dotnet run --project CrankoHub/Linux/CrankoHub.Linux.csproj
```

---

## 📦 Como Gerar o Executável e Publicar Versões

### Gerar manualmente
```powershell
# Windows: um único CrankoHub.exe autossuficiente
dotnet publish CrankoHub/CrankoHub.csproj -c Release -o out

# Linux: pasta autossuficiente (depois: tar -czf CrankoHub-linux-x64.tar.gz -C out .)
dotnet publish CrankoHub/Linux/CrankoHub.Linux.csproj -c Release -o out

# Android: APK (precisa do workload: dotnet workload install maui-android).
# Sem a chave de assinatura, sai assinado com a chave de debug (bom para testar, não para distribuir).
dotnet publish CrankoHub/Android/CrankoHub.Android.csproj -c Release -o out
```

### 🔐 Chave de assinatura do Android (uma vez só)
O Android só instala uma atualização por cima se o APK novo for assinado com **a mesma chave** do anterior. A chave fica fora do repositório:

1. Gerar a chave (o `keytool` vem com o JDK do Visual Studio, em `C:\Program Files\Microsoft\jdk-*\bin`):
   ```powershell
   keytool -genkeypair -v -keystore crankohub.keystore -alias crankohub -keyalg RSA -keysize 2048 -validity 36500
   ```
   Use a mesma senha para o keystore e para a chave. **Guarde o arquivo e a senha num lugar seguro.** Se a chave for perdida, quem já instalou vai precisar desinstalar para pegar as versões novas.
2. Em **Settings → Secrets and variables → Actions** do repositório, criar:
   - `ANDROID_KEYSTORE_BASE64`: o conteúdo de `[Convert]::ToBase64String([IO.File]::ReadAllBytes("crankohub.keystore"))`
   - `ANDROID_KEYSTORE_PASSWORD`: a senha

### 🚀 Publicação de Novas Versões no GitHub
O workflow [`crankohub-release.yml`](.github/workflows/crankohub-release.yml) compila as três versões e anexa ao Release:

1. **Criar a tag da versão:**
   ```powershell
   git tag crankohub-v1.2.0
   git push origin crankohub-v1.2.0
   ```
   A versão do app vem da tag. Se o Release já foi criado pelo site, o workflow só anexa os arquivos nele.
2. **Nomes padronizados:** os arquivos do Release devem continuar se chamando `CrankoHub.exe`, `CrankoHub-linux-x64.tar.gz` e `CrankoHub-android.apk`.
3. **Persistência do link:** com esses nomes, os links de download direto
   ```
   https://github.com/CrankosEnterprise/cranko-hub/releases/latest/download/CrankoHub.exe
   https://github.com/CrankosEnterprise/cranko-hub/releases/latest/download/CrankoHub-linux-x64.tar.gz
   https://github.com/CrankosEnterprise/cranko-hub/releases/latest/download/CrankoHub-android.apk
   ```
   apontam sozinhos para a versão nova, e os apps instalados a encontram em Configurações → Atualizações.

---

## 🔑 Integrações (opcionais)

A busca do Entretenimento funciona com AniList e Open Library sem configuração nenhuma. Para filmes, séries e jogos:

1. Abra **Configurações → Integrações**.
2. **TMDB** (filmes, séries e animações): crie uma chave gratuita em [themoviedb.org](https://www.themoviedb.org/settings/api).
3. **RAWG** (jogos): crie uma chave gratuita em [rawg.io/apidocs](https://rawg.io/apidocs).
4. As chaves ficam salvas apenas no seu computador.
