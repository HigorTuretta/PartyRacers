using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace PartyRacers.EditorTools
{
    /// <summary>
    /// Devolve as fontes TMP dinâmicas ao estado vazio ao sair do play mode.
    ///
    /// POR QUE ISTO EXISTE
    /// Uma fonte com atlas <c>Dynamic</c> não guarda glifo nenhum no disco — ela rasteriza sob
    /// demanda, em tempo de execução, e vai gravando o resultado DENTRO do próprio .asset. Rodar o
    /// jogo no Editor por alguns segundos já basta para o arquivo engordar de alguns KB para vários
    /// MB de glifos e de textura de atlas.
    ///
    /// Esse conteúdo é puro cache: é reconstruído sozinho no próximo play e o
    /// <c>clearDynamicDataOnBuild</c> o descarta na hora de compilar. Ou seja, ele nunca precisou
    /// estar no repositório — mas, uma vez gravado, o git enxerga o .asset como modificado e ele
    /// entra em todo commit.
    ///
    /// O estrago não é o tamanho, é o merge: como cada máquina rasteriza um conjunto diferente de
    /// glifos, em ordem diferente, dois colaboradores produzem milhares de linhas divergentes no
    /// mesmo trecho do YAML. O merge do git não tem como reconciliar isso e despeja marcadores de
    /// conflito no meio de um arquivo de milhões de caracteres. Foi exatamente o que aconteceu com
    /// as 4 fontes deste projeto (134 marcadores em "Archivo Bold SDF.asset").
    ///
    /// Limpando ao sair do play mode, o arquivo versionado volta sozinho ao estado canônico e para
    /// de aparecer como modificado — então não há mais o que conflitar.
    ///
    /// CUSTO: o primeiro texto exibido no play seguinte rasteriza os glifos de novo. É trabalho de
    /// milissegundos e invisível na prática.
    /// </summary>
    [InitializeOnLoad]
    public static class LimpezaDeAtlasDeFonte
    {
        private const string ChavePreferencia = "PartyRacers.LimparAtlasDeFonteAoSairDoPlay";
        private const string CaminhoDoMenu = "Tools/PartyRacers/Fontes/Limpar atlas ao sair do play";
        private const string CaminhoDoMenuAgora = "Tools/PartyRacers/Fontes/Limpar atlas agora";

        private static bool Ativo
        {
            get => EditorPrefs.GetBool(ChavePreferencia, true);
            set => EditorPrefs.SetBool(ChavePreferencia, value);
        }

        static LimpezaDeAtlasDeFonte()
        {
            EditorApplication.playModeStateChanged -= AoMudarOPlayMode;
            EditorApplication.playModeStateChanged += AoMudarOPlayMode;
        }

        private static void AoMudarOPlayMode(PlayModeStateChange estado)
        {
            // Só depois de o play ter REALMENTE terminado. Limpar em ExitingPlayMode pegaria as
            // fontes ainda em uso por telas vivas, que voltariam a rasterizar durante a saída.
            if (estado == PlayModeStateChange.EnteredEditMode && Ativo)
                Limpar(false);
        }

        [MenuItem(CaminhoDoMenuAgora)]
        private static void LimparPeloMenu() => Limpar(true);

        [MenuItem(CaminhoDoMenu)]
        private static void AlternarAtivo() => Ativo = !Ativo;

        [MenuItem(CaminhoDoMenu, true)]
        private static bool ValidarAlternarAtivo()
        {
            Menu.SetChecked(CaminhoDoMenu, Ativo);
            return true;
        }

        /// <param name="avisarNoConsole">
        /// Só quando o usuário pediu pelo menu. No gatilho automático o silêncio é proposital: um
        /// log a cada saída do play vira ruído e some com o que importa no Console.
        /// </param>
        private static void Limpar(bool avisarNoConsole)
        {
            List<TMP_FontAsset> limpas = new List<TMP_FontAsset>();

            // Restrito a "Assets": fonte que mora em Packages é read-only, e sujar o cache de
            // pacote só produziria erro de importação.
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets" }))
            {
                string caminho = AssetDatabase.GUIDToAssetPath(guid);
                TMP_FontAsset fonte = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(caminho);

                if (fonte == null)
                    continue;

                // Uma fonte corrompida não pode derrubar a limpeza das outras — nem virar erro
                // recorrente no Console a cada saída do play mode.
                try
                {
                    if (!PrecisaLimpar(fonte))
                        continue;

                    // 'true' mantém a textura do atlas em 1x1. O nome do parâmetro engana: com
                    // 'false' o TMP RECRIA a textura no tamanho cheio (1024x1024) e serializa 1 MB
                    // de zeros — o arquivo continuaria mudando, que é o que queremos evitar.
                    fonte.ClearFontAssetData(true);
                    EditorUtility.SetDirty(fonte);
                    limpas.Add(fonte);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Fontes] Não consegui limpar '{caminho}': {e.Message}");
                }
            }

            if (limpas.Count == 0)
            {
                if (avisarNoConsole)
                    Debug.Log("[Fontes] Nenhum atlas dinâmico para limpar — os .asset já estão no estado versionado.");

                return;
            }

            AssetDatabase.SaveAssets();

            if (avisarNoConsole)
                Debug.Log($"[Fontes] Atlas dinâmico limpo em {limpas.Count} fonte(s): {string.Join(", ", limpas.ConvertAll(f => f.name))}.");
        }

        /// <summary>
        /// Fonte estática tem os glifos assados de propósito e é para continuar assim — mexer nela
        /// apagaria conteúdo real. Só as dinâmicas têm cache descartável.
        ///
        /// A checagem de "tem algo a limpar" evita reescrever no disco um .asset que já está limpo:
        /// sem ela, toda saída do play marcaria as 4 fontes como modificadas de novo — o problema
        /// que este script veio resolver.
        /// </summary>
        private static bool PrecisaLimpar(TMP_FontAsset fonte)
        {
            if (fonte.atlasPopulationMode == AtlasPopulationMode.Static)
                return false;

            if (fonte.glyphTable != null && fonte.glyphTable.Count > 0)
                return true;

            if (fonte.characterTable != null && fonte.characterTable.Count > 0)
                return true;

            // Mais de uma página de atlas, ou uma página maior que o 1x1 canônico, também é cache.
            //
            // Ler o array direto, e NÃO a propriedade 'atlasTexture': o getter dela faz
            // 'atlasTextures[0]' sem checar nada, então numa fonte com o array nulo ou vazio ele
            // estoura — e aqui isso aconteceria dentro de um hook de play mode, virando erro a cada
            // saída do play.
            Texture2D[] paginas = fonte.atlasTextures;
            if (paginas == null || paginas.Length == 0)
                return false;

            if (paginas.Length > 1)
                return true;

            Texture2D atlas = paginas[0];
            return atlas != null && (atlas.width > 1 || atlas.height > 1);
        }
    }
}
