using UnityEngine;

public class PrefabReferenceLibrary : MonoBehaviour
{
    [SerializeField] private RectTransform materialCardPrefab;
    [SerializeField] private RectTransform popupDragonWindowBlankPrefab;

    public RectTransform MaterialCardPrefab => materialCardPrefab;
    public RectTransform PopupDragonWindowBlankPrefab => popupDragonWindowBlankPrefab;
}
