# Party Racers — pacote de assets (direção PLACA)

Tudo aqui é fonte pronta para importar na Unity. Referência visual completa: `Party Racers - PLACA.dc.html`.

## Estrutura

```
Frames/   molduras 9-slice (PNG)      → Sprite (2D and UI), Mesh Type: Full Rect, borders abaixo
Powers/   ícones de poder 512×512     → 3 variantes por poder: _Color, _Mono, _Gray
Icons/    ícones de interface         → SVG (fonte) + PNG 128×128 (importar)
Bars/     trilha e preenchimento      → 9-slice horizontal
Race/     overlays de corrida         → arco de perigo, pulso, card de aviso
Brand/    marca, placa de contagem, ícone do app
tokens.json  cores, contornos, sombras, raios, tipografia, timings
```

## Bordas 9-slice (campo Border no Sprite Editor: L, B, R, T)

| Arquivo | Tamanho | L | B | R | T |
|---|---|---|---|---|---|
| UI_Card_R18_*.png | 128×128 | 44 | 44 | 44 | 44 |
| UI_Panel_R26_*.png | 160×160 | 60 | 60 | 60 | 60 |
| UI_Modal_R36.png | 192×192 | 80 | 80 | 80 | 80 |
| UI_Button_R22_*.png | 144×144 | 52 | 64 | 52 | 52 |
| UI_Button_R22_Pressed_*.png | 144×144 | 52 | 54 | 52 | 62 |
| UI_Badge_R14_*.png | 96×96 | 36 | 36 | 36 | 36 |
| UI_Dashed_R18.png | 128×128 | 44 | 44 | 44 | 44 |
| Toast_Card.png | 128×128 | 40 | 40 | 40 | 40 |
| Bar_Track.png / Bar_Fill.png | 64×32 | 20 | 12 | 20 | 12 |
| Overlay_DangerArc*.png | 1024×1024 | 240 | 240 | 240 | 240 |

O botão já traz a sombra dura embutida na parte de baixo (por isso B > T). A variante `_Pressed` é o mesmo sprite com o corpo afundado — troque o sprite no clique, não mexa na posição do RectTransform.

## Regras de uso

- **Ícones (`Icons/`) são brancos puros** — dê a cor pelo campo Color do componente Image. Nunca recolora ícone no Photoshop.
- **Molduras já vêm coloridas** (uma por cor de corpo) porque o contorno `#0A0C22` não pode ser tingido junto. Se faltar uma cor, duplique o PNG do corpo mais próximo.
- **Poderes**: `_Color` no slot ativo, `_Mono` durante a recarga (com máscara de preenchimento radial em cima), `_Gray` para bloqueado.
- **Arco de perigo**: `Overlay_DangerArc.png` = ameaça se aproximando (pulso 0,8 s); `Overlay_DangerArc_Strong.png` = impacto iminente (pulso 0,25 s). Sem texto, sem ícone, sem placa no centro. `Overlay_DangerPulse.png` entra por baixo, ancorado na base, quando a ameaça vem de trás.
- **Contagem regressiva**: `Countdown_Plate.png` é só a placa; o dígito é TextMeshPro em Titan One por cima. Não há sprite de número.
- **Logotipo**: o lockup é a palavra PARTY em Titan One + a placa `Countdown_Plate` com RACERS dentro. Não existe PNG do logo fechado neste pacote — monte com TMP para manter nitidez em qualquer resolução.

## Fontes (baixar separado, licença livre)

- **Titan One** — display
- **Archivo** (600/700/800) — interface
- **Space Mono** — códigos e timers

Gerar 3 TMP Font Assets, atlas 1024×1024, sampling point size 90, padding 9, com fallback para acentos PT-BR (À Á Â Ã Ç É Ê Í Ó Ô Õ Ú).

## Import settings recomendados

- Texture Type: Sprite (2D and UI) · Sprite Mode: Single
- Filter: Bilinear · Compression: None para UI (ou RGBA32) · Generate Mip Maps: off
- Max Size: 512 (poderes/overlays), 128 (ícones e molduras)
- Pixels Per Unit: 100 · Canvas em Scale With Screen Size, referência 1920×1080, Match 0.5
