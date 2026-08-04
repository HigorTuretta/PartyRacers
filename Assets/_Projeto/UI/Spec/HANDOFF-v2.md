# Party Racers — Handoff v2 (Unity)

> **Por que existe um v2.** O v1 descrevia as telas em prosa ("placa de volta no topo-centro"). Isso obrigou a implementação a **inventar a geometria** em C#, e o resultado divergiu do design. O v2 substitui prosa por **especificação**: cada tela tem um `layout.json` com âncora, pivô, offsets, tamanho, fonte, sprite e ordem de cada nó.
>
> Fonte visual: `Party Racers v2.dc.html` (protótipo navegável).
> Fonte geométrica: `especificacao/layout/*.json` — **esta é a verdade**, não o texto abaixo.
> Tokens: `especificacao/tokens-v2.json`.

---

## 0 · O que deve ser REMOVIDO do repositório antes de começar

Auditei `HigorTuretta/PartyRacers@main`. Estes arquivos são a causa direta da divergência visual:

| Arquivo | Problema | Ação |
|---|---|---|
| `Assets/_Projeto/Scripts/UI/GarageController.cs` | Constrói UI em runtime: `new GameObject`, `AddComponent<Image>`, `BuildRoundSprite()` gerando sprite por pixel, `rt.sizeDelta =`. É o que renderiza a garagem com setinhas `◄ 11/15 ►`. | **Apagar.** Substituir por `GarageScreenUI.cs` (só binder). |
| `Assets/_Projeto/Scripts/UI/Editor/BuildScreensFrontend.cs` | ~49 KB de layout escrito à mão. Cada número é um chute; rodar de novo destrói edições do designer. | **Apagar** após o import inicial das cenas. |
| `Assets/_Projeto/Scripts/UI/Editor/BuildScenes.cs` | idem | **Apagar** |
| `Assets/_Projeto/Scripts/UI/Editor/BuildWidgets.cs` | idem | **Apagar** |
| `Assets/_Projeto/Scripts/UI/Editor/UIKitPlaca.cs` | Fabrica o lockup da marca tingindo `Countdown_Plate` de âmbar, porque faltava o PNG do logo. | **Apagar**; usar `Brand/Brand_Logo` (agora entregue). |

Substitua o pipeline "builder script" por **um importador único, descartável**, que lê os JSON e materializa as cenas — e depois some do projeto.

---

## 1 · Regra de ouro (inalterada, agora com dentes)

**Toda a UI é montada à mão na cena / em prefabs. Scripts apenas bindam.**

Proibido em qualquer `.cs` fora de `Editor/Importer/`:

```
new GameObject(…)            AddComponent<Image>()       AddComponent<TextMeshProUGUI>()
rectTransform.sizeDelta =    .anchoredPosition =         .anchorMin/anchorMax =
new Color(…)  em UI          Sprite.Create(…)            new Vector2(…) em RectTransform
```

Permitido:

```csharp
[SerializeField] Image healthFill;
[SerializeField] TextMeshProUGUI hpValue;
[SerializeField] GameObject stateReady, stateWaiting;   // filhos de estado, já na cena
[SerializeField] Transform friendListContent;
[SerializeField] RowFriend rowFriendPrefab;             // único Instantiate permitido

void Bind(PlayerVM vm) {
  hpValue.text = vm.Hp.ToString();
  healthFill.fillAmount = vm.Hp / 100f;
  stateReady.SetActive(vm.Ready);
  stateWaiting.SetActive(!vm.Ready);
}
```

**Estados são filhos, não código.** Um slot tem `State_Player` / `State_Empty` / `State_Locked` como GameObjects irmãos já estilizados na cena. O script faz `SetActive`. Nunca troca cor, sprite ou tamanho.

Adicione um teste de edição que falha o build se os padrões proibidos aparecerem fora do importador. Sem isso a regra volta a ser letra morta.

---

## 2 · Como ler o `layout.json`

