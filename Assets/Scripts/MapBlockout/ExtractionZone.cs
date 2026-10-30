using TMPro;
using UnityEngine;

public sealed class ExtractionZone : MonoBehaviour
{
    [SerializeField, Min(1f)] private float holdDuration = 30f;

    private TMP_Text statusText;
    private Collider extractionBounds;
    private CharacterController playerController;
    private bool playerInside;
    private bool extractionComplete;
    private float holdTimer;

    private void Awake()
    {
        CacheReferences();
        UpdateTimerDisplay();
    }

    private void Update()
    {
        if (extractionComplete)
            return;

        CacheReferences();
        if (extractionBounds == null || playerController == null)
            return;

        bool isPlayerInside = IsPlayerInsideZone();
        if (!isPlayerInside)
        {
            if (playerInside)
                holdTimer = 0f;

            playerInside = false;
            UpdateTimerDisplay();
            return;
        }

        if (!playerInside)
            holdTimer = 0f;

        playerInside = true;
        holdTimer += Time.deltaTime;
        if (holdTimer >= holdDuration)
        {
            extractionComplete = true;
            // Debug.Log("Extraction complete.", this);
            return;
        }

        UpdateTimerDisplay();
    }

    private void CacheReferences()
    {
        if (statusText == null)
        {
            GameObject textObject = GameObject.Find("Canvas/TestText");
            if (textObject != null)
                statusText = textObject.GetComponent<TMP_Text>();
        }

        if (extractionBounds == null)
            extractionBounds = GetComponent<Collider>();
        if (playerController == null)
            playerController = FindFirstObjectByType<CharacterController>();
    }

    private bool IsPlayerInsideZone()
    {
        if (extractionBounds == null || playerController == null)
            return false;

        Bounds zone = extractionBounds.bounds;
        Bounds player = playerController.bounds;
        Vector3 playerPosition = playerController.transform.position;

        bool insideHorizontalArea = playerPosition.x >= zone.min.x && playerPosition.x <= zone.max.x
            && playerPosition.z >= zone.min.z && playerPosition.z <= zone.max.z;
        bool overlapsHeight = player.min.y <= zone.max.y && player.max.y >= zone.min.y;

        return insideHorizontalArea && overlapsHeight;
    }

    private void UpdateTimerDisplay()
    {
        int remainingSeconds = Mathf.CeilToInt(Mathf.Max(0f, holdDuration - holdTimer));
        int minutes = remainingSeconds / 60;
        int seconds = remainingSeconds % 60;
        SetStatus($"{minutes:00}:{seconds:00}");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
