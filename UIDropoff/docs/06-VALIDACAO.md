# 06 · Validação

Compilar sem erro **não é** concluir. Uma tela só está pronta quando foi comparada com a referência e as divergências foram corrigidas.

## O laço, por tela e por estado

1. abrir a cena pelo MCP
2. entrar em Play
3. configurar o estado (ver `03-ESTADOS.md`)
4. Game View em **resolução fixa** — 1920×1080, ou 2340×1080 no HUD celular
5. capturar
6. abrir a referência correspondente em `Validation/`
7. comparar lado a lado
8. listar as divergências
9. corrigir **no USS ou no UXML**, nunca em C#
10. repetir até não sobrar divergência

**Free Aspect não serve.** Distorce proporção e esconde erro de layout. Foi assim que os prints do porte anterior chegaram e a leitura ficou impossível.

## Checklist por tela

Passe em todas, sempre:

- [ ] nenhum objeto 3D identificável através de um painel
- [ ] nenhuma moldura tracejada com aresta faltando ou canto solto
- [ ] nenhum quadrado branco, nenhum `▯`, nenhuma chapinha vazia
- [ ] nenhum texto encostando ou passando por cima de outro
- [ ] nenhum elemento cortado pela borda da tela
- [ ] todo texto ≥ 17px
- [ ] botão primário é o maior elemento clicável da tela
- [ ] todos os cliques respondem em Play

Específicos:

| Tela | Verificação |
|---|---|
| Lobby | zona franca `x[470,1450] y[300,860]` sem UI opaca · 4 vagas com estado correto por modo · 3 botões na barra |
| Garagem | kart **e chão** em x=1341 · 7 categorias em linha · **zero** setas `‹ ›` e zero `1/2` |
| Busca | **exatamente 16** vagas, 8×2 · a cena 3D aparece atrás do scrim · o limite de 40s não aparece |
| Sala privada | **exatamente 16** linhas, altura 54 · código sem `▯` e sem espaço literal |
| HUD PC | centro da tela livre · 4 estados do escudo · 5 blocos de vida |
| HUD celular | nenhum alvo de toque < 96px · cluster centrado no rodapé |

## Guarda automática

`UI/Editor/UiPortGuardTest.cs` quebra o build se a aparência voltar para o C#. Ele proíbe, fora de `Editor/`:

```
style.backgroundColor · style.color · style.border*Color · style.fontSize
style.width/height · style.padding* · style.margin* · style.border*Width
new Color( · new Color32( · Sprite.Create(
AddComponent<Image/Canvas/TextMeshProUGUI> · GetComponent<RectTransform>
```

Permitido, porque é comportamento e não aparência:

```
style.display · style.visibility · style.opacity
style.left / top          (só para seguir objeto 3D projetado)
style.translate / rotate  (animação)
style.width em %          (preenchimento de barra)
style.backgroundImage     (troca de ícone de poder)
AddToClassList / RemoveFromClassList   <- a forma correta de tudo o mais
```

Se o teste falhar, **ache e desfaça** — não afrouxe o teste.

## O que reportar ao terminar cada tela

1. captura em resolução fixa, um por estado
2. a checklist marcada
3. `UiPortGuardTest` verde
4. divergências que você **não** conseguiu resolver, com a captura ao lado da referência
5. perguntas que surgiram (vão para `08`)
