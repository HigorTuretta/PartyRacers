# 01 · HTML → UI Toolkit

Tabela de equivalência e, para cada recurso que o UI Toolkit **não tem**, a estratégia já decidida. Nenhuma dessas decisões é sua.

## Equivalência direta

| HTML / CSS | UI Toolkit |
|---|---|
| `.html` | `.uxml` |
| `.css` | `.uss` |
| estado do protótipo (JS) | C# controller |
| `<div>` | `VisualElement` |
| `<span>`, texto | `Label` |
| `<button>` | `Button` |
| classe CSS | classe USS |
| flexbox | flexbox (Yoga) |
| `overflow-y: auto` | `ScrollView` |
| `:hover`, `:active` | `:hover`, `:active` |
| `position: absolute` + left/top/right/bottom | idêntico |
| `border-radius`, `border-width`, `border-color` | idêntico |
| `opacity` | idêntico |
| `text-shadow` | idêntico |
| `white-space`, `text-overflow: ellipsis` | idêntico |
| `transform: translateX(-50%)` | `translate: -50% 0` |
| `transform: rotate(-2deg)` | `rotate: -2deg` |

## Sete armadilhas que mudam o resultado

### 1. `flex-direction` padrão é **column**
No CSS é `row`. Toda linha do HTML precisa de `.pr-row` explícito no UXML. Esquecer isso empilha verticalmente o que devia ficar lado a lado — é o erro mais comum e o mais difícil de ver em diff.

### 2. Não existe `gap`
**Estratégia:** `gaps.uss`. Margem no valor do gap em **todos** os filhos + margem **negativa** do mesmo valor no contêiner, que cancela a sobra do último filho.

```
.gx-8  { margin-right: -8px; }      /* contêiner */
.gx-8 > * { margin-right: 8px; }    /* filhos    */
```

Funciona com lista dinâmica e não exige classe no primeiro filho. `gx-` para linha, `gy-` para coluna; os dois podem coexistir no mesmo elemento (grades com quebra).

### 3. Não existe `box-shadow`
A sombra dura (`0 6px 0 #0A0C22`) é a assinatura visual do jogo — está em todo botão, placa e chapinha. **Estratégia:** `.pr-hs` — a sombra é um **irmão desenhado antes**, porque no UI Toolkit filho nunca fica atrás do pai e não existe z-index.

```xml
<VisualElement class="pr-hs pr-hs--6">
    <VisualElement class="pr-hs__layer" style="border-radius: 16px;" />
    <Button class="pr-btn__face pr-btn--green" text="BUSCAR PARTIDA" />
</VisualElement>
```

O pressionar move a **face** 6px para baixo (`translate: 0 6px` em `:active`), cobrindo a sombra: é exatamente o `transform: translateY(6px)` do HTML. Duração 0.08s.

O raio da camada de sombra tem que ser declarado igual ao da face. Sombra com raio errado aparece como orelha escura no canto.

### 4. Não existe gradiente
**Estratégia:** textura em `Art/Generated/`, aplicada como `background-image`. Nenhum gradiente é inventado — cada um foi rasterizado do valor exato do HTML.

| Gradiente do HTML | Textura |
|---|---|
| HP cheio `#6BF2BC → #2FBB7E` | `Grad_HP_Full.png` |
| HP ferido `#FFD066 → #E09410` | `Grad_HP_Hurt.png` |
| escudo pronto `#DFF6FF → #35A7FF` | `Grad_Shield.png` |
| escudo ativo `#FFFFFF → #9BE0FF` | `Grad_Shield_Bright.png` |
| escudo recarga (50%/42%) | `Grad_Shield_Cool.png` |
| escudo celular `#9BE0FF → #35A7FF` | `Grad_Shield_Mobile.png` |
| modal `#161B48 → #0E1130` | `Grad_Modal.png` |
| poço do dial `#0A0C22 → #12163A` | `Grad_DialWell.png` |
| progresso `#FFB020 → #FF7A2B` | `Grad_Progress_Amber.png` |
| scrim da barra superior | `Scrim_TopBar.png` |
| scrim do rodapé (frontend) | `Scrim_Bottom.png` |
| scrim do rodapé (HUD) | `Scrim_HudBottom.png` |
| listras da marca (−45°, 22/22) | `Stripes_45_Amber_Ink.png` (tileável) |
| listras de reparo (−45°, 14/14) | `Stripes_45_Repair.png` (tileável) |
| brilho diagonal do item | `Stripes_45_White_Soft.png` (tileável) |
| varredura de luz | `Shine_Sweep.png` |
| glow radial âmbar | `Glow_Radial_Amber.png` |
| sombra elíptica de kart | `Shadow_Ellipse.png` (ou blob shadow 3D) |

As listras diagonais são geradas em função de `(x+y) mod P`, então **tileiam nos dois eixos**. Use `background-repeat: repeat`, nunca esticar.

### 5. Não existe `backdrop-filter: blur()`
Os painéis são "vidro": `rgba(10,12,34,0.82)` + blur 16.
**Estratégia decidida: só o alpha.** Não tente blur. Sobre a cena 3D do palco, 0,82 já separa a camada — o que dá a leitura de vidro é a combinação de alpha + contorno fino `rgba(155,165,215,0.20)`, e o contorno está nos USS.

Se depois quiserem blur de verdade, é um Render Feature de blur em RenderTexture, decisão de render pipeline — **fora do escopo deste porte** e não é para você abrir.

