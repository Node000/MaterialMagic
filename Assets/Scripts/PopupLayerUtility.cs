using UnityEngine;

public static class PopupLayerUtility
{
    public const int SortingOrder = 9000;

    public static void ApplyTo(RectTransform rect)
    {
        if (rect == null)
            return;

        Canvas canvas = rect.GetComponent<Canvas>();
        if (canvas == null)
            canvas = rect.gameObject.AddComponent<Canvas>();

        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;
    }
}
