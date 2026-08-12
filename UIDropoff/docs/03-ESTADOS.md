# 03 · Estados

Estado **não** é variação de código: cada estado já existe montado no UXML como irmão. O controller liga um e desliga os outros (`UiStates.ShowOnly`). Nunca pinte um estado por cima do outro, nunca troque cor por código.

Uma tela só está pronta quando **todos** os estados desta lista foram vistos no Game View.

---

## Lobby

### Modo — `Card_Mode` × 3
| Estado | Nó | Efeito no grupo |
|---|---|---|
| escolhido | `State_On` | placa âmbar com sombra dura |
| não escolhido | `State_Off` | vidro com contorno fino |

O modo decide o **estado** das 4 vagas, nunca a quantidade:

| Modo | Vaga 0 | Vagas 1-3 |
|---|---|---|
| SOLO | `State_Player` | 3 × `State_Locked` ("MODO SOLO") |
| DUO | `State_Player` | 1 × `State_Empty` + 2 × `State_Locked` |
| SQUAD | `State_Player` | 3 × `State_Empty` (ou `State_Player` se ocupadas) |

### Vaga do grupo — `Row_GroupSlot`
| Estado | Nó | Aparência |
|---|---|---|
| jogador | `State_Player` | linha cheia, avatar, nome, meta |
| convidável | `State_Empty` | tracejado r13 `mute-22`, "+" e CONVIDAR, clicável |
| indisponível | `State_Locked` | fill `mute-06`, cadeado, "MODO SOLO". **Sem tracejado** |

Dentro de `State_Player`:
- `Badge_Leader` só na vaga do líder
- `Badge_Ready` (verde, "PRONTO") **ou** `Badge_Waiting` (âmbar, "AGUARDA") — nunca os dois
- pronto troca a borda da linha: classe `slot--ready`

**O nome tem largura máxima de 150px com elipse.** Sem isso ele passa por cima do badge LÍDER — defeito conhecido do porte anterior.

### Amigos — `Row_Friend`
| Estado | Classe do texto | Ponto de presença |
|---|---|---|
| online | `friend__state--online` | verde |
| ausente | `friend__state--away` | âmbar |
| Steam | `friend__state--steam` | azul |
| offline | `friend__state--offline` | cinza |

Ação à direita: `Btn_Invite` (verde, clicável) **ou** `Badge_Cant` ("NO GRUPO", inerte).
Duas abas: DO JOGO / STEAM — `lobby__friend-tab--on` / `--off`.

### Estado do grupo e busca
| Situação | `Group_Status` | Botão |
|---|---|---|
| falta gente confirmar | "AGUARDANDO N JOGADORES", classe `--waiting` (âmbar) | `Btn_Search_Blocked` com o motivo |
| todos prontos | "TODOS PRONTOS · PODE BUSCAR" (verde) | `Btn_Search` verde |

`Btn_Ready` e `Btn_Cancel` também se alternam: você pendente mostra FICAR PRONTO; você pronto mostra CANCELAR.

**O botão verde é o maior elemento clicável da tela.** Se empatar com o âmbar, a hierarquia se perdeu.

---

## Busca de partida

Overlay: a cena 3D e a barra superior continuam atrás, com scrim `rgba(6,8,24,.70)`.

### Etapas — `Chip_Stage` × 5
`PRONTOS · PROCURANDO · ENCONTRADOS · PREENCHENDO · CARREGANDO`

| Posição | Nó |
|---|---|
| anteriores | `State_Done` (verde + tique) |
| atual | `State_Now` (âmbar + ponto pulsante) — **exatamente uma** |
| seguintes | `State_Todo` (escuro + anel) |

| Etapa | Título | Agulha | Vagas |
|---|---|---|---|
| 1 AGUARDANDO GRUPO | "AGUARDANDO O GRUPO" | parada | só o seu grupo |
| 2 SINTONIZANDO | "SINTONIZANDO CANAL" | `Needle_Scan` varrendo | grupo + vazias |
| 3 ENCONTRADOS | "PILOTOS NA FREQUÊNCIA" | varrendo | humanos entrando |
| 4 PREENCHENDO | "PREENCHENDO COM BOTS" | `Needle_Lock` em 66% | bots completam |
| 5 ENCONTRADA | "PARTIDA ENCONTRADA" | travada | 16 cheias |

### Vaga — `Card_MatchSlot` × **16** (8 colunas × 2 linhas)
| Estado | Nó | Moldura |
|---|---|---|
| piloto | `State_Human` | borda tinta, fundo royal |
| do seu grupo | `State_Mate` | borda **âmbar** |
| bot | `State_Bot` | borda tinta, fundo violeta |
| livre | `State_Empty` | tracejado r14 `mute-20` |

**São 16, sempre.** 32 vagas em 4 linhas foi o defeito do porte anterior.

O timer mostra **só o tempo decorrido**. O limite de 40s é regra interna e **nunca aparece na tela**.

---

## Garagem

