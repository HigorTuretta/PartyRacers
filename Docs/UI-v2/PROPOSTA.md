# Party Racers — Reformulação de menus e HUD (PLACA v2)

Proposta completa e registro do que já está implementado. A fonte visual é o protótipo
`Party Racers v2.dc.html`; a fonte geométrica são os `Assets/_Projeto/UI/Spec/layout/*.json`.
Quando este texto e o JSON discordarem, **o JSON vence** — foi exatamente a prosa mandando na
geometria que fez o v1 divergir do design.

---

## 1 · Análise das telas atuais

Auditoria da UI que existe hoje no projeto (11 telas `Screen_*`, cenas `Boot`/`Frontend`/`Race`).

### 1.1 Hierarquia

| Problema | Onde aparece | Consequência |
|---|---|---|
| Contorno preto grosso aplicado em tudo | painéis, cards, botões e placas usam a mesma moldura | nada indica o que é clicável. O olho não separa "onde eu leio" de "onde eu ajo" |
| Painéis opacos ocupando a área central | lobby e garagem | a cena 3D vira plano de fundo decorativo; o carro, que é o ativo mais caro do jogo, compete com uma caixa escura |
| Peso tipográfico único | Titan One em quase tudo | sem escala de leitura: título, rótulo e valor gritam no mesmo volume |

### 1.2 Navegação

- **A garagem navega por setas `◄ 11/15 ►`.** Ver o catálogo exige 15 cliques e memória de curto
  prazo. Não há visão de conjunto, não há noção de quanto falta, não dá para pular.
- **Não existe lobby de verdade.** A tela chamada lobby é uma sala local de 1 jogador; não há modo,
  grupo, amigos, convite nem estado de pronto.
- **Não existe matchmaking.** Nenhuma tela cobre procurar partida, e portanto nenhum estado do
  fluxo é comunicado.
- **Partida personalizada não existe** como conceito separado do lobby.

### 1.3 Organização

- O painel "LOBBY" dentro da tela da Garagem mostra dados de mock — duas telas disputam o mesmo
  papel e nenhuma o cumpre.
- Loja e passe têm tela mas não têm transação: comprar e resgatar não debitam nada.
- `Garage.unity` (frontend antigo) continua no projeto **e no Build Settings** junto com a
  `Frontend.unity` nova.

### 1.4 Legibilidade e apresentação

- **A causa-raiz do "monte de PNG esticado":** os sprites do v1 foram desenhados ~2,4× maiores que
  o uso final (`UI_Panel_R26_Deep` tinha 144 px com borda 60 para um raio de 26). Com
  `pixelsPerUnitMultiplier = 1` a Unity encolhia as bordas e o botão virava pílula sem contorno.
  O v2 corrige na origem: os sprites agora nascem no tamanho de uso (70×70 com borda 34), e o
  multiplicador volta a ser 1.
- Rótulos de estado com placeholder vazando (`"ABA"`, `"MÉDIA"` em opções selecionadas).
- HUD sem sistema de vida, sem escudo permanente e com o slot de poder disputando a linha de
  condução.

---

## 2 · Nova arquitetura de navegação

```
Boot
 └── Frontend  ── barra fixa: LOBBY · GARAGEM · LOJA · PASSE     [carteira · perfil]
      ├── LOBBY (público)          cena 3D de garagem, karts do grupo no palco
      │    └── BUSCANDO PARTIDA    modal sobre o lobby, não troca de tela
      │         └── CARREGANDO ──► pista
      ├── PARTIDA PERSONALIZADA    aba dentro de LOBBY (16 vagas, mapa manual)
      ├── GARAGEM                  kart em destaque à direita, grade de cosméticos à esquerda
      ├── LOJA
      └── PASSE
Race (pista)
 ├── HUD                 informação · cluster vital · alerta · poder
 ├── MENU DE PAUSA
 └── RESULTADO
```

Três decisões estruturais:

1. **Uma barra só, sempre no mesmo lugar.** Quatro destinos de topo, nunca aninhados. Uma criança
   consegue voltar de qualquer lugar com um movimento.
