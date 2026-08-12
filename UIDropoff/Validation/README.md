# Validation — Golden Master

Estas capturas vêm do protótipo aprovado, `GoldenMaster/Party Racers v2.dc.html`. Elas são **a autoridade visual**: a tela na Unity tem que chegar nelas, não o contrário.

| Pasta | Estados |
|---|---|
| `Lobby/` | `Squad_AguardandoConfirmacao` · `Squad_VocePendente` · `Duo_TodosProntos` · `Solo_TodosProntos` |
| `Matchmaking/` | `Step1_AguardandoGrupo` · `Step2_SintonizandoCanal` · `Step3_PilotosNaFrequencia` · `Step4_PreenchendoComBots` · `Step5_PartidaEncontrada` |
| `Garage/` | `Cat1_Modelo` → `Cat7_Adesivos` |
| `CustomMatch/` | `Sala_Padrao` |
| `HUD/` | `Normal` · `Dano` · `Danificado` · `Cura` · `Escudo` |
| `HUD_Mobile/` | `Normal` |

## Como usar

Capture o Game View em **resolução fixa** (1920×1080; 2340×1080 no HUD celular) e compare lado a lado. O laço completo está em `docs/06-VALIDACAO.md`.

As capturas incluem uma moldura escura em volta (o palco do protótipo). Compare o **conteúdo**, não a borda.

## Estado não coberto

Alguns estados não têm captura — escudo em recarga, escudo vazio, amigo offline, vaga expulsa. Nesses casos a autoridade é o HTML: abra `GoldenMaster/Party Racers v2.dc.html` no navegador e navegue até o estado. O protótipo tem controles de estado na lateral.

**Nunca** invente a aparência de um estado não coberto. Se o HTML também não cobrir, pergunte (`docs/08`).
