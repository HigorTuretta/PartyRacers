using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pool das nuvenzinhas de fumaça (<see cref="DriftPuffBubble"/>).
///
/// Um kart derrapando pede até ~24 puffs por frame; com 16 karts na pista o Instantiate/Destroy
/// direto virava milhares de alocações por segundo — o maior gerador de GC do jogo. Aqui os puffs
/// são reciclados: o custo de spawn cai para um SetActive + reposicionar.
///
/// O pool é por prefab (o kart e as bolas de golfe podem usar puffs diferentes) e sobrevive à troca
/// de cena, para não pagar o aquecimento de novo a cada corrida.
/// </summary>
public static class DriftPuffPool
{
    /// <summary>Teto por prefab: acima disso o excedente é destruído em vez de guardado.</summary>
    const int CapacidadePorPrefab = 600;

    static readonly Dictionary<DriftPuffBubble, Stack<DriftPuffBubble>> disponiveis =
        new Dictionary<DriftPuffBubble, Stack<DriftPuffBubble>>();

    static Transform raiz;

    // Com "Enter Play Mode sem domain reload" os estáticos não são zerados sozinhos.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Resetar()
    {
        disponiveis.Clear();
        raiz = null;
    }

    public static DriftPuffBubble Obter(DriftPuffBubble prefab, Vector3 posicao, Quaternion rotacao)
    {
        if (prefab == null)
            return null;

        if (!disponiveis.TryGetValue(prefab, out Stack<DriftPuffBubble> pilha))
        {
            pilha = new Stack<DriftPuffBubble>();
            disponiveis[prefab] = pilha;
        }

        while (pilha.Count > 0)
        {
            DriftPuffBubble reciclado = pilha.Pop();

            // Pode ter sido destruído por fora (troca de cena, limpeza manual).
            if (reciclado == null)
                continue;

            Transform t = reciclado.transform;
            t.SetPositionAndRotation(posicao, rotacao);
            reciclado.gameObject.SetActive(true);
            return reciclado;
        }

        DriftPuffBubble novo = Object.Instantiate(prefab, posicao, rotacao, GarantirRaiz());
        novo.MarcarComoDoPool(prefab);
        return novo;
    }

    /// <summary>Chamado pelo próprio puff quando a vida acaba.</summary>
    public static void Devolver(DriftPuffBubble puff, DriftPuffBubble prefab)
    {
        if (puff == null)
            return;

        if (prefab == null || !disponiveis.TryGetValue(prefab, out Stack<DriftPuffBubble> pilha))
        {
            Object.Destroy(puff.gameObject);
            return;
        }

        if (pilha.Count >= CapacidadePorPrefab)
        {
            Object.Destroy(puff.gameObject);
            return;
        }

        puff.gameObject.SetActive(false);
        puff.transform.SetParent(GarantirRaiz(), false);
        pilha.Push(puff);
    }

    static Transform GarantirRaiz()
    {
        if (raiz != null)
            return raiz;

        var go = new GameObject("[DriftPuffPool]");
        Object.DontDestroyOnLoad(go);
        raiz = go.transform;
        return raiz;
    }
}