2. **Buscar partida é um modal sobre o lobby, não uma tela nova.** O jogador não perde o contexto
   do grupo enquanto espera, e cancelar não é "voltar" — é fechar.
3. **Partida personalizada é aba, não modo escondido.** É o mesmo espaço mental do lobby, com
   outras regras.

---

## 3 · Fluxo do lobby público

```
escolher MODO (SOLO 1 · DUO 2 · SQUAD 4)
   └─ convidar (lista NO JOGO · lista STEAM)
        └─ todos PRONTO ──► BUSCAR PARTIDA libera
             └─ modal de busca (§3.2)
```

### 3.1 Regras

- O kart do jogador fica **sempre centrado em x=960**, em qualquer modo. Os acompanhantes se
  distribuem alternadamente à direita e à esquerda, em dois grupos de 298 px.
- A fileira de karts é ancorada pela **base**, nunca pelo topo. Ancorada pelo topo, os karts
  flutuam acima da plataforma quando a resolução muda.
- Cada kart tem sombra elíptica **caindo sobre a plataforma**. Sombra desenhada no ar não resolve
  flutuamento — só o disfarça em uma resolução.
- **Nenhum controle de cosmético nesta tela.** Customizar é na garagem. O carro aqui é vitrine.
- `BUSCAR PARTIDA` desabilitado usa `UI_Dashed_R18` **e diz o motivo** ("FALTA 1 CONFIRMAR").
  Botão âmbar acinzentado sem explicação é o padrão que ensina o jogador a clicar em vão.
- Zona franca 3D: nenhuma UI opaca entra em `x=[520,1400] × y=[300,860]`.

### 3.2 Fluxo da busca — a metáfora do rádio

Não é um spinner. É **sintonizar o canal da oficina**.

| t | Etapa | Dial | Faixa de progresso |
|---|---|---|---|
| 0 s | Aguardando o grupo | agulha parada, dial escuro | `PRONTOS` |
| 0–40 s | Sintonizando | agulha varre 2%→98% em 2,6 s pingpong `easeInOutSine`; cada piloto encontrado vira um `Blip_Player` (`prBlip` 0,5 s `easeOut`) | `PROCURANDO` |
| 25 s | — | timer fica âmbar | — |
| 35 s | — | timer fica vermelho | — |
| 40 s | Fecha | agulha trava em verde | `ENCONTRADOS` → `PREENCHENDO` → `CARREGANDO` |

**O limite de 40 s nunca aparece na tela.** O jogador vê o tempo *decorrido*, não uma contagem
regressiva. Um cronômetro correndo para o fim ensina a esperar o estouro; o tempo decorrido só
comunica "estamos procurando". A cor do número faz o trabalho de urgência sem prometer prazo.

Ao estourar: fecha a lista de humanos → completa as 16 vagas com bots → sorteia o mapa → carrega.
**Sem seleção de mapa no público.** Membros do grupo entram **primeiro** na grade 8×2 e com contorno
âmbar, para que os amigos fiquem visivelmente juntos.

> Implementado em `Scripts/UI/Frontend/Party/MatchmakingService.cs`. A máquina de estados e a tela
> já são as definitivas; só a descoberta de humanos é simulada até o NGO entrar. Trocar a simulação
> pelo serviço real é trocar o corpo de um único método.

---

## 4 · Fluxo da partida personalizada

Aba do lobby, mesmas molduras, prioridade diferente: **aqui manda a administração da sala, não o
carro**. O palco 3D recua para o fundo.

- Criar sala privada com código (chip `Chip_RoomCode`, Space Mono, tracking 0,28).
- Até **16 jogadores**, em **2 colunas de 8** — a sala inteira cabe sem rolagem. Rolagem numa lista
  de sala é o que faz o anfitrião perder de vista quem ainda não confirmou.
- Cada linha (`Row_CustomSlot`) tem três estados: `State_Player`, `State_Bot`, `State_Empty`.
  Bot usa o violeta `#8C7BFF` — nunca a mesma cor de um humano.
- Ações do anfitrião por linha: remover, trocar de posição/equipe.
- **Seleção manual de mapa** com card de informação do mapa escolhido.
- Iniciar libera com as condições atendidas; o motivo do bloqueio aparece como no lobby.