7 categorias: MODELO · COR · RODAS · FRENTE · TRASEIRA · TETO · ADESIVOS. Uma em `State_On`, as outras em `State_Off`.

### Card — `Card_Item`
| Estado | Nó | Moldura |
|---|---|---|
| equipado | `State_Equipped` | contorno verde 4px + tique no canto |
| selecionado | `State_Selected` | contorno âmbar 4px |
| livre | `State_Free` | contorno `mute-22` 2px, raridade no rodapé |
| bloqueado | `State_Locked` | contorno `mute-12`, cadeado, condição de desbloqueio |

`Badge_New` ("NOVO") só em `State_Free`, canto superior esquerdo.
Raridade colore o rótulo: `--raro` azul · `--epico` violeta · `--lendario` âmbar.

**Um card está sempre em exatamente um estado.** Card sem estado (moldura ausente) foi defeito do porte anterior.

### Câmera
Cada categoria tem uma pose. Ao trocar: blend 0.45s easeInOutCubic e `CameraChip` aparece com "CÂMERA · <POSE>", sumindo **1,2s depois do blend terminar**. Nunca fica parado e vazio.

**Paginação é proibida.** Sem setas `‹ ›`, sem contador `1 / 2`. A lista rola.

---

## Sala privada

Overlay, igual à busca.

### Linha — `Row_CustomSlot` × **16** (2 colunas × 8 linhas)
| Estado | Nó |
|---|---|
| jogador | `State_Player` |
| bot | `State_Bot` (violeta) |
| livre | `State_Empty` (tracejado r12 `mute-18`) |

Dentro de `State_Player`: `Badge_Host` no anfitrião; `Mark_Ready` (tique verde) **ou** `Mark_Waiting` (anel âmbar); `Btn_Kick` só para o anfitrião e nunca na própria linha.

**Altura da linha: 54px.** Vem do padding (10) + avatar (34) + bordas. Não ligue `ContentSizeFitter`, não force 80.

### Iniciar
| Situação | Nó |
|---|---|
| todos prontos | `Btn_Start` verde |
| faltam prontos | `Btn_Start_Blocked` tracejado + "AGUARDANDO PRONTOS" |

`SAIR DA SALA` é secundário e **nunca** vermelho de destaque.

Código da sala: 6 caracteres alfanuméricos maiúsculos. O espaçamento vem do `letter-spacing: 6.16px`, **nunca** de espaços digitados na string. `M F D C R R ▯` no porte anterior era glifo ausente no atlas + espaços literais.

---

## HUD de corrida (PC)

O HUD **não tem**: mira, velocímetro, minimapa, alerta central, HP dos outros jogadores. **O centro da tela fica livre** — é a linha de condução.

### Escudo — 4 estados
| Estado | Nó | Sinal |
|---|---|---|
| pronto | `Shield_Ready` | brilho pulsante 1.8s + varredura 2.4s + chapinha "Q PRONTO" |
| ativo | `Shield_Active` | segmentos brancos, borda clara, varredura 1s, "ATIVO 2.1s" |
| recarregando | `Shield_Cooling` | preenchimento parcial, marca, "8.4s". **Sem brilho, sem varredura** |
| vazio | `Shield_Broken` | 3 segmentos apagados |

A disponibilidade é sinalizada pela **própria barra**. Sem ícone, sem botão no PC.

### Vida — 5 blocos
`pr-bar__seg--hp` (verde) · `--hurt` (âmbar, o bloco em queda) · `--gone` (apagado).

### Danificado
`HP_Row` some, `Repair_Row` entra por 2,5s: poço vermelho, listras de reparo avançando, "DANIFICADO" e o contador.

### Estados globais
| Estado | O que muda |
|---|---|
| normal | nenhuma vinheta |
| dano | `Vignette_Danger` (0.25s easeInOut) + `Float_Damage` "−15" subindo |
| cura | `Vignette_Heal` + `Float_Heal` "+40" |
| escudo | `Vignette_Shield` + `Shield_Active` |
| escolha | `ChoicePrompt` (ITEM ← / → CURA), cards flutuando 1.6s |

### Poder
`Power_Filled` + `Power_Key` ("E FOGUETE") **ou** `Power_Empty` + `Power_Empty_Label` ("SEM ITEM"). Trocar `background-image` de `Power_Icon` é a **única** troca de imagem permitida em código.

### Avisos
Máx. 3 simultâneos, empilhando de baixo para cima. Entrada 0.18s, vida 2.5s, saída 0.25s.

---

## HUD de corrida (celular)

2340 × 1080, paisagem. Diferenças em relação ao PC:

- cluster vital **compacto e centrado no rodapé**, não no canto
- classificação com **3 linhas**, sem coluna de diferença
- sem avisos, sem chips de tempo, sem escolha item/cura
- controles touch nos dois cantos inferiores
- escudo **tem** botão, com anel pulsante 1.6s quando disponível. Em recarga o anel some — nunca troque o ícone
- **nenhum alvo de toque abaixo de 96px**
