<#
.SYNOPSIS
    Ensina o git a mesclar cenas, prefabs e .asset do Unity usando o UnityYAMLMerge.

.DESCRIPTION
    RODE UMA VEZ POR CLONE. Depois disso pode esquecer que existe.

    O .gitattributes deste repositório já manda todo arquivo YAML do Unity (*.unity, *.prefab,
    *.asset, *.mat, *.controller, *.meta ...) ser mesclado com o driver "unityyamlmerge". Só que
    "driver" ali é apenas um NOME: quem diz o que esse nome executa é a configuração local do git,
    e ela não pode ser versionada — o git recusa comandos de merge vindos de dentro de um clone,
    senão clonar um repositório qualquer executaria código arbitrário na sua máquina.

    Enquanto o nome não aponta para lugar nenhum, o git NÃO avisa: ele cai calado no merge de texto
    linha a linha. Num YAML do Unity isso é péssimo — duas edições em propriedades vizinhas do mesmo
    objeto viram conflito, e o arquivo volta cheio de marcadores <<<<<<< no meio de megabytes de
    dados serializados. Com o driver ligado, as mesmas duas edições entram sem conflito nenhum.

    CUIDADO: configuração pela metade é PIOR que nenhuma. Se 'merge.unityyamlmerge.name' existir
    sem 'merge.unityyamlmerge.driver', o git aborta com "custom merge driver unityyamlmerge lacks
    command line" e nem começa o merge. Este script grava as três chaves de uma vez.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools/configurar-merge-do-unity.ps1
#>

[CmdletBinding()]
param(
    # Caminho do UnityYAMLMerge.exe, se o Unity estiver instalado fora do padrão do Hub.
    [string] $CaminhoDaFerramenta
)

$ErrorActionPreference = 'Stop'

$raizDoRepo = (git rev-parse --show-toplevel)
if (-not $?) { throw "Rode este script de dentro do repositório." }

# ---------------------------------------------------------------- localizar o UnityYAMLMerge

function Resolver-Ferramenta {
    param([string] $Informado)

    if ($Informado) {
        if (-not (Test-Path $Informado)) { throw "Não achei o UnityYAMLMerge em: $Informado" }
        return (Resolve-Path $Informado).Path
    }

    # A versão do Editor que ESTE projeto usa. Pegar a versão certa importa: o formato serializado
    # muda entre versões do Unity, e mesclar com a ferramenta de outra versão pode errar.
    $arquivoDeVersao = Join-Path $raizDoRepo 'ProjectSettings/ProjectVersion.txt'
    $versao = $null
    if (Test-Path $arquivoDeVersao) {
        $linha = Select-String -Path $arquivoDeVersao -Pattern '^m_EditorVersion:\s*(.+)$'
        if ($linha) { $versao = $linha.Matches[0].Groups[1].Value.Trim() }
    }

    $candidatos = @()
    if ($versao) {
        $candidatos += "C:/Program Files/Unity/Hub/Editor/$versao/Editor/Data/Tools/UnityYAMLMerge.exe"
    }

    # Registro do instalador: cobre quem instalou fora do "Program Files".
    try {
        Get-ItemProperty 'HKLM:\SOFTWARE\Unity Technologies\Installer\*' -ErrorAction Stop |
            ForEach-Object {
                $local = $_.'Location x64'
                if ($local) { $candidatos += (Join-Path $local 'Editor/Data/Tools/UnityYAMLMerge.exe') }
            }
    } catch { }

    foreach ($c in $candidatos) {
        if (Test-Path $c) { return (Resolve-Path $c).Path }
    }

    throw ("Não achei o UnityYAMLMerge.exe" + $(if ($versao) { " para o Unity $versao" } else { "" }) +
           ". Passe o caminho: -CaminhoDaFerramenta '<...>/Editor/Data/Tools/UnityYAMLMerge.exe'")
}

$ferramenta = (Resolver-Ferramenta -Informado $CaminhoDaFerramenta) -replace '\\', '/'
Write-Host "UnityYAMLMerge: $ferramenta"

# ---------------------------------------------------------------- gravar a configuração

# Aspas SIMPLES em volta do caminho: o git roda o driver por um shell, e sem elas o "Program Files"
# quebra no espaço (o git guardaria só "C:/Program").
#
# Ordem dos argumentos: <base> <left> <right> [dest], onde a própria ferramenta chama left="theirs"
# e right="mine". Traduzindo para os marcadores do git: %O=base, %B=theirs, %A=ours-e-saída.
#   -h              modo headless, sem caixa de diálogo (é um merge automático, ninguém está olhando)
#   -p              pré-merge: resolve o que der e deixa marcado só o que sobrou
#   --force         mescla mesmo em extensão que ela não reconhece (.asset, .meta ...)
#   --fallback none NÃO tenta abrir um merge tool gráfico no meio de um 'git merge'
$comando = "'$ferramenta' merge -h -p --force --fallback none %O %B %A %A"

git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config merge.unityyamlmerge.driver $comando
git config merge.unityyamlmerge.recursive binary

$gravado = (git config --get merge.unityyamlmerge.driver)
if ($gravado -ne $comando) {
    throw "A configuração não foi gravada como esperado.`n  esperado: $comando`n  gravado : $gravado"
}

Write-Host ""
Write-Host "Pronto. Merge do Unity configurado para este clone." -ForegroundColor Green
Write-Host "  git config --get merge.unityyamlmerge.driver   # para conferir depois"