```json
{
  "name": "Panel_Group",
  "type": "Image",
  "anchorMin": [0, 1], "anchorMax": [0, 1], "pivot": [0, 1],
  "anchoredPosition": [44, -294], "sizeDelta": [404, 336],
  "sprite": "Frames/UI_Panel_R26_Deep", "borders": [26,26,26,26],
  "color": "rgba(10,12,34,0.82)", "stroke": "rgba(155,165,215,0.20)",
  "children": [ … ]
}
```

- **Unidades:** pixels no canvas de referência **1920×1080** (mobile: **2340×1080**). `CanvasScaler` = ScaleWithScreenSize, MatchWidthOrHeight, **match 0.5**.
- **Y é negativo para baixo** quando o pivô está no topo (convenção Unity). Os JSON já vêm assim.
- `offsetMin`/`offsetMax` aparecem quando o nó estica; `anchoredPosition`/`sizeDelta` quando é fixo. Nunca os dois.
- `borders` = os 4 valores de border do sprite 9-slice. Image type = **Sliced**.
- `shadow` = deslocamento Y da sombra dura, cor sempre `#0A0C22`, X sempre 0. Implemente como uma `Image` irmã atrás, não como `Shadow` component (o componente da Unity borra).
- `prefab` = este nó É uma instância; não recrie os filhos, use o prefab de `_widgets.json`.
- `states` = filhos irmãos mutuamente exclusivos. Todos existem na cena.
- `itemPrefab` + `count` = as N instâncias **já estão na cena** (grid de sala, standings). Só listas de tamanho desconhecido (amigos) usam `Instantiate`.
- `note` = restrição obrigatória, não comentário.

Ordem dos `children` **é** a ordem de hierarquia, que **é** o z-order. Respeite.

---

## 3 · Ordem de implementação

1. **`_widgets.json`** → construa os prefabs primeiro. Tudo depende deles.
2. **`Screen_Lobby`** + **`Screen_Matchmaking`** — prioridade declarada.
3. `Screen_Garage` (com a grade, apagando o `GarageController` legado).
4. `Screen_RaceHUD_PC` — o sistema de vida é o que muda mais.
5. `Screen_CustomMatch`, `Screen_RaceHUD_Mobile`.

---

## 4 · Mudanças de direção visual (v1 → v2)

O caráter PLACA fica: adesivo esmaltado, contorno preto grosso, sombra dura, âmbar + azul-marinho, Titan One. O que muda:

**Painéis viraram vidro.** Antes eram placas opacas com contorno grosso, que sufocavam a cena 3D. Agora: `rgba(10,12,34,0.82)` + blur 16 + stroke fino `rgba(155,165,215,0.20)`, raio 26.

**Contorno grosso passa a ser hierarquia, não textura.** Reservado a botões (3px), placas de HUD (4px) e a marca. Painéis não têm contorno preto. Isso é o que separa "o que eu clico" de "onde eu leio".

**Cena 3D tem zona franca.** No lobby, nenhuma UI opaca entra em x=[520,1400] × y=[300,860]. Os painéis são colunas laterais.

**Câmera é UI.** Na garagem, o kart é centrado em **x=1341** — o centro do espaço livre à direita do painel, não o centro da tela. **O chão da cena 3D vai junto** (plataforma, anel e glow em cx=1341): se ficar em 960, o kart flutua e metade da plataforma desaparece atrás do painel. Sombra elíptica na base do kart em todas as telas. Trocar de categoria move a câmera (0.45s easeInOutCubic) para o que está sendo editado — rodas aproximam por baixo, adesivos vão para o perfil. As poses estão em `Screen_Garage.json → camera3D.poses`.

---

## 5 · Sistema de vida (novo — leia inteiro)

