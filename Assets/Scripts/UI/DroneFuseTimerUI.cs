using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class DroneFuseTimerUI : MonoBehaviour
{
    [SerializeField] private RollingDroneController drone;

    private TextMeshProUGUI timerText;

    private void Awake()
    {
        timerText = GetComponent<TextMeshProUGUI>();
        if (drone == null)
            drone = FindFirstObjectByType<RollingDroneController>();

        if (drone == null)
            Debug.LogError("DroneFuseTimerUI could not find a RollingDroneController.", this);
    }

    private void Update()
    {
        if (drone == null || timerText == null)
            return;

        int totalMilliseconds = Mathf.Max(0, Mathf.FloorToInt(drone.FuseTimeRemaining * 1000f));
        int seconds = totalMilliseconds / 1000;
        int milliseconds = totalMilliseconds % 1000;
        timerText.text = string.Format("{0:00}:{1:000}", seconds, milliseconds);
    }
}