---

## 5 · Fluxo da garagem

Fim das setas `◄ ►`. **Abas de categoria + grade de cards.**

```
[ MODELO ] [ COR ] [ RODAS ] [ FRENTE ] [ TRASEIRA ] [ TETO ] [ ADESIVOS ]
┌────────┬────────┬────────┬────────┐
│  card  │  card  │  card  │  card  │   Card_CosmeticItem 159×190
└────────┴────────┴────────┴────────┘
```

Cada card tem: preview, nome, raridade, selo de novo, cadeado, texto de desbloqueio e anel de foco.
Quatro estados mutuamente exclusivos, todos montados na cena: `Equipped`, `Selected`, `Free`,
`Locked`.

**Equipado e selecionado são coisas diferentes** e precisam ser distinguíveis à distância:
"selecionado" é onde o foco está agora; "equipado" é o que o carro está usando. Confundir os dois é
o erro mais comum desse tipo de tela.

### 5.1 A câmera é parte da interface

Trocar de categoria move a câmera em **0,45 s `easeInOutCubic`** para a peça em edição:

| Categoria | Pose |
|---|---|
| Modelo / Cor | visão completa do carro |
| Rodas | aproxima por baixo |
| Frente | desce à altura do para-choque |
| Traseira | gira para trás |
| Teto / Acessórios | eleva |
| Adesivos | vai para o perfil |

O kart é centrado em **x=1341** — o centro do espaço livre à direita do painel, não o centro da
tela. **O chão da cena 3D vai junto** (plataforma, anel e glow também em cx=1341). Deixar o chão em
960 é o que faz o kart flutuar e metade da plataforma sumir atrás do painel.

---

## 6 · Organização do HUD de corrida

```
┌─────────────────────────────────────────────────────────┐
│ 4º DE 12          VOLTA 2/3  01:12.480      1 ▸ MARINA  │
│ +2 POSIÇÕES        ÚLT · MELH                2 ▸ LEO_99 │
│                                              3 ▸ ...    │
│                                                         │
│              ← CENTRO LIVRE DE UI →                     │
│                                                         │
│ ▸ toast                                                 │
│ ▸ toast                                                 │
│ ESCUDO ▰▰▰                                       ┌────┐ │
│ VIDA   ▰▰▰▰▱  72                                 │ E  │ │
└──────────────────────────────────────────────────┴────┴─┘
```

Quatro camadas, com papéis que não se misturam:

| Camada | Onde | O que |
|---|---|---|
| `InfoLayer` | topo e cantos superiores | volta, tempo, posição, classificação, toasts |
| `VitalCluster` | inferior esquerdo `[36,34]`, 486 px | escudo, vida, imunidade, reparo |
| `AlertLayer` | tela cheia, sem raycast | arco de perigo, flashes, números flutuantes |
| `PowerSlot` | inferior direito `[-36,34]`, 124×124 | o item |

**O centro fica livre.** É a linha de condução — qualquer coisa ali disputa com a pista.

**Vitais à esquerda, poder à direita**, diagonalmente opostos. É a mesma separação das mãos no
controle (LB escudo / RB item) e evita que o jogador procure duas informações no mesmo canto no
momento em que está desviando de um foguete.

### 6.1 Linguagem de cor — os cinco conceitos não se confundem

| Conceito | Cor | Forma | Por que essa forma |
|---|---|---|---|
| Vida | `#3DDC97` | barra segmentada, 5 blocos de 20 HP | blocos são contáveis de relance; barra contínua exige medir |
| Escudo | `#35A7FF` | barra segmentada **acima** da vida | mesma gramática da vida (é defesa), cor e posição diferentes |
| Cura | `#3DDC97` | cruz + número flutuante `+40` — **nunca barra** | mesma cor da vida (é vida), forma oposta (evento, não estado) |
| Dano | `#FF4D6D` | arco na borda + número `−15` | vem de fora da tela, então mora na borda |
| Reparo | `#FFB020` | listras diagonais, só no estado danificado | listras = obra em andamento, gramática que ninguém mais usa |

