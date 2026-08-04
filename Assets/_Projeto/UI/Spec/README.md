# especificacao/

Fonte de verdade geométrica das telas. Substitui a descrição em prosa do handoff v1.

```
layout/
  Screen_Lobby.json            prioridade 1
  Screen_Matchmaking.json      prioridade 1
  Screen_Garage.json
  Screen_RaceHUD_PC.json
  Screen_CustomMatch.json
  Screen_RaceHUD_Mobile.json
  _widgets.json                prefabs compartilhados — construa PRIMEIRO
tokens-v2.json                 cores, contornos, sombras, raios, tipografia, movimento, regras de gameplay
```

Cada `Screen_*.json` traz:

| campo | o que é |
|---|---|
| `canvas` | resolução de referência e modo do CanvasScaler |
| `intent` | a intenção da tela em uma frase — resolve dúvidas de julgamento |
| `camera3D` | poses, blends e curvas da câmera da cena |
| `safeZone3D` | retângulo onde UI opaca não pode entrar |
| `nodes` | a árvore completa: âncora, pivô, offsets, tamanho, sprite, fonte, estados |

Unidades em pixels no canvas de referência. Ordem dos `children` é o z-order.
Leia `HANDOFF-CLAUDE-CODE-v2.md` seção 2 para a convenção completa.
