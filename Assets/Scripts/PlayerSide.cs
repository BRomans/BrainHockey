using UnityEngine;

/// <summary>
/// Marks a BCI trigger zone (TrialCounter, EndTask_1..3) as belonging to one player.
/// Zones WITHOUT this component count as the left/blue player (your existing scene).
/// For the right/red player, duplicate the zones mirrored onto the right side and add
/// this component with side = 2.
/// </summary>
public class PlayerSide : MonoBehaviour
{
    [Tooltip("1 = left / blue (host), 2 = right / red (client)")]
    public int side = 2;
}
