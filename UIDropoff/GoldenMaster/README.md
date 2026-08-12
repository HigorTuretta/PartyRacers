# Golden Master

`Party Racers v2.dc.html` é o **design aprovado**. Não é inspiração, não é rascunho: é o desenho que a Unity tem que reproduzir.

Abra no navegador (junto com `support.js`, que precisa ficar ao lado). A lateral tem os controles de tela e de estado:

- **Tela**: Lobby público · Buscando partida · Partida personalizada · Garagem · HUD corrida PC · HUD corrida celular
- **Modo** (no Lobby): SOLO · DUO · SQUAD
- **Etapa** (na Busca): as 5 etapas
- **Estado do HUD**: NORMAL · DANO · DANIFICADO · CURA · ESCUDO

Toda medida dos arquivos USS foi extraída daqui. Se um valor do pacote divergir do HTML, **o HTML manda** — e avise, porque é defeito do pacote.

O HTML não é para ser portado literalmente: ele é CSS, e `docs/01-PORTE-HTML-PARA-UITOOLKIT.md` traduz cada recurso que o UI Toolkit não tem. Os arquivos UXML e USS já são essa tradução, feita e conferida.
