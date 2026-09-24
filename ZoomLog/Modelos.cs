namespace ZoomLog.Cliente;

/// O que cada evento é. O ecrã desenha a árvore de um pedido a partir disto:
/// um `Pedido` é o que entrou na aplicação, uma `Chamada` é o que ela fez a
/// outro serviço durante esse pedido, e os `Log` penduram-se em qualquer um.
public enum TipoEvento { Log, Pedido, Chamada, Tarefa }

public enum NivelEvento { Debug = 1, Info = 2, Aviso = 3, Erro = 4, Critico = 5 }

/// Um evento tal como viaja para o ZoomLog. Os nomes são os do JSON — não se
/// mudam sem mudar o servidor ao mesmo tempo.
public sealed class EventoZoomLog
{
    public DateTime Ts { get; set; } = DateTime.UtcNow;
    public NivelEvento Nivel { get; set; } = NivelEvento.Info;
    public TipoEvento Tipo { get; set; } = TipoEvento.Log;

    /// W3C trace context: o mesmo `TraceId` atravessa todas as aplicações por
    /// onde o pedido passou; o `PaiId` diz de que passo este nasceu.
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? PaiId { get; set; }

    public string? Categoria { get; set; }
    public string Mensagem { get; set; } = "";

    public string? Metodo { get; set; }
    /// Numa chamada, o anfitrião de destino (`nfe.prefeitura.sp.gov.br`); num
    /// pedido, o anfitrião por onde entrou.
    public string? Destino { get; set; }
    public string? Caminho { get; set; }
    /// O molde da rota (`/api/processos/{id}`), para agrupar sem o número.
    public string? Rota { get; set; }
    public int? Estado { get; set; }
    public int? DuracaoMs { get; set; }

    public string? Utilizador { get; set; }
    public string? Cliente { get; set; }
    public string? Ip { get; set; }

    public string? ExcecaoTipo { get; set; }
    public string? Excecao { get; set; }

    public Dictionary<string, string?>? Propriedades { get; set; }
    public CorpoZoomLog? Corpo { get; set; }

    public string? Ambiente { get; set; }
    public string? Maquina { get; set; }
    public string? Versao { get; set; }
}

/// Os corpos já mascarados e cortados. Só vão quando uma regra os pede.
public sealed class CorpoZoomLog
{
    public string? PedidoCabecalhos { get; set; }
    public string? Pedido { get; set; }
    public string? TipoPedido { get; set; }
    public long? PedidoTamanho { get; set; }

    public string? RespostaCabecalhos { get; set; }
    public string? Resposta { get; set; }
    public string? TipoResposta { get; set; }
    public long? RespostaTamanho { get; set; }

    public bool Truncado { get; set; }
}

internal sealed class LoteZoomLog
{
    public List<EventoZoomLog> Eventos { get; set; } = [];
}