O par cura/vida compartilha a cor **de propósito**: são a mesma grandeza. O que os separa é a forma —
estado é barra, evento é número. O par escudo/vida compartilha a forma: os dois são reservas
consumíveis. O que os separa é a cor. Nenhum par compartilha os dois.

O sinal do número flutuante (`+` / `−`) carrega a informação sozinho, sem depender da cor.

### 6.2 O escudo não tem botão nem ícone

A própria barra é o indicador:

| Estado | Barra |
|---|---|
| Disponível | 3 segmentos cheios · brilho pulsando 16→34 px em 1,8 s · faixa de luz varrendo em 2,4 s · chapinha `Q PRONTO` |
| Ativo | segmentos quase brancos · contorno ciano · glow 40 px · varredura acelerada para 1 s · chapinha `ATIVO 2.1s` |
| Em recarga | barra contínua proporcional · **sem brilho e sem varredura** · ponta piscando · timer |

**A ausência de brilho é o sinal de indisponível.** Um ícone acinzentado exige comparar com a
memória do ícone aceso; a ausência de movimento se percebe pela visão periférica, que é a única
disponível a 150 km/h.

### 6.3 Estado danificado

`HealthBar` e `ShieldBar` **desligam** e `RepairBar` liga no lugar delas. Substituir, não empilhar:
empilhar faria a informação mais urgente do jogo aparecer como mais uma linha entre outras.

### 6.4 O que o HUD não tem, e por quê

- **Sem mira.** O alvo do item é sempre o carro imediatamente à frente ou atrás — implícito.
- **Sem velocímetro.** A sensação de velocidade vem da câmera, do FOV e do vento. Um número
  competiria com a pista para dizer o que o corpo já sabe.
- **Sem minimapa.** O traçado se aprende pela pista.
- **Sem HP dos adversários.** Transformaria a corrida em gestão de alvos.
- **Sem prompt de escolha item/cura.** A bifurcação se comunica pela geometria e pela sinalização
  do mundo 3D. Um prompt na tela ensinaria o jogador a olhar para a UI no exato momento em que ele
  precisa olhar para a pista.
- **Aviso de ataque = só o arco vermelho.** Sem texto, sem seta, sem ícone, sem alerta central.

---

## 7 · Estados de cada componente

Todos os estados são **GameObjects irmãos já estilizados na cena**. O script faz `SetActive` e nada
mais — nunca troca cor, sprite ou tamanho.

| Componente | Estados |
|---|---|
| `Row_GroupSlot` | `State_Player` · `State_Empty` · `State_Locked` (+ `State_Ready`/`State_Waiting` dentro do player) |
| `Row_CustomSlot` | `State_Player` · `State_Bot` · `State_Empty` |
| `Card_MatchSlot` | `State_Mate` · `State_Human` · `State_Bot` · `State_Empty` |
| `Card_CosmeticItem` | `Equipped` · `Selected` · `Free` · `Locked` |
| `Card_Mode` | `State_Active` · `State_Idle` |
| `Chip_Tab` / `Chip_SubTab` | `State_Active` · `State_Idle` |
| `Chip_Stage` | `Done` · `Now` · `Todo` |
| `Slot_Power` | `Filled` · `Empty` · `Recharging` · `Locked` |
| `ShieldBar` | `Ready` · `Active` · `Cooling` · `Broken` |
| `Row_Standing` | `IsLocal` · `Other` |
| `DangerArc` | `Approaching` (pulso 0,8 s) · `Imminent` (pulso 0,25 s) |

---

## 8 · Animações e transições

| Movimento | Duração | Curva |
|---|---|---|
| Pressionar botão | 0,08 s, afunda 6 px, sombra some | — |
| Trocar de tela | 0,22 s | `easeOutQuad` |
| Câmera da garagem | 0,45 s | `easeInOutCubic` |
| Toast entra / vive / sai | 0,18 / 2,5 / 0,25 s | máx 3 simultâneos |
| Arco de perigo | 0,8 s aproximando · 0,25 s iminente | pingpong |
| Agulha do dial | 2,6 s | `easeInOutSine` pingpong |
| Blip de piloto | 0,5 s | `easeOut` |
| Número flutuante | sobe 40 px em 0,9 s | `easeOutQuad` + fade |
| Brilho do escudo pronto | 1,8 s | `easeInOutSine` |
| Varredura do escudo | 2,4 s pronto · 1,0 s ativo | `easeInOutSine` / linear |

