using UnityEngine;

public class PrefabReferenceLibrary : MonoBehaviour
{
    [SerializeField] private RectTransform materialCardPrefab;

    public RectTransform MaterialCardPrefab => materialCardPrefab;
}
