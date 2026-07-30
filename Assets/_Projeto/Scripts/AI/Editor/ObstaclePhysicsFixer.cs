#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PartyRacers.EditorTools
{
    /// <summary>
    /// Arruma a física dos obstáculos que se mexem (pás do moinho e tacos que dão a tacada).
    ///
    /// Dois problemas, os dois no moinho importado do Sketchfab:
    ///
    /// 1. Havia um MeshCollider em CADA nó da hierarquia — inclusive nos nós de agrupamento
    ///    (group_1_22, ID19_23, ID19_24...), que cobrem exatamente a mesma geometria das pás
    ///    logo abaixo. O carro batia nesse duplicado, que não tem ObstacleKnockback, em vez de
    ///    bater na pá. Daí o contato "sem reação".
    ///
    /// 2. As pás eram colliders sem Rigidbody movidos por script. O PhysX trata isso como
    ///    geometria estática teleportando: o contato não carrega a velocidade da pá. Com um
    ///    Rigidbody kinematic por pá, o motor passa a resolver o toque como superfície em
    ///    movimento — e cada pá continua recebendo o próprio OnCollisionEnter (o que não
    ///    aconteceria com um Rigidbody único no pivô, porque a mensagem iria para o pivô).
    /// </summary>
    public static class ObstaclePhysicsFixer
    {
        [MenuItem("Party Racers/Pista/Corrigir física dos obstáculos móveis")]
        public static void Corrigir()
        {
            var log = new StringBuilder();
            int removidos = 0, corpos = 0;

            // ---- moinho: tira collider redundante dos nós de agrupamento ----
            foreach (var rotor in Object.FindObjectsByType<ObstacleRotator>(FindObjectsInactive.Include))
            {
                var doRotor = rotor.GetComponentsInChildren<Collider>(true).ToList();
                foreach (var col in doRotor)
                {
                    bool ehFolha = col.transform.childCount == 0;
                    bool temKnockback = col.GetComponent<ObstacleKnockback>() != null;

                    // só a ponta da hierarquia (a pá de verdade) mantém collider
                    if (ehFolha || temKnockback)
                        continue;

                    log.AppendLine($"  removido collider redundante: {rotor.name}/{col.transform.name}");
                    Object.DestroyImmediate(col);
                    removidos++;
                }
            }

            // ---- pás e tacos: Rigidbody kinematic para o contato carregar a velocidade ----
            foreach (var kb in Object.FindObjectsByType<ObstacleKnockback>(FindObjectsInactive.Include))
            {
                if (kb.GetComponentsInChildren<Collider>(true).Length == 0)
                {
                    log.AppendLine($"  AVISO: '{kb.name}' tem ObstacleKnockback mas nenhum collider — nunca vai acertar ninguém");
                    continue;
                }

                // '??' não serve aqui: um componente ausente na Unity é "fake null" e passa
                // pelo operador, devolvendo uma referência que estoura ao ser usada.
                var rb = kb.GetComponent<Rigidbody>();
                if (rb == null) rb = kb.gameObject.AddComponent<Rigidbody>();

                bool mudou = !rb.isKinematic
                          || rb.interpolation != RigidbodyInterpolation.Interpolate
                          || rb.collisionDetectionMode != CollisionDetectionMode.ContinuousSpeculative;

                rb.isKinematic = true;          // quem manda é a animação, não a gravidade
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                if (mudou) { log.AppendLine($"  corpo kinematic: {kb.name}"); corpos++; }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log($"[obstáculos] {removidos} collider(es) redundante(s) removido(s), " +
                      $"{corpos} corpo(s) ajustado(s)\n{log}");
        }
    }
}
#endif