Regra: animação **reforça** a ação, nunca a atrasa. Nada acima de 0,45 s no caminho de navegação.

---

## 9 · Câmera 3D por tela

| Tela | Enquadramento | Movimento |
|---|---|---|
| Lobby | jogador em x=960, fileira ancorada pela base | dolly out 0,6 s ao trocar de modo; push in 0,35 s no kart que entra |
| Partida personalizada | palco recuado, carro secundário | estático, respiração leve |
| Garagem | kart em x=1341, chão junto | troca de pose por categoria (0,45 s) |
| Matchmaking | lobby ao fundo, desfocado | agulha e blips no dial, não na cena |
| Corrida | câmera de gameplay | inalterada |

---

## 10 · Componentes reutilizáveis

`_widgets.json` define 24 widgets e 9 itens. Os principais:

**Botões** `Btn_Primary` (verde, ação de avanço) · `Btn_Amber` (âmbar, ação de destaque) ·
`Btn_Secondary` · `Btn_Danger` · `Btn_Icon` · `Btn_Touch` (mínimo 88 px, mobile).

**Chips** `Chip_Tab` · `Chip_SubTab` · `Chip_Currency` · `Chip_RoomCode` · `Chip_Profile` ·
`Chip_Stage`.

**Placas** `Plate_Amber` · `Plate_Ink` · `Plate_Cream` — contorno 4 px, sombra 6.

**Itens de lista** `Row_GroupSlot` · `Row_Friend` · `Row_CustomSlot` · `Row_Standing` ·
`Card_MatchSlot` · `Card_CosmeticItem` · `Blip_Player` · `Toast_Item`.

Hierarquia de contorno, que é o que separa "clico" de "leio":

| Elemento | Contorno |
|---|---|
| Painel de vidro | **0** — só stroke fino `rgba(155,165,215,.20)` |
| Card | 2 px |
| Botão | 3 px |
| Placa de HUD / modal | 4 px |

---

## 11 · Responsividade

- Canvas de referência **1920×1080**, `ScaleWithScreenSize`, `MatchWidthOrHeight`, **match 0.5**.
- Mobile: **2340×1080** paisagem + Safe Area.
- Nada abaixo de **17 px** no canvas de referência, em nenhuma tela.
- Nenhum alvo de toque abaixo de **88 px** no mobile.
- Listas de tamanho conhecido (sala, standings, toasts) têm as N instâncias **já na cena**; só a
  lista de amigos, cujo tamanho é desconhecido, usa `Instantiate`.
- Contraste verificado **durante gameplay real** — um fundo estático de editor não representa o
  pior caso, que é o HUD sobre céu claro em alta velocidade.

---

## 12 · Navegação por controle, teclado e mouse

| Ação | Teclado/Mouse | Gamepad |
|---|---|---|
| Navegar | Tab / setas / clique | D-pad e analógico esquerdo |
| Confirmar | Enter / clique | A |
| Voltar | Esc / botão VOLTAR | B |
| Trocar aba | Q / E | LB / RB |
| Usar item (corrida) | **E** | RB |
| Usar escudo (corrida) | **Q** | LB |
| Pausar | Esc | Start |

Todo elemento focável tem `Focus_Ring` próprio na cena — foco de controle nunca é a mesma coisa
que hover de mouse, e nunca é uma cor calculada em runtime.

> ⚠️ `StandaloneInputModule` lança exceção por frame com o Input System deste projeto. As cenas usam
> `InputSystemUIInputModule`.

---

## 13 · Justificativa das decisões

**Painéis viraram vidro** (`rgba(10,12,34,.82)` + blur 16). Placas opacas sufocavam a cena 3D. O
carro é o ativo mais caro do jogo e o motivo de existir a garagem; a UI tem que deixá-lo respirar.

**Contorno grosso virou hierarquia, não textura.** Reservado a botões, placas de HUD e à marca.
Quando tudo tem contorno, contorno não significa nada.

