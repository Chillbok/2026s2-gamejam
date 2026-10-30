using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class ResourceBarUI : MonoBehaviour
{
    [SerializeField] private RectTransform progressTransform;
    [SerializeField, Range(0f, 1f)] private float progress = 1f;
    [SerializeField, Min(0f)] private float fullWidth;

    private void Reset()
    {
        FindProgressTransform();
        InitializeFullWidthIfNeeded();
        ApplyProgress();
    }

    private void OnEnable()
    {
        FindProgressTransform();
        InitializeFullWidthIfNeeded();
        ApplyProgress();
    }

    private void OnValidate()
    {
        progress = Mathf.Clamp01(progress);
        FindProgressTransform();
        InitializeFullWidthIfNeeded();
        ApplyProgress();
    }

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        ApplyProgress();
    }

    private void FindProgressTransform()
    {
        if (progressTransform != null)
            return;

        RectTransform[] childTransforms = GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform childTransform in childTransforms)
        {
            if (childTransform != transform)
            {
                progressTransform = childTransform;
                return;
            }
        }
    }

    private void InitializeFullWidthIfNeeded()
    {
        if (progressTransform != null && fullWidth <= 0f)
            fullWidth = progressTransform.sizeDelta.x;
    }

    private void ApplyProgress()
    {
        FindProgressTransform();
        if (progressTransform == null)
            return;

        InitializeFullWidthIfNeeded();

        Vector2 size = progressTransform.sizeDelta;
        size.x = fullWidth * progress;
        progressTransform.sizeDelta = size;
    }
}
