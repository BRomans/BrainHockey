using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The 4th target that pops up in the middle of the field.
///
/// Put this on an (always active) empty GameObject. Make the visible parts children of
/// "visual": a sprite/icon, and YOUR 4th BCI stimulus, so the stimulus moves with the target.
/// Hook OnShown / OnHidden to your stimulus code to start/stop flashing the 4th class.
/// </summary>
public class PowerUpTarget : MonoBehaviour
{
    [Tooltip("Child object that is shown/hidden. Defaults to the first child.")]
    public GameObject visual;

    [Tooltip("Fired when the target appears — e.g. enable the 4th BCI stimulus.")]
    public UnityEvent OnShown;
    [Tooltip("Fired when the target is claimed or expires — e.g. disable the 4th BCI stimulus.")]
    public UnityEvent OnHidden;

    public enum HideMode { ScaleToZero, Deactivate }
    [Tooltip("ScaleToZero keeps the object (and its NeuroStim) active, so the stim controller finds it by tag " +
             "and always has the same number of classes. Deactivate turns it off completely.")]
    public HideMode hideMode = HideMode.ScaleToZero;

    [Header("Idle animation")]
    public float pulseSpeed = 3f;
    public float pulseAmount = 0.08f;

    public bool IsActive { get; private set; }
    public int CurrentId { get; private set; }

    private Vector3 baseScale = Vector3.one;

    void Awake()
    {
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0).gameObject;
        if (visual != null) baseScale = visual.transform.localScale;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (visual == null) return;
        if (hideMode == HideMode.Deactivate)
        {
            visual.transform.localScale = baseScale;
            visual.SetActive(visible);
        }
        else
        {
            visual.SetActive(true);
            visual.transform.localScale = visible ? baseScale : Vector3.zero;
        }
    }

    public void Show(int id, Vector2 position)
    {
        CurrentId = id;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        if (IsActive) return;

        IsActive = true;
        SetVisible(true);
        OnShown.Invoke();
    }

    public void Hide()
    {
        if (!IsActive) return;
        IsActive = false;
        SetVisible(false);
        OnHidden.Invoke();
    }

    void Update()
    {
        if (!IsActive || visual == null || pulseAmount <= 0f) return;
        float s = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        visual.transform.localScale = baseScale * s;
    }
}
