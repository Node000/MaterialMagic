using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnlockPopupQueueUI : MonoBehaviour
{
    [SerializeField] private RectTransform itemRoot;
    [SerializeField] private UnlockPopupItemUI itemPrefab;
    [SerializeField] private float initialDelay = 0.4f;
    [SerializeField] private float gapSeconds = 0.18f;

    private readonly Queue<UnlockPendingMessageData> queue = new Queue<UnlockPendingMessageData>();
    private Coroutine showRoutine;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ShowPendingUnlocks();
    }

    public void ShowPendingUnlocks()
    {
        ResolveReferences();
        if (itemRoot == null || itemPrefab == null)
            return;

        UnlockPendingMessageData[] messages = UnlockSystem.ConsumePendingUnlockMessages();
        for (int i = 0; messages != null && i < messages.Length; i++)
        {
            if (messages[i] != null)
                queue.Enqueue(messages[i]);
        }

        if (showRoutine == null && queue.Count > 0)
            showRoutine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        if (initialDelay > 0f)
            yield return new WaitForSecondsRealtime(initialDelay);

        while (queue.Count > 0)
        {
            UnlockPendingMessageData message = queue.Dequeue();
            UnlockPopupItemUI item = Instantiate(itemPrefab, itemRoot);
            item.gameObject.SetActive(true);
            item.Bind(message);
            item.PlayEnter();
            yield return new WaitForSecondsRealtime(item.DisplaySeconds);

            bool exitCompleted = false;
            item.PlayExit(() => exitCompleted = true);
            while (!exitCompleted)
                yield return null;

            Destroy(item.gameObject);
            if (gapSeconds > 0f)
                yield return new WaitForSecondsRealtime(gapSeconds);
        }

        showRoutine = null;
    }

    private void ResolveReferences()
    {
        if (itemRoot == null)
            itemRoot = transform as RectTransform;
    }
}