### 6. Não existe `border-style: dashed`
O tracejado aparece em toda vaga vazia, e foi o que quebrou pior no porte anterior.
**Estratégia:** `.pr-dashed` — composto de 4 arestas (tile repetido) + 4 cantos (arco tracejado), sobreposto ao elemento alvo sem interferir no layout dele.

```xml
<VisualElement class="pr-dashed pr-dashed--r13 pr-dashed--mute-22">
  <VisualElement class="pr-dashed__t" /><VisualElement class="pr-dashed__b" />
  <VisualElement class="pr-dashed__l" /><VisualElement class="pr-dashed__r" />
  <VisualElement class="pr-dashed__tl" /><VisualElement class="pr-dashed__tr" />
  <VisualElement class="pr-dashed__br" /><VisualElement class="pr-dashed__bl" />
</VisualElement>
```

Traço 14px, vão 10px, período 24px — igual ao HTML. Raios disponíveis: 9, 11, 12, 13, 14, 15, 16, 18, 20, 22, 24, 26. Círculos: `Dash_Circle_40/44/12`. A cor vem de `-unity-background-image-tint-color` na raiz (`--mute-22`, `--amber-45` etc).

**Por que não 9-slice:** o slice **estica** a fatia central. Se ela cai num traço, vira linha borrada; se cai num vão, a aresta desaparece. Determinístico e sempre feio. Aresta repetida não tem esse problema.

### 7. Não existe `clip-path`
Toda forma desenhada com clip-path no HTML virou sprite.

| Forma no HTML | Asset |
|---|---|
| bandeirinha (SEU KART) | `Icons/Icon_Flag.png` |
| tique (PRONTO, EQUIPADO, etapa concluída) | `Icons/Icon_Check.png` |
| triângulo de play (BUSCAR PARTIDA) | `Icons/Icon_Play.png` |
| bico da agulha do dial | `Generated/Tri_Down.png` |
| cruz de cura | `Generated/Icon_HealCross.png` |
| diamante da carteira | `Icons/Icon_Diamond.png` |
| moeda da carteira | `Icons/Icon_Coin.png` |
| cadeado | `Icons/Icon_Lock.png` |
| setas do celular | `Icons/Icon_ArrowLeft/Right.png` |
| menu do celular | `Icons/Icon_Menu.png` |
| ícone do poder | `Powers/Power_<id>_Color.png` |

Sprite branco + `-unity-background-image-tint-color` para colorir. **Nunca** desenhe forma com `VisualElement` vazio e borda: quadrado branco vazio foi um dos defeitos do porte anterior.

## Outras diferenças que já estão resolvidas nos arquivos

| Recurso | Situação |
|---|---|
| `line-height` | não existe. Entrelinha vem do font asset. Rótulo de uma linha não é afetado; texto corrido usa `white-space: normal` |
| `letter-spacing: .2em` | USS usa **px**. `theme.uss` tem a tabela de conversão pronta para todos os valores do HTML. Não recalcule à mão |
| `font: 800 14px Archivo` | shorthand não existe. Peso = font asset (§6 do CLAUDE.md) |
| `font-variant-numeric: tabular-nums` | Space Mono já é monoespaçada. Em Titan One e Archivo, ative *tabular figures* no font asset |
| `display: grid` | não existe. Traduzido para linhas explícitas com `flex-grow`, ou quebra (`flex-wrap`) com largura de célula pré-calculada. Nunca improvise a largura |
| `box-shadow: inset` (vinhetas do HUD) | textura 9-slice `Vignette_Red/Green/Blue.png`, slice 236. **Não** troque por stretch simples: a espessura do brilho distorce entre eixos |
| `::-webkit-scrollbar` | `.pr-scroll` em `base.uss` |
| SVG | os ícones existem em `.svg` e `.png`. **Use o PNG.** SVG no UI Toolkit exige Vector Graphics e não vale o risco aqui |

## Animações

O HTML tem 9 `@keyframes`. No UI Toolkit, animação é **comportamento**: C# escrevendo `style.translate`, `style.opacity`, `style.left` ou trocando classe com `transition`. Essas propriedades são as únicas liberadas pela guarda.

| Keyframe | Onde | O que animar | Duração / curva |
|---|---|---|---|
| `prSweep` | agulha do dial | `style.left` 2% → 98% | 2.6s easeInOutSine, ida e volta |
| `prBlip` | blip; vaga preenchida | scale 0.4 → 1.15 → 1 + opacity 0 → 1 | 0.5s / 0.35s easeOut |
| `prPulse` | pontos, anel do escudo, barras de sinal | opacity 0.35 → 1 | 1.0s / 1.4s / 1.6s easeInOut |
| `prBob` | cards da escolha item/cura | translateY 0 → −8px | 1.6s easeInOut (o do kart é 3D) |
| `prDanger` | vinheta vermelha | opacity 0.25 → 0.9 | 0.25s easeInOut |
| `prShine` | varredura de luz | translateX −120% → 320% | escudo pronto 2.4s easeInOut · ativo 1s linear · item 3s easeInOut |
| `prGlow` | barra de escudo pronta | opacity de um irmão de glow | 1.8s easeInOut |
| `prTick` | marca da recarga | opacity 0.55 → 1 | 0.9s easeInOut |
| `prSpin`, `prRadar` | não usados nas telas v2 | — | — |

Transições de tela: 0.22s easeOutQuad. Câmera da garagem: 0.45s easeInOutCubic.