```
HP máximo ......... 100
Parede 75–110 km/h .. 4     |  111–150 .. 6  |  151–200 .. 8   |  <75 .. 0
Armadilha .......... 10
Item ............... 15
Cooldown contato ... 0.75s  (impede drenagem contínua no moinho)

HP <= 0  →  ESTADO DANIFICADO por 2.5s
             velocidade máx  −35%
             aceleração      −50%
             resposta esterço −25%
             ao fim: HP volta a 100 automaticamente

Escudo ...... habilidade FIXA, recarga 22s, ativo 3s
              bloqueia itens, armadilhas e obstáculos
              PC: Q       |  Gamepad: LB  |  Mobile: botão azul
Item ........ PC: E       |  Gamepad: RB  |  Mobile: botão âmbar
Cura ........ pickup na bifurcação, +40
```

**Linguagem de cor — não misture:**

| Conceito | Cor | Forma |
|---|---|---|
| Vida | `#3DDC97` | barra segmentada horizontal, 5 blocos de 20 HP, base-centro |
| Escudo | `#35A7FF` | barra segmentada **acima** da vida. **Sem botão e sem ícone** — a barra é o indicador |
| Cura | `#3DDC97` | cruz + número flutuante `+40`. **Nunca uma barra.** |
| Dano | `#FF4D6D` | arco na borda + número `−15` |
| Reparo | `#FFB020` | listras diagonais na barra, só no estado danificado |

Segmento parcialmente drenado = `Image` type Filled, Horizontal. Não redimensione o RectTransform.

**Cluster vital.** Escudo e vida empilhados no **canto inferior esquerdo**, ancorados em `[36, 34]`, 486px de largura. O poder vai para o canto inferior **direito**. Os toasts sobem para a coluna esquerda, acima das barras (`[36, 172]`).

**O escudo não tem botão nem ícone.** A própria barra comunica o estado:

| Estado | Barra |
|---|---|
| Disponível | 3 segmentos cheios, brilho pulsando 16→34px em 1.8s, faixa de luz varrendo em 2.4s, chapinha `Q PRONTO` |
| Ativo | segmentos quase brancos, contorno ciano, glow 40px + inner glow, varredura acelerada para 1s, chapinha `ATIVO 2.1s` |
| Em recarga | barra contínua com preenchimento proporcional, **sem brilho e sem varredura**, ponta piscando, timer |

A ausência de brilho é o sinal de indisponível — não é preciso ícone.

**O poder fica no canto inferior direito** (124×124, ancorado em `[-36, 34]`), diagonalmente oposto às barras vitais. O **centro da tela permanece livre de UI** — é a linha de condução; qualquer coisa ali polui a leitura da pista. Chapinha com a letra **E**.

**Estado danificado substitui as barras**, não empilha: `HealthBar` e `ShieldBar` desligam, `RepairBar` liga.

**Sem prompt de escolha item/cura.** A bifurcação se comunica pela pista (geometria e sinalização do mundo 3D), não por UI.

**Sem mira, sem velocímetro, sem minimapa, sem HP dos adversários.** O alvo de item é sempre o carro imediatamente à frente ou atrás — implícito, sem indicador.

**Aviso de ataque = só o arco vermelho na borda.** Sem texto, sem seta, sem ícone, sem alerta no centro. Duas intensidades: `Approaching` (pulso 0.8s) e `Imminent` (pulso 0.25s).

**Toda notificação vai para o `ToastStack`** no canto inferior-esquerdo. Máximo 3, vida 2.5s.

---

## 6 · Matchmaking — a experiência de rádio

Metáfora: **sintonizar o canal da oficina**. Não é um spinner.

```
0s ........ AGUARDANDO O GRUPO      agulha parada, dial escuro
0–40s ..... SINTONIZANDO            agulha varre 2%→98% em 2.6s pingpong easeInOutSine
            cada piloto encontrado = Blip_Player aparece no dial (prBlip 0.5s easeOut)
            barras de sinal pulsam verde/âmbar
25s ....... timer fica âmbar (sem rótulo de limite na tela)
35s ....... timer fica vermelho
40s ....... trava a agulha em verde, fecha a lista de humanos,
            preenche as vagas com bots, sorteia o mapa
```

**16 jogadores por sala** (grid 8×2). O limite de 40s é regra interna — **não é exibido**; o jogador só vê o tempo decorrido. Membros do seu grupo aparecem com contorno âmbar e são posicionados **primeiro** na grid. Os 12 cards estão na cena — o script só troca o filho de estado.

