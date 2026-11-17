using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Points the BCI on this machine at the right paddle:
/// host / offline -> left (blue) side, LAN client -> right (red) side.
///
/// * leftSideObjects / rightSideObjects: the stimuli (and anything else BCI-specific) for
///   each side. Only the local side's set is active, so each PC flashes just its own targets.
/// * OnLocalPaddleChanged: hook your TaskController / classifier here if it keeps a reference
///   to the paddle it moves (e.g. a public "SetPaddle(Paddle)" method).
/// </summary>
public class BciSideSwitcher : MonoBehaviour
{
    [Serializable] public class PaddleEvent : UnityEvent<Paddle> { }

    public BrainHockeyNet net;
    [Tooltip("Switches CortexSystem to this side's stim controller.")]
    public CortexSystem cortex;
    [Tooltip("Stimuli etc. for the LEFT / BLUE player (host, or offline).")]
    public GameObject[] leftSideObjects;
    [Tooltip("Stimuli etc. for the RIGHT / RED player (client).")]
    public GameObject[] rightSideObjects;

    public PaddleEvent OnLocalPaddleChanged;

    public int LocalSide { get; private set; }

    void Start()
    {
        if (net == null) net = BrainHockeyNet.Instance;
        if (net != null) net.LocalSideChanged += Apply;
        Apply(net != null ? net.LocalOwner : 1);
    }

    void OnDestroy()
    {
        if (net != null) net.LocalSideChanged -= Apply;
    }

    private void Apply(int side)
    {
        if (side == LocalSide) return;
        LocalSide = side;
        SetActive(leftSideObjects, side == 1);
        SetActive(rightSideObjects, side == 2);
        if (net != null) OnLocalPaddleChanged.Invoke(side == 2 ? net.rightPaddle : net.leftPaddle);
        if (cortex != null) cortex.SetLocalSide(side);
    }

    private static void SetActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (var o in objects) if (o != null) o.SetActive(active);
    }
    
}