**Zona franca 3D declarada em pixels.** "Deixar espaço para o carro" é intenção; um retângulo
proibido é uma regra que pode ser verificada.

**Escudo sem botão.** Um botão a mais na tela custa atenção permanente para uma informação binária.
A barra já está lá por causa do HP; ensiná-la a brilhar sai de graça.

**Dano por faixa de velocidade, não por força de impacto.** Faixas são previsíveis e explicáveis:
"bati rápido, perdi mais". Física de impacto seria mais realista e menos aprendível — e este é um
party game.

**Cooldown de 0,75 s.** Sem ele, ficar preso no moinho zera a vida antes de o jogador entender o
que aconteceu. Com ele, dá tempo de perceber o perigo, reagir e sair.

**Estado danificado em vez de destruição.** O carro nunca é tirado da corrida. A punição é
temporária (2,5 s) e a vida volta sozinha a 100. O jogo continua sendo sobre a corrida, não sobre
sobreviver.

**Sem prompt na bifurcação item/cura.** A escolha é de pista, e o lugar de comunicá-la é a pista.

---

## 14 · Estado da implementação

### Verificado no Editor

O pipeline roda ponta a ponta e o resultado foi conferido de duas formas independentes:

**Geometria — 113 nós, 0 divergentes, 0 ausentes** (tolerância 2 px), comparando o YAML dos
prefabs gerados contra os `Screen_*.json`:

| Tela | Nós posicionados |
|---|---|
| Screen_Lobby | 28 ✓ |
| Screen_CustomMatch | 26 ✓ |
| Screen_Matchmaking | 22 ✓ |
| Screen_Garage | 15 ✓ |
| Screen_RaceHUD_PC | 12 ✓ |
| Screen_RaceHUD_Mobile | 10 ✓ |

**Visual — capturas reais em 1920×1080** em `Docs/UI-v2/capturas/`, geradas por
`Party Racers ▸ UI v2 ▸ 4 · Capturar as telas`. O HUD confere com a spec: escudo azul acima da
vida verde no canto inferior esquerdo, poder no inferior direito, centro sem nenhuma UI.

### Cinco defeitos que só a captura revelou

Nenhum destes aparece na verificação geométrica — todos passavam com a posição correta:

1. **Tingir `Bars/Bar_Fill` nunca produziria azul.** O sprite é âmbar (255,176,32) e o canal azul
   dele vale 32/255, então o multiply devolvia verde-oliva: a barra de ESCUDO nascia com a mesma
   cor da barra de VIDA, e a linguagem de cor do §6.1 caía por terra logo no primeiro item que
   ela precisava distinguir. Os segmentos passaram a usar o sprite branco neutro da Unity, e o
   `Bar_Fill` ficou onde a cor nativa dele É a resposta certa: as listras âmbar do reparo.
2. **`RawImage` sem textura é BRANCO.** O `Backdrop3D` (janela da cena 3D) cobria a tela de branco
   e os painéis de vidro escuro apareciam cinza por cima dele. O fallback correto é a tinta do tema.
3. **Overlays sem geometria declarada nascem 100×100 no centro.** Arco de perigo e flashes viravam
   um quadradinho no meio da tela — exatamente onde o design manda não ter nada.
4. **Sombra e contorno são objetos IRMÃOS e não acompanham o `SetActive`.** Esconder a barra de
   reparo deixava a sombra dela flutuando no canto.
5. **O primeiro estado de um item nasce ligado.** As 6 linhas da classificação vinham todas âmbar,
   e "esta linha é a minha" deixava de significar coisa alguma.

### Pronto neste passo

