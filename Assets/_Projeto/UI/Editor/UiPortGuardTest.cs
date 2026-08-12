using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace PartyRacers.UI.Tests
{
    /// <summary>
    /// GUARDA DO PORTE PARA UI TOOLKIT.
    ///
    /// O porte anterior falhou porque a aparencia foi decidida em C#.
    /// Este teste quebra o build se voltar a acontecer.
    ///
    /// PROIBIDO em qualquer script fora de Assets/_Projeto/UI/Editor:
    ///   · style.backgroundColor / color / borderXColor / fontSize
    ///   · style.width / height / padding* / margin* / borderXWidth
    ///   · new Color( / new Color32( / Sprite.Create(
    ///   · Instantiate de Canvas / GameObject de UI
    ///
    /// PERMITIDO (comportamento, nao aparencia):
    ///   · style.display, style.visibility, style.opacity
    ///   · style.left / top   (SO para seguir objeto 3D projetado)
    ///   · style.translate / rotate  (animacao)
    ///   · style.width em % (barra de progresso / preenchimento)
    ///   · style.backgroundImage (troca de icone de poder)
    ///   · AddToClassList / RemoveFromClassList  <- a forma correta
    /// </summary>
    public class UiPortGuardTest
    {
        const string UiRoot = "Assets/_Projeto/UI";

        static readonly (string pattern, string why)[] Forbidden =
        {
            (@"style\.backgroundColor",  "cor pertence ao USS"),
            (@"style\.color\s*=",        "cor pertence ao USS"),
            (@"style\.border[A-Za-z]*Color", "cor pertence ao USS"),
            (@"style\.fontSize",         "tipografia pertence ao USS"),
            (@"style\.(padding|margin)", "espacamento pertence ao USS"),
            (@"style\.border[A-Za-z]*Width", "espessura pertence ao USS"),
            (@"new\s+Color(32)?\s*\(",   "cor pertence ao USS"),
            (@"Sprite\.Create\s*\(",     "sprite gerado em codigo"),
            (@"AddComponent<Image>",      "uGUI: a UI 2D e UI Toolkit"),
            (@"AddComponent<Canvas>",     "uGUI: a UI 2D e UI Toolkit"),
            (@"AddComponent<TextMeshProUGUI>", "uGUI: a UI 2D e UI Toolkit"),
            (@"GetComponent<RectTransform>",   "uGUI: a UI 2D e UI Toolkit"),
        };

        [Test]
        public void UiAparenciaNaoVemDeCodigo()
        {
            var files = Directory.GetFiles(UiRoot, "*.cs", SearchOption.AllDirectories)
                                 .Where(f => !f.Replace('\\', '/').Contains("/Editor/"))
                                 .ToArray();

            var violations = (from f in files
                              let text = File.ReadAllText(f)
                              from rule in Forbidden
                              from m in Regex.Matches(text, rule.pattern).Cast<Match>()
                              select $"{f}: {m.Value}  ->  {rule.why}").ToList();

            Assert.IsEmpty(violations,
                "A aparencia da UI voltou para o codigo:\n" + string.Join("\n", violations));
        }

        [Test]
        public void NaoExisteUxmlOrfaoNemUssOrfao()
        {
            var uxml = Directory.GetFiles(UiRoot, "*.uxml", SearchOption.AllDirectories);
            Assert.IsNotEmpty(uxml, "Nenhum UXML encontrado: a UI nao pode viver so em C#.");
        }
    }
}
