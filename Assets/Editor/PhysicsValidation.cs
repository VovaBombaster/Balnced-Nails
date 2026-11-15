using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NailBalance.Editor
{
    public static class PhysicsValidation
    {
        [Serializable] public class Solution { public int level, task; public float[] x, angle; public float stableSeconds; public int supportedNails; }
        [Serializable] public class Results { public List<Solution> solutions = new List<Solution>(); }

        [MenuItem("Nail Balance/Validate eight physical puzzles")]
        public static void Run()
        {
            SimulationMode2D previous = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Results results = new Results();
            try
            {
                NailPiece prefab = AssetDatabase.LoadAssetAtPath<NailPiece>("Assets/Prefabs/Nail.prefab");
                for (int i = 0; i < 8; i++)
                {
                    PuzzleDefinition puzzle = AssetDatabase.LoadAssetAtPath<PuzzleDefinition>($"Assets/Puzzles/Level{i / 4 + 1}_Task{i % 4 + 1}.asset");
                    Solution found = null;
                    for (int attempt = 0; attempt < 40 && found == null; attempt++) found = TryPuzzle(prefab, puzzle, i, attempt);
                    if (found == null) throw new Exception($"No stable physical solution for task {i / 4 + 1}.{i % 4 + 1}");
                    results.solutions.Add(found);
                    Debug.Log($"PHYSICS PASS {found.level}.{found.task}: supported={found.supportedNails}, stable={found.stableSeconds:F2}s, x=[{string.Join(",", found.x)}], angle=[{string.Join(",", found.angle)}]");
                }
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/physics-solutions.json", JsonUtility.ToJson(results, true));
                Debug.Log("NAIL_BALANCE: All eight tasks solved by ordinary drops in Unity Physics2D, with 3 continuous seconds of real support contacts.");
            }
            finally
            {
                Physics2D.simulationMode = previous;
                EditorSceneManager.OpenScene("Assets/Scenes/NailBalance.unity");
            }
        }

        static Solution TryPuzzle(NailPiece prefab, PuzzleDefinition puzzle, int index, int attempt)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene unloads assets not referenced by a scene; reload the test inputs afterwards.
            prefab = AssetDatabase.LoadAssetAtPath<NailPiece>("Assets/Prefabs/Nail.prefab");
            puzzle = AssetDatabase.LoadAssetAtPath<PuzzleDefinition>($"Assets/Puzzles/Level{index / 4 + 1}_Task{index % 4 + 1}.asset");
            GameObject platform = UnityEngine.Object.Instantiate(puzzle.supportPrefab, new Vector3(0, -1.65f, 0), Quaternion.identity);
            platform.transform.localScale = new Vector3(puzzle.supportWidth, 1, 1);
            HingeJoint2D hinge = platform.GetComponent<HingeJoint2D>();
            if (hinge) hinge.connectedAnchor = new Vector2(0, -1.65f);
            SwingSupport swing = platform.GetComponent<SwingSupport>();
            var nails = new List<NailPiece>(); var supported = new HashSet<Rigidbody2D>();
            Solution result = new Solution { level = index / 4 + 1, task = index % 4 + 1, x = new float[puzzle.nails.Length], angle = new float[puzzle.nails.Length] };
            var random = new System.Random(800 + attempt);
            float elapsed = 0, stable = 0;
            for (int n = 0; n < puzzle.nails.Length; n++)
            {
                float angle = attempt == 0 ? 0 : n % 2 * 180;
                float x = attempt < 2 ? 0 : attempt < 5 ? (n % 2 == 0 ? 1 : -1) * (attempt - 1) * 0.1f : (float)(random.NextDouble() - 0.5) * 0.55f;
                if (attempt >= 10) angle = random.Next(2) * 180;
                result.x[n] = x; result.angle[n] = angle;
                NailPiece nail = UnityEngine.Object.Instantiate(prefab, new Vector3(x, 2.35f, 0), Quaternion.Euler(0, 0, angle));
                nail.Prepare(puzzle.nails[n]); nail.transform.rotation = Quaternion.Euler(0, 0, angle); nail.Release(); nails.Add(nail);
                Physics2D.SyncTransforms();
                int steps = n == puzzle.nails.Length - 1 ? 600 : 100;
                for (int s = 0; s < steps; s++)
                {
                    if (swing) swing.Drive(elapsed);
                    Physics2D.Simulate(0.02f); elapsed += 0.02f;
                    foreach (NailPiece piece in nails) if (piece.transform.position.y < -4.6f) return null;
                    if (n == puzzle.nails.Length - 1)
                    {
                        stable = GameController.IsConstructionStable(nails, platform.transform, supported) ? stable + 0.02f : 0;
                        if (stable >= 3)
                        {
                            result.stableSeconds = stable; result.supportedNails = supported.Count;
                            return result;
                        }
                    }
                }
            }
            return null;
        }
    }
}
