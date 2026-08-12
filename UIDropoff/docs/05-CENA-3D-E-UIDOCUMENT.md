# 05 · Cena 3D e UIDocument

## O que é UI e o que é 3D

```
GAMEOBJECT 3D                          UI TOOLKIT
karts, cenário, plataforma do palco,   menus, painéis, botões, textos,
anel tracejado do chão, sombra,        cards, abas, chapinhas, barras,
luzes, câmeras, VFX, partículas        HUD, overlays, modais
```

As caixas tracejadas escritas "KART 3D" no HTML são marcação de posição do protótipo. Na Unity elas são o modelo. **Não** transforme kart, chão, luz ou câmera em elemento de UI.

As **placas de nome** são UI e seguem o kart por projeção — `StagePresenter.ToPanelPosition` converte a posição do anchor 3D para coordenada de painel. Escrever `style.left`/`style.top` nesse caso é a única exceção liberada pela guarda, porque a origem é a cena e não um valor de design.

## GameObjects da cena Frontend

| GameObject | Componente | Papel |
|---|---|---|
| `UI_Frontend` | `UIDocument` + `FrontendRouter` | **único** GameObject de UI do frontend |
| `Stage` | `StagePresenter` | raiz do palco 3D |
| `Stage/PlayerKart` | — | anchor do kart do jogador |
| `Stage/MateKart_0…2` | — | anchors dos acompanhantes |
| `Stage/Platform`, `Stage/Ring`, `Stage/Glow` | — | chão, anel, brilho |
| `Stage/Camera` | `Camera` | câmera do palco |
| `Stage/Pose_Lobby`, `Pose_Garage`, `Pose_OrbitFar` | `Transform` | poses de câmera, posicionadas à mão |
| `PreviewStudio` | `PreviewStudio` | rig de retrato (prefab) |

Na cena de corrida: `UI_RaceHUD` com `UIDocument` + `RaceHUDController`. Um só.

## PanelSettings

| Painel | Reference Resolution | Screen Match |
|---|---|---|
| `PS_Frontend` | 1920 × 1080 | Scale With Screen Size · Match **0.5** |
| `PS_RaceHUD` | 1920 × 1080 | idem |
| `PS_RaceHUD_Mobile` | 2340 × 1080 | idem |

`Sort Order`: frontend 0, HUD 0, overlays dentro do mesmo documento (não crie um segundo UIDocument para modal).

## Centro do palco — a regra que quebrou o porte anterior

| Tela | Centro em X (canvas 1920) |
|---|---|
| Lobby, Sala privada | **960** |
| **Garagem** | **1341** |

1341 é o centro do vão livre à direita do painel de itens, que termina em x=762.

**O chão acompanha o kart.** Plataforma, anel tracejado e glow compartilham o mesmo centro do kart. No porte anterior o kart foi deslocado para 1341 e o chão ficou em 960 — o kart passou a parecer flutuando e metade do anel sumiu atrás do painel.

A `CameraChip` da garagem também está em 1341 (`left: 1341px; translate: -50% 0`).

## Zona franca do lobby

`x[470, 1450] · y[300, 860]` — nenhuma UI opaca. É onde os karts aparecem. Os painéis laterais param antes: coluna esquerda termina em 448, painel de amigos começa em 1484.

## Retrato 3D na UI — RenderTexture, não Sprite

A miniatura do kart e o preview de cada card da garagem são **o modelo que o jogador montou agora**. Nenhum sprite pode existir para isso.

```
Componente ..... background-image alimentado por RenderTexture
                 (ou RawImage equivalente). NUNCA Sprite.Create.
RT ............. 256×256, RGB32, depth 16, AA 2
Pool ........... 16 RTs reaproveitadas; libera ao sair do viewport
Cadência ....... sob demanda, no máximo 1 render por frame
Fallback ....... enquanto não renderizou: o fundo do USS + Icon_Person.
                 Nunca retângulo branco.
Moldura ........ continua sendo USS. A RT vive DENTRO do card.
```

### PreviewStudio é um prefab

Rig montado à mão e commitado em `Assets/_Projeto/Prefabs/PreviewStudio.prefab`: câmera + 3 luzes + turntable + layer dedicada. O script só instancia, posiciona a câmera e chama `Render()`.

| Luz | Tipo | Intensidade | Cor | Rotação (X,Y,Z) |
|---|---|---|---|---|
| Key | Directional | **1.15** | `#FFF4E2` | `35, −40, 0` |
| Fill | Directional | **0.42** | `#9BB4FF` | `12, 145, 0` |
| Rim | Directional | **0.78** | `#35A7FF` | `−8, 195, 0` |

Ambient: cor plana `#1A1E44`, intensidade 0.35.
Câmera: Perspective, FOV **28**, Clear Flags = Solid Color com **alpha 0** (a RT precisa de fundo transparente para o card aparecer atrás), Culling Mask só na layer `KartPreview`.
Turntable parado por padrão; gira 12°/s apenas no card em foco.

## Overlays

Busca de partida e Sala privada são **overlays**, não cenas novas. A cena 3D e a barra superior continuam renderizando atrás; o que muda é: painéis do lobby desativados, scrim ligado, câmera em `OrbitSlowFar`.

Se você trocar de cena, desfaça.
