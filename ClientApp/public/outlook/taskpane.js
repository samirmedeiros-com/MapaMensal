(() => {
  const API = 'https://app.zoompositivo.pt/api';
  const KEY = 'mm_outlook_token';
  const $ = id => document.getElementById(id);
  const token = () => localStorage.getItem(KEY);

  let email = null;

  function show(el, on) { el.hidden = !on; }
  function msg(text, kind) {
    const m = $('msg');
    m.textContent = text;
    m.className = 'msg ' + (kind || '');
    show(m, !!text);
  }

  async function api(path, options = {}) {
    const headers = Object.assign({ 'Content-Type': 'application/json' }, options.headers || {});
    const t = token();
    if (t) headers.Authorization = 'Bearer ' + t;
    const res = await fetch(API + path, Object.assign({}, options, { headers }));
    if (res.status === 401) { localStorage.removeItem(KEY); throw new Error('sessao'); }
    if (!res.ok) throw new Error((await res.text()) || ('HTTP ' + res.status));
    return res.status === 204 ? null : res.json();
  }

  // ── Email selecionado ──────────────────────────────────────────────────────
  function readEmail() {
    const item = Office.context.mailbox.item;
    const from = item.from || item.sender || {};
    return new Promise(resolve => {
      const base = {
        assunto: item.subject || '(sem assunto)',
        de: [from.displayName, from.emailAddress].filter(Boolean).join(' <') + (from.emailAddress ? '>' : ''),
        data: item.dateTimeCreated ? new Date(item.dateTimeCreated).toLocaleString('pt-PT') : '',
        link: deepLink(item)
      };
      if (!item.body || !item.body.getAsync) return resolve(base);
      item.body.getAsync(Office.CoercionType.Text, r => {
        base.corpo = r.status === Office.AsyncResultStatus.Succeeded ? (r.value || '').trim() : '';
        resolve(base);
      });
    });
  }

  function deepLink(item) {
    try {
      const restId = Office.context.mailbox.convertToRestId(item.itemId, Office.MailboxEnums.RestVersion.v2_0);
      return 'https://outlook.office.com/mail/deeplink/read/' + encodeURIComponent(restId);
    } catch { return ''; }
  }

  function descricaoDe(e) {
    const corpo = (e.corpo || '').split('\n').slice(0, 25).join('\n').slice(0, 1500);
    return [
      'Email de: ' + e.de,
      e.data ? 'Recebido: ' + e.data : '',
      e.link ? 'Abrir no Outlook: ' + e.link : '',
      '',
      corpo
    ].filter(l => l !== null).join('\n').trim();
  }

  // ── Ecrãs ─────────────────────────────────────────────────────────────────
  async function abrirFormulario() {
    show($('login'), false);
    let projetos;
    try {
      projetos = await api('/projects');
    } catch (e) {
      if (e.message === 'sessao') return abrirLogin();
      return msg('Não foi possível carregar os projetos: ' + e.message, 'err');
    }
    const sel = $('projeto');
    sel.innerHTML = '';
    for (const p of projetos) {
      const o = document.createElement('option');
      o.value = p.id; o.textContent = p.name;
      sel.appendChild(o);
    }
    const ultimo = localStorage.getItem('mm_outlook_projeto');
    if (ultimo && projetos.some(p => String(p.id) === ultimo)) sel.value = ultimo;

    $('titulo').value = email.assunto.slice(0, 200);
    $('descricao').value = descricaoDe(email);
    show($('form'), true);
    show($('logout'), true);
    msg('');
  }

  function abrirLogin() {
    show($('form'), false);
    show($('logout'), false);
    show($('login'), true);
  }

  async function login() {
    const btn = $('doLogin');
    btn.disabled = true; msg('');
    try {
      const r = await api('/auth/login', {
        method: 'POST',
        body: JSON.stringify({ username: $('u').value.trim(), password: $('p').value })
      });
      localStorage.setItem(KEY, r.token);
      $('p').value = '';
      await abrirFormulario();
    } catch (e) {
      msg(e.message === 'sessao' ? 'Utilizador ou password incorretos.' : 'Falha no login: ' + e.message, 'err');
    } finally {
      btn.disabled = false;
    }
  }

  async function criar() {
    const btn = $('criar');
    const titulo = $('titulo').value.trim();
    if (!titulo) return msg('O título é obrigatório.', 'err');
    btn.disabled = true; msg('');
    try {
      const t = await api('/tarefas', {
        method: 'POST',
        body: JSON.stringify({
          projectId: Number($('projeto').value),
          titulo,
          descricao: $('descricao').value,
          status: $('status').value,
          dataEntrega: $('data').value || null,
          horasGastas: 0
        })
      });
      localStorage.setItem('mm_outlook_projeto', $('projeto').value);
      msg('Tarefa #' + t.id + ' criada em ' + t.projectName + '.', 'ok');
    } catch (e) {
      if (e.message === 'sessao') { abrirLogin(); return msg('Sessão expirada — entra outra vez.', 'err'); }
      msg('Não foi possível criar a tarefa: ' + e.message, 'err');
    } finally {
      btn.disabled = false;
    }
  }

  Office.onReady(async info => {
    if (info.host !== Office.HostType.Outlook) return;
    $('doLogin').addEventListener('click', login);
    $('p').addEventListener('keydown', e => { if (e.key === 'Enter') login(); });
    $('criar').addEventListener('click', criar);
    $('logout').addEventListener('click', () => { localStorage.removeItem(KEY); abrirLogin(); });

    email = await readEmail();
    if (token()) await abrirFormulario(); else abrirLogin();
  });
})();
