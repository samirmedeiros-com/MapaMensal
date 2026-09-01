/// Recebe uma sessão entregue pela Administração (admin.zoompositivo.pt).
///
/// O endereço chega como `…/#sso=<base64>`. O fragmento não é enviado a
/// servidor nenhum — não fica no registo do nginx nem viaja no `Referer` —, e
/// é apagado do endereço antes de a aplicação arrancar, para não ficar no
/// histórico do browser.

/// Uma chave a guardar no armazenamento desta origem.
export interface ChaveDePassagem {
  chave: string;
  valor: string;
}

/// A parte que decide: lê o fragmento e devolve o que há para guardar.
///
/// Está separada de quem escreve por duas razões. A primeira é que é aqui que
/// mora o risco — a leitura de dados que vêm de fora e a recusa do que não é
/// desta aplicação — e uma função sem efeitos põe-se à prova sem browser. A
/// segunda é que assim se lê de uma vez o que a regra é.
///
/// A carga traz `i` com uma lista de chaves: há aplicações que guardam a
/// sessão em mais do que um sítio. O `c`/`v` solto é a forma antiga, de quando
/// era sempre uma chave só, e continua a ser lida para uma Administração ainda
/// por publicar não deixar de funcionar.
export function chavesDaPassagem(
  hash: string,
  prefixos: string | string[],
): ChaveDePassagem[] {
  const marca = '#sso=';
  if (!hash.startsWith(marca)) return [];

  const aceites = Array.isArray(prefixos) ? prefixos : [prefixos];

  try {
    const bruto = decodeURIComponent(hash.slice(marca.length));
    const bytes = Uint8Array.from(atob(bruto), (c) => c.charCodeAt(0));
    const carga = JSON.parse(new TextDecoder().decode(bytes)) as {
      c?: string;
      v?: string;
      i?: { c?: string; v?: string }[];
    };

    const itens = carga.i?.length ? carga.i : [{ c: carga.c, v: carga.v }];

    return itens
      // Só chaves desta aplicação. Sem esta recusa, um endereço preparado por
      // outra pessoa escrevia o que quisesse no armazenamento desta origem.
      .filter((x): x is { c: string; v: string } =>
        typeof x?.c === 'string' && typeof x.v === 'string' && aceites.some((p) => x.c!.startsWith(p)),
      )
      .map((x) => ({ chave: x.c, valor: x.v }));
  } catch {
    // Fragmento estragado: entra-se como se não tivesse vindo nada, que é o
    // ecrã de entrada normal.
    return [];
  }
}

/// A parte que escreve.
export function receberSessao(prefixos: string | string[]): void {
  const hash = window.location.hash;
  if (!hash.startsWith('#sso=')) return;

  // Apaga-se primeiro, aconteça o que acontecer a seguir.
  history.replaceState(null, '', window.location.pathname + window.location.search);

  for (const { chave, valor } of chavesDaPassagem(hash, prefixos)) {
    localStorage.setItem(chave, valor);
  }
}
