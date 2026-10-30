using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public sealed class StaminaBarFader : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)] private float fadeOutStaminaPercent = 90f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

    private CanvasGroup canvasGroup;
    private PlayerStatsController playerStats;
    private bool shouldShowUI;
    private bool hasStarted;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        UpdateInteractionState();
    }

    private void OnEnable()
    {
        if (hasStarted)
            SubscribeToStamina();
    }

    private void Start()
    {
        hasStarted = true;
        SubscribeToStamina();

        // Set the initial state immediately; subsequent stamina changes are faded in Update.
        canvasGroup.alpha = shouldShowUI ? 1f : 0f;
        UpdateInteractionState();
    }

    private void OnDisable()
    {
        if (playerStats != null)
            playerStats.StaminaChanged -= HandleStaminaChanged;
    }

    private void Update()
    {
        float targetAlpha = shouldShowUI ? 1f : 0f;

        if (fadeDuration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
        }
        else
        {
            canvasGroup.alpha = Mathf.MoveTowards(
                canvasGroup.alpha,
                targetAlpha,
                Time.unscaledDeltaTime / fadeDuration);
        }
    }

    private void SubscribeToStamina()
    {
        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStatsController>();

        if (playerStats == null)
        {
            shouldShowUI = false;
            Debug.LogError("StaminaBarFader could not find a PlayerStatsController.", this);
            UpdateInteractionState();
            return;
        }

        playerStats.StaminaChanged -= HandleStaminaChanged;
        playerStats.StaminaChanged += HandleStaminaChanged;
        HandleStaminaChanged(playerStats.CurrentStamina, playerStats.MaxStamina);
    }

    private void HandleStaminaChanged(float currentStamina, float maxStamina)
    {
        float staminaPercent = maxStamina > 0f ? currentStamina / maxStamina * 100f : 0f;
        shouldShowUI = maxStamina > 0f && staminaPercent < fadeOutStaminaPercent;
        UpdateInteractionState();
    }

    private void UpdateInteractionState()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.interactable = shouldShowUI;
        canvasGroup.blocksRaycasts = shouldShowUI;
    }
}