| Área | Arquivos |
|---|---|
| Sistema de vida | `Scripts/Kart/Health/KartHealth.cs` |
| Escudo como habilidade fixa | `Scripts/Kart/Health/KartShieldAbility.cs` |
| Caixa de cura | `Scripts/ItemBox/HealPickup.cs` |
| Penalidade de pilotagem + evento de impacto | `Scripts/Kart/KartController.cs` |
| Dano de armadilha | `Scripts/Kart/ObstacleKnockback.cs` |
| Dano de item | `RocketProjectile.cs`, `ElectricTrapPower.cs` |
| Escudo fora da ItemBox | `Scripts/ItemBox/ItemBox.cs` (com filtro em runtime para as caixas já salvas nas pistas) |
| Escudo para bots | `Scripts/AI/BotPowerController.cs` |
| Cluster vital do HUD | `Scripts/UI/Race/VitalClusterUI.cs` |
| Números flutuantes | `Scripts/UI/Race/FloatingNumbersUI.cs` |
| Arco de perigo dirigido | `Scripts/UI/Race/DangerArcDriver.cs`, `Scripts/Race/RaceThreats.cs` |
| Movimento de UI | `UIShineSweep.cs`, `UIFloatRise.cs` |
| Dados do HUD | `Scripts/UI/HUD/RaceHUDDataProvider.cs`, `RaceHudEvents.cs` |
| Grupo e matchmaking | `Scripts/UI/Frontend/Party/PartyModel.cs`, `MatchmakingService.cs` |
| Importador de layout | `Assets/Editor/Importer/` |
| Sprites v2 | `Art/UI/` — 15 substituídos, 5 novos, borders 9-slice corrigidos |

### Menu do pipeline

| Item | O que faz |
|---|---|
| `UI v2 ▸ 0 · Importar TUDO` | widgets + itens + as 6 telas, a partir dos JSON |
| `UI v2 ▸ 3 · Montar e ligar o HUD` | constrói o miolo do cluster vital e liga os binders |
| `UI v2 ▸ 4 · Capturar as telas` | PNGs 1920×1080 em `Docs/UI-v2/capturas/` |

Rodar **nessa ordem**, e um de cada vez: duas chamadas pesadas em sequência rápida fazem a fila do
MCP descartar a segunda **em silêncio** — foi assim que uma captura saiu de um prefab ainda não
ligado e pareceu uma regressão do wiring.

### Próximos passos

1. **Passe de wiring do frontend.** As 4 telas de menu têm a geometria exata e o visual de vidro
   correto, mas os binders ainda não estão ligados: as abas nascem todas âmbar (o primeiro estado
   é o ativo), os botões estão sem rótulo e as vagas do grupo, vazias. É o mesmo trabalho que o
   `HudV2Wiring` já faz para o HUD, aplicado a `PublicLobbyScreenUI`, `MatchmakingModalUI`,
   `CustomMatchScreenUI` e `GarageGridUI`.
2. **Conteúdo das linhas de classificação.** `Row_Standing` é só moldura na spec; posição, nome e
   tempo precisam existir como filhos para o `StandingsUI` preencher.
3. Adicionar `KartHealth` + `KartShieldAbility` ao prefab do kart e aplicar o HUD novo nas pistas.
4. Posicionar as caixas de cura nas bifurcações da MiniGolfeRun.
5. Montar a cena 3D da garagem para o lobby (a zona central já está livre e reservada).
6. Ligar `tnum` no font asset do Space Mono (as 5 pendências que o import reporta).
7. Apagar os builders do v1 e instalar o `UIGuardTest` (ver §15).

---

## 15 · Uma divergência assumida em relação ao handoff

O `UIGuardTest` do pacote proíbe `.anchoredPosition =` em **todo** `.cs` fora de
`Assets/Editor/Importer`. A intenção é certa — nada de layout escrito em C# —, mas a regra como
está escrita também proíbe **animação**: um toast que entra, um número que sobe e a varredura do
escudo movem `anchoredPosition` por definição, e o próprio pacote pede essas três animações.

A biblioteca de movimento do projeto (`UIAppear`, `UIPress`) já dependia disso antes do v2.

**Decisão:** adotar a guarda com uma segunda pasta isenta, `Scripts/UI/Motion/`, restrita a
componentes de movimento — que animam objetos existentes e nunca criam, dimensionam ou pintam. Isso
preserva a intenção da regra (a geometria vem do JSON, não do C#) sem proibir a única coisa que o
handoff pede em três lugares diferentes.

O padrão `new GameObject(` também precisa ser escopado: hoje ele pegaria
`FrontendFlow.cs:214`, que cria um contêiner de rede — não é UI.
