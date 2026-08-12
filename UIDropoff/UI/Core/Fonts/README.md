# Fontes

Sete font assets TMP. **O peso não é uma propriedade no UI Toolkit** — cada peso é um asset próprio, e `theme.uss` tem uma classe por asset.

| Arquivo TTF | Font asset | Função |
|---|---|---|
| `TitanOne-Regular.ttf` | `TitanOne-Regular SDF` | títulos, botões, abas, marca, números grandes |
| `Archivo-Regular.ttf` | `Archivo-Regular SDF` | texto corrido |
| `Archivo-SemiBold.ttf` | `Archivo-SemiBold SDF` | "+" e sinais |
| `Archivo-Bold.ttf` | `Archivo-Bold SDF` | nomes secundários, rótulos |
| `Archivo-ExtraBold.ttf` | `Archivo-ExtraBold SDF` | nomes, valores, carteira |
| `Archivo-Black.ttf` | `Archivo-Black SDF` | chapinhas, estado do grupo |
| `SpaceMono-Regular.ttf` | `SpaceMono-Regular SDF` | metadados, ticks |
| `SpaceMono-Bold.ttf` | `SpaceMono-Bold SDF` | rótulos de seção, código da sala, timers |

Todas com licença OFL (Google Fonts). Se algum `.ttf` não estiver no repositório, baixe e **commite** em `Assets/_Projeto/UI/Core/Fonts/`.

**Não substitua Titan One por Archivo Black.** A marca e os botões perdem a identidade.

## Font Asset Creator

`Window > TextMeshPro > Font Asset Creator`, para cada uma:

```
Atlas Resolution ..... 1024 x 1024
Sampling Point Size .. 64
Padding .............. 8
Render Mode .......... SDFAA
Character Set ........ Custom Range
```

Range:

```
32-126,160-255,0x2018-0x201D,0x2022,0x00B7,0x2026,0x00BA,0x00AA,0x25CF,0x2190,0x2192,0x2039,0x203A,0x2212
```

O que cada faixa cobre e por quê:

| Faixa | Cobre | Onde aparece |
|---|---|---|
| `32-126` | ASCII | tudo |
| `160-255` | acentuação latina | FARÓIS, POSIÇÕES, ÚLT, CÂMERA, ANFITRIÃO |
| `0x00B7` | `·` ponto médio | "TODOS PRONTOS · PODE BUSCAR", "CANAL · 88.4 FM" |
| `0x00BA` | `º` ordinal | posição no HUD |
| `0x25CF` | `●` círculo cheio | pips dos cards de modo |
| `0x2190` `0x2192` | `←` `→` | escolha item/cura |
| `0x2039` `0x203A` | `‹` `›` | carrossel de mapa |
| `0x2212` | `−` menos matemático | número de dano |
| `0x2018-0x201D` `0x2022` `0x2026` | aspas, bala, reticências | texto corrido |

Glifo fora do atlas vira `▯`. Foi assim que o código da sala apareceu quebrado no porte anterior.

Depois de gerar, em **cada** font asset: **Fallback Font Assets → `Archivo-Regular SDF`**. Assim nenhum caractere inesperado vira caixinha.

Nos assets de Titan One e Archivo, ative **tabular figures** se disponível — timers e contagens não podem "dançar" entre quadros. Space Mono já é monoespaçada.
