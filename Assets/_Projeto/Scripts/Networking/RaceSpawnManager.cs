using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.Networking
{
    // Fornece posições de largada suficientes para até 16 karts. Usa pontos de spawn definidos na
    // cena (filhos deste objeto ou a lista 'spawnPoints'); se não houver o bastante, completa com
    // uma grid procedural a partir deste transform. Assim a corrida sempre tem 16 vagas.
    public class RaceSpawnManager : MonoBehaviour
    {
        public static RaceSpawnManager Instance { get; private set; }

        [Header("Pontos de largada")]
        [Tooltip("Pontos explícitos. Se vazio, usa os filhos deste objeto; se faltar, completa com grid.")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private bool useChildrenAsSpawns = true;

        [Header("Grid procedural (preenche o que faltar até 16)")]
        [SerializeField] private int totalSpawns = RaceConstants.MaxPlayers;
        [SerializeField] private int columns = RaceConstants.GridColumns;
        [SerializeField] private float columnSpacing = RaceConstants.GridColumnSpacing;
        [SerializeField] private float rowSpacing = RaceConstants.GridRowSpacing;

        private readonly List<Pose> poses = new List<Pose>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            BuildPoses();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public int SpawnCount => poses.Count;

        public Pose GetSpawnPose(int index)
        {
            if (poses.Count == 0)
                BuildPoses();

            if (poses.Count == 0)
                return new Pose(transform.position, transform.rotation);

            index = Mathf.Clamp(index, 0, poses.Count - 1);
            return poses[index];
        }

        private void BuildPoses()
        {
            poses.Clear();

            List<Transform> explicitPoints = new List<Transform>();

            if (spawnPoints != null)
            {
                foreach (Transform t in spawnPoints)
                {
                    if (t != null)
                        explicitPoints.Add(t);
                }
            }

            if (useChildrenAsSpawns && explicitPoints.Count == 0)
            {
                foreach (Transform child in transform)
                    explicitPoints.Add(child);
            }

            foreach (Transform t in explicitPoints)
                poses.Add(new Pose(t.position, t.rotation));

            // Completa com grid procedural até atingir totalSpawns.
            int needed = Mathf.Max(totalSpawns, RaceConstants.MaxPlayers);

            for (int i = poses.Count; i < needed; i++)
                poses.Add(BuildGridPose(i));
        }

        private Pose BuildGridPose(int index)
        {
            int safeColumns = Mathf.Max(1, columns);
            int row = index / safeColumns;
            int column = index % safeColumns;

            float xOffset = (column - (safeColumns - 1) * 0.5f) * columnSpacing;
            float zOffset = -row * rowSpacing;

            Vector3 localPosition = new Vector3(xOffset, 0f, zOffset);
            Vector3 worldPosition = transform.TransformPoint(localPosition);

            return new Pose(worldPosition, transform.rotation);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.85f);
            int needed = Mathf.Max(totalSpawns, RaceConstants.MaxPlayers);

            for (int i = 0; i < needed; i++)
            {
                Pose pose = Application.isPlaying && poses.Count > 0 ? GetSpawnPose(i) : BuildGridPose(i);
                Gizmos.DrawWireCube(pose.position + Vector3.up * 0.5f, new Vector3(1.6f, 1f, 2.6f));
                Gizmos.DrawRay(pose.position + Vector3.up * 0.5f, pose.rotation * Vector3.forward * 1.5f);
            }
        }
#endif
    }
}
