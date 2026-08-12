# CLAUDE.md — Party Racers · porte da UI para Unity UI Toolkit

```
THIS IS A UI PORTING TASK, NOT A UI DESIGN TASK.
THE HTML REFERENCES ARE THE VISUAL AUTHORITY.
DO NOT REDESIGN THE INTERFACE.
USE UI TOOLKIT.
USE UXML FOR STRUCTURE.
USE USS FOR VISUAL STYLING AND LAYOUT.
USE C# FOR BEHAVIOR AND GAME INTEGRATION.
DO NOT CREATE INDIVIDUAL GAMEOBJECTS FOR UI ELEMENTS.
THE INTERFACE MUST REMAIN EDITABLE THROUGH UNITY UI BUILDER.
VALIDATE EVERY SCREEN AGAINST THE PROVIDED REFERENCES.
```

---

## 1. O que este pacote é

O design já está resolvido. `GoldenMaster/Party Racers v2.dc.html` é o **protótipo navegável aprovado** — não é inspiração, é o desenho. Seu trabalho é **portar**, não reinterpretar.

Este pacote não descreve a interface em prosa. Ele entrega **os arquivos UXML e USS já escritos**, com cada medida extraída do HTML. Você não vai decidir espaçamento, cor, raio, peso de fonte ou hierarquia: isso já está nos arquivos.

O que sobra para você: **instalar, ligar comportamento e validar.**

## 2. O que você NÃO tem liberdade para fazer

- melhorar o layout
- reorganizar componentes
- normalizar espaçamentos
- trocar cores
- alterar proporções
- escolher outras fontes
- simplificar elementos
- substituir um componente por uma alternativa que pareça melhor
- mexer na identidade visual
- criar uma versão "inspirada" no HTML

Se um valor parecer errado, **pare e pergunte**. Não conserte por conta própria e não invente valor que o pacote não deu — se faltar algo, `docs/08-PERGUNTAS-PARA-O-DONO.md` é onde isso vai.

## 3. Arquitetura

| Camada | Onde vive | Responsabilidade |
|---|---|---|
| Estrutura | `.uxml` | hierarquia dos elementos |
| Aparência e layout | `.uss` | tamanho, cor, tipografia, borda, estado |
| Comportamento | `.cs` | navegação, dados, rede, animação, estado |
| Cena | GameObjects | UIDocument, câmeras, karts, cenário, luz, VFX |

**Na Scene existe exatamente UM GameObject de UI por contexto:** um `UIDocument` para o frontend e um para o HUD de corrida. Nenhum botão, painel, texto ou card é GameObject.

Regra prática do que é UI e do que é 3D:

```
GAMEOBJECT 3D                        UI TOOLKIT
karts, cenário, plataforma,          menus, painéis, botões, textos,
anel do palco, sombra no chão,       cards, abas, chapinhas, HUD,
luzes, câmeras, VFX, partículas      overlays, modais, barras
```

O kart do lobby e o kart da garagem **não são UI**. As caixas tracejadas escritas "KART 3D" no HTML são marcações de posição do protótipo. Na Unity, elas são o modelo 3D. Já as **placas de nome** são UI, e seguem o kart por projeção (`StagePresenter.ToPanelPosition`).

## 4. Estrutura de pastas

Copie a pasta `UI/` inteira para `Assets/_Projeto/UI/`. É um mapa 1:1 — os `url()` dentro dos USS já apontam para lá, e mudar a raiz quebra **todos**.

```
Assets/_Projeto/UI/
  Core/
    Theme/    theme.uss  gaps.uss  base.uss        <- tokens + componentes
    Fonts/    os 7 font assets TMP (ver §5)
    Art/      Icons/  Powers/  Generated/  Brand/
  Shared/
    Templates/  Card_Mode  Row_GroupSlot  Row_Friend  Card_MatchSlot
                Chip_Stage  Blip  Row_CustomSlot  Chip_Tab  Card_Item
                Row_Standing  Toast
  Frontend/
    Shell/        Shell.uxml/.uss        <- barra superior, compartilhada
    Lobby/        Lobby.uxml/.uss
    Matchmaking/  Matchmaking.uxml/.uss  <- OVERLAY, não cena nova
    CustomMatch/  CustomMatch.uxml/.uss  <- OVERLAY
    Garage/       Garage.uxml/.uss
  HUD/
    Desktop/  RaceHUD.uxml/.uss
    Mobile/   RaceHUDMobile.uxml/.uss
  Runtime/
    Core/         UiStates  FrontendRouter  StagePresenter  PreviewStudio
    Controllers/  Lobby  Matchmaking  CustomMatch  Garage  RaceHUD
  Editor/
    UiPortGuardTest.cs                   <- quebra o build se a aparência voltar pro C#
```

