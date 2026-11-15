using UnityEngine;

namespace NailBalance
{
    [CreateAssetMenu(menuName = "Nail Balance/Puzzle")]
    public class PuzzleDefinition : ScriptableObject
    {
        [System.Serializable]
        public struct NailSettings
        {
            [Range(0.65f, 1.3f)] public float length;
            [Range(0.5f, 2f)] public float mass;
            [Range(-180f, 180f)] public float initialAngle;
        }

        public string title;
        public string hint;
        public GameObject supportPrefab;
        public float pedestalTop = 0;
        [Min(0.5f)] public float supportWidth = 3f;
        [Min(10f)] public float timeLimit = 45f;
        public NailSettings[] nails;
        public Color background = new Color(0.93f, 0.96f, 0.95f);
        public Color supportColor = new Color(0.18f, 0.58f, 0.56f);
    }
}
