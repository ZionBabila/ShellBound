using System.Collections;
using UnityEngine;

// =============================================================================
// PipeTeleporter — Mario-style two-pipe warp pair (suck → hide → spit).
// -----------------------------------------------------------------------------
// Role:        OnTriggerEnter2D for objects tagged "Player" runs WarpRoutine:
//              lerps the player into this pipe's center (optionally shrinking),
//              hides briefly, then teleports and spits the player out at
//              destinationPipe.exitPoint.
// Depends on:  destinationPipe paired in the Inspector; player has Rigidbody2D
//              and a SpriteRenderer (in self or a child).
// Notes:       - Uses rb.isKinematic which is deprecated in newer Unity; consider
//                migrating to rb.bodyType = RigidbodyType2D.Kinematic.
//              - Player input is NOT locked during warp (TODO comments in WarpRoutine
//                mark the spots where input disable/enable should hook in).
// =============================================================================
public class PipeTeleporter : MonoBehaviour
{
    [Header("Destination Settings")]
    [Tooltip("Drag the other PipeTeleporter object here.")]
    public PipeTeleporter destinationPipe;

    [Tooltip("An empty GameObject placed just outside the destination pipe where the player pops out.")]
    public Transform exitPoint;

    [Header("Warp Timers")]
    public float suckDuration = 0.5f;
    public float undergroundDuration = 1.0f;
    public float spitDuration = 0.5f;

    [Header("Visual Effects")]
    [Tooltip("Should the player shrink when entering the pipe?")]
    public bool shrinkPlayer = true;

    // Cooldown to prevent an infinite teleportation loop between the two pipes
    [HideInInspector]
    public bool canWarp = true;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!canWarp) return;

        GameObject playerObj = null;

        // Check if it's the player and the pipe is ready to warp
        if (collision.CompareTag("Player"))
        {
            playerObj = collision.gameObject;
        }
        else
        {
            // Backward compatibility: Old TunaCan separated from the player hierarchy
            TunaCan oldCan = collision.GetComponent<TunaCan>();
            if (oldCan != null && oldCan.CurrentState == ShellState.InUse)
            {
                oldCan.OnDeactivate();
                if (oldCan.transform.parent != null)
                {
                    PlayerController oldPlayer = oldCan.GetComponentInParent<PlayerController>();
                    if (oldPlayer != null) playerObj = oldPlayer.gameObject;
                }
            }
        }

        if (playerObj != null)
        {
            StartCoroutine(WarpRoutine(playerObj));
        }
    }

    private IEnumerator WarpRoutine(GameObject player)
    {
        // 1. Lock the pipes to prevent looping
        canWarp = false;
        if (destinationPipe != null) destinationPipe.canWarp = false;

        // --- BUG FIX 1.1: Force exit active shells so player doesn't warp as a ball ---
        PlayerShellSystem newShellSystem = player.GetComponent<PlayerShellSystem>();
        if (newShellSystem != null && newShellSystem.CurrentShell != null && newShellSystem.CurrentShell.CurrentState == ShellState.InUse)
        {
            newShellSystem.CurrentShell.DeactivateAbility();
        }

        PlayerController oldPlayerController = player.GetComponent<PlayerController>();
        if (oldPlayerController != null && oldPlayerController.currentShell != null && oldPlayerController.currentShell.CurrentState == ShellState.InUse)
        {
            oldPlayerController.currentShell.OnDeactivate();
        }

        // 2. Get player components and store original scale
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        
        // Find the visual root to hide it completely during the pipe travel (supports 2D Bone Rigs)
        Transform visualsRoot = null;
        if (newShellSystem != null && newShellSystem.VisualsRoot != null) visualsRoot = newShellSystem.VisualsRoot;
        else if (oldPlayerController != null && oldPlayerController.visualsRoot != null) visualsRoot = oldPlayerController.visualsRoot;

        Vector3 originalScale = player.transform.localScale;

        // --- BUG FIX 1.2: Disable controls AND release grabs ---
        SimplePlayer simplePlayer = player.GetComponent<SimplePlayer>();
        if (simplePlayer != null) 
        {
            simplePlayer.isMovementDisabled = true; // This automatically drops grabbed objects!
        }
        else if (oldPlayerController != null)
        {
            oldPlayerController.SetControlEnabled(false);
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero; // Stop player movement
            rb.bodyType = RigidbodyType2D.Kinematic;            // Disable gravity physics while in pipe
        }

        // 3. Suction animation (Move to center of current pipe)
        Vector3 startPos = player.transform.position;
        Vector3 pipeCenter = transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < suckDuration)
        {
            player.transform.position = Vector3.Lerp(startPos, pipeCenter, elapsedTime / suckDuration);

            if (shrinkPlayer)
            {
                // Shrink from original scale to zero
                player.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, elapsedTime / suckDuration);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 4. Inside the pipe (Hidden)
        if (visualsRoot != null) visualsRoot.gameObject.SetActive(false);
        else
        {
            SpriteRenderer sprite = player.GetComponentInChildren<SpriteRenderer>();
            if (sprite != null) sprite.enabled = false;
        }

        if (destinationPipe != null)
        {
            // Teleport to destination pipe instantly
            player.transform.position = destinationPipe.transform.position;
        }

        yield return new WaitForSeconds(undergroundDuration);

        // Safety check: Make sure the player wasn't destroyed while underground
        if (player == null)
        {
            canWarp = true;
            if (destinationPipe != null) destinationPipe.canWarp = true;
            yield break;
        }

        // 5. Spit out animation (Move to exit point)
        if (visualsRoot != null) visualsRoot.gameObject.SetActive(true);
        else
        {
            SpriteRenderer sprite = player.GetComponentInChildren<SpriteRenderer>();
            if (sprite != null) sprite.enabled = true;
        }
        elapsedTime = 0f;

        Vector3 spitStartPos = player.transform.position;
        
        // Default to current pipe if destination is missing, otherwise use destination
        Vector3 spitEndPos = transform.position + Vector3.up; 
        if (destinationPipe != null) {
            spitEndPos = destinationPipe.exitPoint != null ? destinationPipe.exitPoint.position : destinationPipe.transform.position + Vector3.up;
        }

        while (elapsedTime < spitDuration)
        {
            player.transform.position = Vector3.Lerp(spitStartPos, spitEndPos, elapsedTime / spitDuration);

            if (shrinkPlayer)
            {
                // Grow from zero back to original scale
                player.transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, elapsedTime / spitDuration);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 6. Restore player state
        if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic; // Re-enable physics

        // Ensure scale is exactly as it was (preserves left/right flipping)
        player.transform.localScale = originalScale;

        if (simplePlayer != null) simplePlayer.isMovementDisabled = false;
        else if (oldPlayerController != null) oldPlayerController.SetControlEnabled(true);

        // 7. Unlock pipes
        yield return new WaitForSeconds(0.2f);
        canWarp = true;
        if (destinationPipe != null) destinationPipe.canWarp = true;
    }
}