using UnityEngine;

namespace NailBalance
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect lastArea;
        Vector2Int lastSize;
        void Update()
        {
            Rect area = Screen.safeArea;
            Vector2Int size = new Vector2Int(Screen.width, Screen.height);
            if (area == lastArea && size == lastSize) return;
            lastArea = area;
            lastSize = size;
            RectTransform rect = (RectTransform)transform;
            rect.anchorMin = area.min / new Vector2(size.x, size.y);
            rect.anchorMax = area.max / new Vector2(size.x, size.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