A faixa de etapas (`PRONTOS → PROCURANDO → ENCONTRADOS → PREENCHENDO → CARREGANDO`) é a única indicação textual de progresso. `Done` / `Now` / `Todo` são filhos, não cores calculadas.

---

## 7 · Lobby

**O kart do jogador fica sempre centrado em x=960**, em qualquer modo. Os acompanhantes se distribuem alternadamente à direita e à esquerda em dois grupos de 298px. A fileira é ancorada pela **base** (`bottom`), nunca pelo topo ou centro — é o que faz os karts acompanharem a plataforma. Cada kart tem sombra elíptica caindo na faixa frontal da plataforma; sombra desenhada no ar não resolve flutuamento. Modo define quantos aparecem: SOLO 1, DUO 2, SQUAD 4. Trocar de modo faz dolly out (0.6s) para reenquadrar. Convidar alguém faz push in (0.35s) no kart que entrou.

**Carro no lobby é só visualização.** Customização acontece exclusivamente na garagem. Nenhum controle de cosmético nesta tela.

`BUSCAR PARTIDA` só habilita com o grupo inteiro pronto. Desabilitado usa `UI_Dashed_R18` + a razão do bloqueio ("FALTAM 2 JOGADORES") — nunca um botão âmbar cinzento sem explicação.

Aba de amigos tem duas fontes: **NO JOGO** e **STEAM**. Amigos já no grupo mostram "NO GRUPO" em vez do botão convidar.

Partida personalizada abre pela aba LOBBY (16 jogadores, mapa manual, sem rolagem — a sala inteira cabe em 2 colunas de 8).

---

## 8 · Fonte

**Titan One** — Google Fonts, licença OFL: <https://fonts.google.com/specimen/Titan+One>

Font Asset Creator: atlas 1024², Custom Range `32-126,192-255` (acentos PT-BR), SDFAA, padding 9.
Material: Outline thickness 0.2 cor `#1A1015`; Underlay offset Y −0.35, softness 0.

Fallbacks de peso equivalente: **Lilita One**, **Bowlby One**.

**Archivo** (UI, pesos 600–900) e **Space Mono** (rótulos, números) também no Google Fonts.

Mínimo legível: **17px** no canvas de referência. Nada abaixo disso.

---

## 9 · Checklist de aceite

- [ ] Nenhum `new GameObject` / `AddComponent<Image>` / `sizeDelta =` fora de `Editor/Importer/`
- [ ] Builders de editor apagados; cenas editáveis à mão sem risco de sobrescrita
- [ ] `GarageController.cs` legado removido; garagem usa grade de cards, sem setinhas `◄ ►`
- [ ] Cada tela confere com seu JSON em âncora, offset e tamanho (tolerância 2px)
- [ ] Nenhum botão nem ícone de escudo na tela; a barra brilha e varre só quando disponível
- [ ] Poder no canto inferior direito; centro da tela sem nenhuma UI
- [ ] Barras de escudo e vida no canto inferior esquerdo; toasts logo acima delas
- [ ] Karts com sombra de chão **caindo sobre a plataforma**; fileira ancorada por `bottom`, não por `top`
- [ ] Kart do jogador centrado em x=960 em todos os modos
- [ ] Sala de matchmaking com 16 vagas; nenhum rótulo de "limite 40s" na tela
- [ ] Estado danificado substitui as barras; listras âmbar drenam em 2.5s
- [ ] Arco vermelho é o único aviso de ataque
- [ ] Agulha do dial varre e trava aos 40s; blips aparecem por piloto
- [ ] Câmera da garagem muda de pose ao trocar categoria
- [ ] Chão da garagem centrado em x=1341 junto com o kart; kart assentado na plataforma, com sombra
- [ ] Nenhum alvo de toque abaixo de 88px no mobile
- [ ] Nada abaixo de 17px em nenhuma tela
- [ ] Contraste verificado **durante gameplay real**, não em fundo estático
