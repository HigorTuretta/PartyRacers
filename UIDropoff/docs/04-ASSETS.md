# 04 · Assets

Tudo em `Assets/_Projeto/UI/Core/Art/`. Os `url()` dos USS já apontam para lá — mudar a raiz quebra todos.

## Import settings

Para **todos** os PNG deste pacote:

```
Texture Type ....... Sprite (2D and UI)
Sprite Mode ........ Single
Mesh Type .......... Full Rect
Filter Mode ........ Bilinear
Compression ........ None
Generate Mip Maps .. off
Wrap Mode .......... Repeat    <- OBRIGATÓRIO nos tileáveis (ver abaixo)
```

`Wrap Mode = Repeat` nos tracejados, listras e linhas do dial. Em `Clamp` o tile não emenda e aparece costura.

## Generated/ — o que o USS não faz nativamente

| Arquivo | Substitui | Uso no USS |
|---|---|---|
| `Dash_Tile_H.png` `Dash_Tile_V.png` | `border-style: dashed` | arestas de `.pr-dashed` — **repeat** |
| `Dash_Corner_R9…R26.png` | idem, cantos | 12 raios: 9 11 12 13 14 15 16 18 20 22 24 26 |
| `Dash_Circle_40/44/12.png` | círculo tracejado | vaga vazia do palco, slot de poder |
| `Grad_HP_Full/Hurt.png` | gradiente da vida | `.pr-bar__seg--hp` `--hurt` |
| `Grad_Shield*.png` (4) | gradientes do escudo | pronto, ativo, recarga, celular |
| `Grad_Modal.png` | fundo do modal | `.mm__modal` |
| `Grad_DialWell.png` | poço do dial | `.mm__dial` |
| `Grad_Progress_Amber.png` | barra de progresso | `.mm__progress-fill` |
| `Scrim_TopBar/Bottom/HudBottom.png` | gradiente de escurecimento | barra superior, rodapés |
| `Stripes_45_Amber_Ink.png` | faixa da marca (−45°) | `.pr-stripes-brand` — **repeat** |
| `Stripes_45_Repair.png` | listras de reparo | `.pr-stripes-repair` — **repeat** |
| `Stripes_45_White_Soft.png` | brilho diagonal do item | `.pr-stripes-gloss` — **repeat** |
| `Shine_Sweep.png` | varredura de luz | `.pr-shine` |
| `Glow_Radial_Amber.png` | glow do palco | `.pr-glow-amber` |
| `Shadow_Ellipse.png` | sombra elíptica de kart | UI de fallback; o ideal é blob shadow 3D |
| `Vignette_Red/Green/Blue.png` | `box-shadow: inset` | `.pr-vignette` — **9-slice 236** |
| `Tri_Down.png` | `clip-path` do bico da agulha | `.mm__needle-head` |
| `Icon_HealCross.png` | `clip-path` da cruz | `.pr-icon--heal` |

**As vinhetas são as únicas 9-slice do pacote.** Slice L/R/T/B = **236**, `-unity-slice-scale: 1`. Esticar sem slice distorce a espessura do brilho entre os eixos.

Os três `Stripes_*` são gerados por `(x + y) mod P`, então tileiam nos dois eixos. Nunca estique.

## Icons/ — 14 ícones brancos, coloridos por tint

`Icon_Check · Icon_Plus · Icon_Minus · Icon_Lock · Icon_Crown · Icon_Coin · Icon_Diamond · Icon_Copy · Icon_Play · Icon_Flag · Icon_Person · Icon_Menu · Icon_ArrowLeft · Icon_ArrowRight`

Cada um tem `.pr-icon--<nome>`. A cor vem de `.pr-icon--ink/cream/green/amber/muted` ou de um `-unity-background-image-tint-color` próprio.

Existem versões `.svg` na pasta. **Use o PNG** — SVG no UI Toolkit exige Vector Graphics e não vale o risco aqui.

| Ícone | Onde aparece |
|---|---|
| Check | PRONTO, EQUIPADO, etapa concluída, linha pronta |
| Plus | vaga convidável (grupo, sala, palco) |
| Minus | expulsar da sala privada |
| Lock | vaga indisponível, cosmético bloqueado |
| Crown | líder do grupo, anfitrião |
| Coin / Diamond | carteira da barra superior |
| Copy | copiar código da sala |
| Play | BUSCAR PARTIDA |
| Flag | placa SEU KART |
| Person | avatar placeholder, preview sem RenderTexture |
| Menu | HUD celular |
| ArrowLeft / Right | controles do HUD celular, carrossel de mapa |

## Powers/ — ícones de poder

`Power_<id>_Color.png`. Trocar o `background-image` de `Power_Icon` conforme o item sorteado é a **única** troca de imagem permitida em código.

## O que NÃO veio, e por quê

Os sprites 9-slice do pacote antigo (`UI_Panel_R26_Deep`, `UI_Button_R22_Green`, `UI_Card_R18_*`, `UI_Badge_*`, `UI_Modal_R36`, `UI_Dashed_*`) **não existem mais**. No UI Toolkit, painel, botão, card e chapinha são cor + `border-width` + `border-radius` no USS — nativo, sem textura, sem borda para calibrar, sem alpha assado.

Foi exatamente o alpha assado desses PNGs que deixou os painéis transparentes demais no porte anterior. O problema deixa de existir: agora a cor está num lugar só, o `theme.uss`.

## Fontes

`Core/Fonts/README.md` tem o character set e as opções do Font Asset Creator. Sete assets, um por peso. Nunca substitua Titan One.
