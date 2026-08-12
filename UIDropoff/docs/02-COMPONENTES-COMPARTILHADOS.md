# 02 · Componentes compartilhados

Um componente = uma definição. Se a mesma coisa existir duas vezes, uma alteração futura só chega em metade das telas — foi o que aconteceu no porte anterior.

## Componentes de estrutura (UXML reutilizável)

| Componente | Arquivo | Onde aparece |
|---|---|---|
| **Shell / barra superior** | `Frontend/Shell/Shell.uxml` | Lobby, Garagem, Sala privada (e atrás da Busca) |
| **Card_Mode** | `Shared/Templates/Card_Mode.uxml` | Lobby (SOLO/DUO/SQUAD) |
| **Row_GroupSlot** | `Shared/Templates/Row_GroupSlot.uxml` | Lobby (4 vagas do grupo) |
| **Row_Friend** | `Shared/Templates/Row_Friend.uxml` | Lobby (lista de amigos, 2 abas) |
| **Card_MatchSlot** | `Shared/Templates/Card_MatchSlot.uxml` | Busca de partida (16 vagas) |
| **Chip_Stage** | `Shared/Templates/Chip_Stage.uxml` | Busca de partida (5 etapas) |
| **Blip** | `Shared/Templates/Blip.uxml` | Busca de partida (dial) |
| **Row_CustomSlot** | `Shared/Templates/Row_CustomSlot.uxml` | Sala privada (16 linhas) |
| **Chip_Tab** | `Shared/Templates/Chip_Tab.uxml` | Garagem (7 categorias) |
| **Card_Item** | `Shared/Templates/Card_Item.uxml` | Garagem (grade de cosméticos) |
| **Row_Standing** | `Shared/Templates/Row_Standing.uxml` | HUD PC (6 linhas) e HUD celular (3 linhas) |
| **Toast** | `Shared/Templates/Toast.uxml` | HUD PC |

`Row_Standing` é o único template usado nos dois HUDs. O celular **não** cria uma versão própria: reaproveita e ajusta pelo USS do HUD celular (`.hudm__standings .stand__other` etc). Essa é a forma correta de variar um compartilhado.

## Componentes de aparência (classes em `base.uss`)

| Classe | O que é | Variantes |
|---|---|---|
| `.pr-panel` | vidro: `rgba(10,12,34,.82)`, contorno fino, raio 18, padding 20 | `--88` `--90` |
| `.pr-hs` | sombra dura (substitui box-shadow) | `--3 --4 --5 --6 --7 --16` |
| `.pr-btn` | botão com sombra dura e pressionar | `--green --amber --dark --cream --danger` |
| `.pr-tab` | aba da barra superior | `--on --off` |
| `.pr-chip` | chip de categoria | `--on --off` |
| `.pr-badge` | chapinha | `--green --amber --violet --muted --solid --ready --waiting` |
| `.pr-wallet` | pílula de moeda/diamante | — |
| `.pr-avatar` | avatar | `--xs --sm --md --lg` + `.pr-presence` |
| `.pr-card` | card de cosmético | `--equipped --selected --free --locked` |
| `.pr-slot` | linha de jogador | `--filled --locked` |
| `.pr-dashed` | moldura tracejada | raios 9→26, círculos, 10 cores |
| `.pr-icon` | ícone sprite | 14 ícones + 5 tints |
| `.pr-bar` | barra segmentada | `--hp --hurt --gone --shield --shield-bright` |
| `.pr-vignette` | vinheta de estado | `--red --green --blue` |
| `.pr-scroll` | barra de rolagem fina | — |
| `.pr-section-label` | rótulo de seção (MODO, GRUPO, SALA…) | — |

## O que NÃO virou componente, de propósito

- **Botões individuais** não são template: o UXML não passa parâmetro, então um template de botão daria um botão sem texto configurável. Botão é escrito inline com as classes `.pr-hs` + `.pr-btn__face` + variante. É mais legível no UI Builder e não é duplicação: a aparência mora numa definição só.
- **Painéis** não são template pelo mesmo motivo: `.pr-panel` já é a definição única.
- **A grade** não é componente: cada tela tem contagem e largura de célula próprias, calculadas para o painel dela.

Não crie abstração além dessa lista. Também não duplique nada dela.