## 5. Sistema de coordenadas

| Contexto | Referência | PanelSettings |
|---|---|---|
| Frontend e HUD PC | **1920 × 1080** | Scale With Screen Size · Match **0.5** |
| HUD celular | **2340 × 1080** paisagem | idem |

Todo número nos USS é px nessa referência. Não converta, não escale à mão.

## 6. Tipografia

Sete font assets TMP. **O peso não é uma propriedade no UI Toolkit** — cada peso é um asset próprio. `theme.uss` já tem uma classe por asset.

| Função | Fonte | Asset |
|---|---|---|
| Display: títulos, botões, abas, marca, números grandes | **Titan One** | `TitanOne-Regular SDF` |
| UI: nomes, valores, rótulos fortes | **Archivo** 400/600/700/800/900 | `Archivo-Regular/SemiBold/Bold/ExtraBold/Black SDF` |
| Mono: rótulos micro, código da sala, timers, metadados | **Space Mono** 400/700 | `SpaceMono-Regular/Bold SDF` |

Nunca substitua Titan One por Archivo Black. Nunca use fonte fora dessa lista. `Core/Fonts/README.md` tem o character set e as opções do Font Asset Creator.

Mínimo legível: **17px** em 1920×1080. Nada abaixo disso.

## 7. As cinco regras que decidem se o porte dá certo

1. **Nenhum valor visual em C#.** Cor, tamanho, espaçamento, fonte, raio, borda: só USS. O controller troca **classe** ou **visibilidade**. `UiPortGuardTest` verifica.
2. **Estado é irmão, não código.** Cada estado (`State_Player`, `State_Empty`, `State_Locked`…) já existe montado no UXML. O controller liga um e desliga os outros. Nunca pinte um estado por cima do outro.
3. **Quantidade é fixa no UXML.** 4 vagas de grupo, 16 vagas de sala, 16 linhas de sala privada, 5 blocos de HP, 3 de escudo, 5 etapas. O estado muda; a quantidade não.
4. **Um componente compartilhado tem uma definição.** `Shell` (barra superior) é a mesma no Lobby, na Garagem e na Sala privada. Se você copiar a barra em duas telas, o porte já falhou.
5. **Nada é concluído sem comparação visual.** Compilar sem erro não é concluir. Ver `docs/06-VALIDACAO.md`.

## 8. Ordem de leitura

| Documento | Para quê |
|---|---|
| `docs/01-PORTE-HTML-PARA-UITOOLKIT.md` | **leia antes de tudo** — o que o UI Toolkit não tem e a estratégia decidida para cada caso |
| `docs/02-COMPONENTES-COMPARTILHADOS.md` | quais elementos são reutilizáveis e onde aparecem |
| `docs/03-ESTADOS.md` | todos os estados de todas as telas |
| `docs/04-ASSETS.md` | onde está cada arquivo, para que serve, como importar |
| `docs/05-CENA-3D-E-UIDOCUMENT.md` | montagem da Scene, câmeras, RenderTexture |
| `docs/06-VALIDACAO.md` | o laço obrigatório de comparação via MCP |
| `docs/07-ORDEM-DE-IMPLEMENTACAO.md` | em que ordem construir |
| `docs/08-PERGUNTAS-PARA-O-DONO.md` | o que o HTML não responde — **não decida sozinho** |
| `Validation/README.md` | mapa das referências visuais |

## 9. Unity MCP

O MCP não é só um jeito de escrever arquivo. Use ativamente para: abrir cena, conferir hierarquia, ler o Console, checar erro de compilação, entrar em Play, trocar estado, capturar o Game View e **comparar com a referência**. O laço de validação do `docs/06` depende disso.
