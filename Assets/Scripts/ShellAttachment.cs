using UnityEngine;

// =============================================================================
// ShellAttachment — serializable per-shell config (one instance per BaseShell).
// -----------------------------------------------------------------------------
// Role:        Describes how a shell connects to and influences the player —
//              physical mount point, COM shift while carried, visual flip
//              behavior, and seat offset for "ride inside" shells.
// Used by:     BaseShell.attachment field (every shell holds one).
//              PlayerController reads carryComShift in ApplyCenterOfMass each tick.
//              BaseShell.Equip uses attachPoint for mount alignment.
// =============================================================================
[System.Serializable]
public class ShellAttachment
{
    [Header("Physical Mount")]
    [Tooltip("Point on the shell that aligns to the player's shellMountPoint when equipped.")]
    public Transform attachPoint;

    [Header("Effect on Player (Carried)")]
    [Tooltip("Offset added to the player's center of mass while this shell is carried.")]
    public Vector2 carryComShift = new(0f, 0.2f);

    [Header("Visual Behavior")]
    [Tooltip("If true, the shell mirrors with the player's flip. If false, it keeps its original orientation.")]
    public bool mirrorWithPlayerFlip = true;

    [Header("Encapsulating Mode (player rides inside)")]
    [Tooltip("Where the player sits relative to the shell center when inside.")]
    public Vector2 playerSeatOffset = Vector2.zero;
}
