#if DEBUG
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Só na depuração: com CRANKOHUB_DEMO=1 o app abre com estes dados de exemplo (os mesmos do protótipo
/// "Fusão" em Redesign/), sem ler nem gravar o dados.json e sem sincronizar com a conta.
/// Serve para conferir o layout com as telas cheias.
/// </summary>
public static class DadosDemo
{
    public static readonly bool Ativo = Environment.GetEnvironmentVariable("CRANKOHUB_DEMO") == "1";

    public static EstadoApp Criar()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var agora = DateTime.Now;
        var id = 0;
        string Id() => $"demo{++id}";

        // ---- Dieta: 25 pesos em 90 dias, descendo de 89,1 para 82,4 ----
        double[] pesos = [89.1, 88.7, 88.9, 88.2, 87.8, 87.9, 87.1, 86.8, 86.9, 86.2, 85.9, 85.6, 85.8, 85.1, 84.7, 84.9, 84.3, 84.0, 83.8, 83.9, 83.3, 83.0, 82.8, 82.9, 82.4];
        var dieta = new DadosDieta
        {
            Perfil = new PerfilDieta { Altura = 1.80, Meta = 78 },
            Registros = pesos.Select((p, i) => new RegistroPeso { Id = Id(), Data = hoje.AddDays(-90 + (int)Math.Round(i * 3.6)), Peso = p }).ToList(),
        };

        // ---- Carro: Civic com um ano de abastecimentos ----
        var civic = new Veiculo
        {
            Id = "demo-civic", Nome = "Civic", Marca = "Honda", Ano = 2019,
            Ficha = new FichaVeiculo { Versao = "EXL 2.0", Placa = "FHK-2B47", TanqueLitros = 47, Cor = "Prata" },
        };
        var abastecimentos = new List<Abastecimento>();
        double[] kmls = [10.9, 11.4, 11.1, 12.3, 11.6, 10.8, 11.9, 12.1, 11.5, 11.2, 12.4, 11.8];
        string[] postos = ["Shell · Av. Brasil", "Ipiranga · Centro", "Petrobras · Rod. SP-330"];
        var odo = 56_200.0;
        abastecimentos.Add(new Abastecimento { Id = Id(), VeiculoId = civic.Id, Data = hoje.AddDays(-372), Combustivel = "gasolina", Odometro = odo, Km = 0, Litros = 40, Valor = 236, TanqueCheio = true, Posto = postos[0] });
        for (var i = 0; i < 24; i++)
        {
            var data = hoje.AddDays(-357 + i * 15);
            var kml = kmls[i / 2] + (i % 2 == 0 ? -0.2 : 0.2);
            var etanol = i == 19;
            var km = etanol ? 370 : 410 + (i * 37 % 90);
            var litros = Math.Round(km / (etanol ? 8.4 : kml), 1);
            odo += km;
            abastecimentos.Add(new Abastecimento
            {
                Id = Id(), VeiculoId = civic.Id, Data = data, Combustivel = etanol ? "etanol" : "gasolina",
                Odometro = odo, Km = km, Litros = litros, Valor = Math.Round(litros * (etanol ? 4.71 : 6.11), 2),
                TanqueCheio = true, Posto = postos[i % 3],
            });
        }
        var manutencoes = new List<Manutencao>
        {
            new() { Id = Id(), VeiculoId = civic.Id, Tipo = "oleo", Data = hoje.AddMonths(-9), Odometro = 58_420, Valor = 389, IntervaloKm = 10_000, IntervaloMeses = 12, Local = "Oficina do Zé" },
            new() { Id = Id(), VeiculoId = civic.Id, Tipo = "freios", Data = hoje.AddMonths(-8), Odometro = 52_000, Valor = 640, IntervaloKm = 20_000 },
            new() { Id = Id(), VeiculoId = civic.Id, Tipo = "alinhamento", Data = hoje.AddMonths(-6), Odometro = 61_000, Valor = 160, IntervaloKm = 10_000, IntervaloMeses = 12 },
        };
        var documentos = new List<DocumentoCarro>
        {
            new() { Id = Id(), VeiculoId = civic.Id, Tipo = "ipva", Descricao = "IPVA 2027", Vencimento = hoje.AddDays(18), Valor = 2148 },
            new() { Id = Id(), VeiculoId = civic.Id, Tipo = "licenciamento", Vencimento = hoje.AddMonths(4), Valor = 160, Pago = true },
            new() { Id = Id(), VeiculoId = civic.Id, Tipo = "seguro", Vencimento = hoje.AddMonths(4).AddDays(-10), Valor = 3200 },
            new() { Id = Id(), Tipo = "cnh", Vencimento = hoje.AddYears(3) },
        };

        // ---- Assinaturas ----
        Assinatura A(string nome, double valor, int dia, string categoria, string ciclo = "mensal") => new()
        {
            Id = Id(), Nome = nome, Valor = valor, Ciclo = ciclo, Categoria = categoria,
            Vencimento = new DateOnly(hoje.Year, hoje.Month, Math.Min(dia, DateTime.DaysInMonth(hoje.Year, hoje.Month))),
            Pagamento = "Cartão final 4471",
        };
        var assinaturas = new List<Assinatura>
        {
            A("Netflix", 44.90, 22, "Streaming"), A("Spotify", 34.90, hoje.AddDays(1).Day, "Música"),
            A("iCloud+", 16.90, 3, "Armazenamento"), A("ChatGPT", 119.90, 27, "Produtividade"),
            A("Amazon Prime", 19.90, 9, "Streaming"), A("Game Pass", 59.99, 18, "Jogos"),
            A("YouTube Premium", 26.90, 30, "Streaming"), A("Smart Fit", 129.90, 5, "Saúde"),
        };
        var pausada = A("Disney+", 33.90, 12, "Streaming");
        pausada.Ativa = false;
        assinaturas.Add(pausada);

        // ---- Agenda e Tarefas de hoje ----
        var agenda = new DadosAgenda
        {
            Agendas = [new AgendaCor { Id = AgendaCor.Principal, Nome = "Pessoal", Cor = CoresAgenda.Todas[0] }],
            Eventos =
            [
                new() { Id = Id(), AgendaId = AgendaCor.Principal, Titulo = "Dentista", Local = "Clínica Sorriso", Inicio = agora.Date.AddHours(Math.Max(agora.Hour + 1, 15)).AddMinutes(30), Fim = agora.Date.AddHours(Math.Max(agora.Hour + 2, 16)).AddMinutes(30) },
                new() { Id = Id(), AgendaId = AgendaCor.Principal, Titulo = "Futsal", Local = "Arena Norte", Inicio = agora.Date.AddHours(Math.Max(agora.Hour + 2, 19)), Fim = agora.Date.AddHours(Math.Max(agora.Hour + 3, 20)) },
            ],
        };
        var quadro = new Quadro { Id = "demo-casa", Nome = "Casa", Colunas = [new() { Id = "c1", Nome = "A fazer" }, new() { Id = "c2", Nome = "Fazendo" }, new() { Id = "c3", Nome = "Feito" }] };
        CardTarefa T(string titulo, string coluna, DateOnly? prazo = null, DateOnly? feito = null) =>
            new() { Id = Id(), QuadroId = quadro.Id, ColunaId = coluna, Titulo = titulo, Prazo = prazo, Criado = hoje.AddDays(-3), Concluido = feito };
        var tarefas = new DadosTarefas
        {
            Quadros = [quadro], Ativo = Quadro.Hoje,
            Cards =
            [
                T("Pagar condomínio", "c3", hoje, hoje),
                T("Revisar orçamento da reforma", "c1", hoje),
                T("Ligar para a oficina", "c1", hoje),
                T("Comprar ração", "c1", hoje),
                T("Trocar lâmpada da garagem", "c1", hoje.AddDays(-2)),
            ],
        };

        // ---- Entretenimento ----
        var entretenimento = new List<ItemEntretenimento>
        {
            new() { Id = Id(), Categoria = "livro", Titulo = "Duna", Status = "andamento", Data = hoje.AddDays(-20) },
            new() { Id = Id(), Categoria = "serie", Titulo = "The Bear", Status = "andamento", Data = hoje.AddDays(-5) },
            new() { Id = Id(), Categoria = "jogo", Titulo = "Hollow Knight: Silksong", Status = "andamento", Data = hoje.AddDays(-2) },
            new() { Id = Id(), Categoria = "filme", Titulo = "Interestelar", Status = "concluido", Data = hoje.AddDays(-40), Nota = 5 },
        };

        return new EstadoApp
        {
            Dieta = dieta,
            Carro = new DadosCarro { Veiculos = [civic], Ativo = civic.Id, Abastecimentos = abastecimentos, Manutencoes = manutencoes, Documentos = documentos },
            Assinaturas = assinaturas,
            Agenda = agenda,
            Tarefas = tarefas,
            Entretenimento = entretenimento,
        };
    }
}
#endif
