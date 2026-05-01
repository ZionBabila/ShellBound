using System.Collections;
using UnityEngine;

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
        // Check if it's the player and the pipe is ready to warp
        if (canWarp && collision.CompareTag("Player"))
        {
            StartCoroutine(WarpRoutine(collision.gameObject));
        }
    }

    private IEnumerator WarpRoutine(GameObject player)
    {
        // 1. Lock the pipes to prevent looping
        canWarp = false;
        if (destinationPipe != null) destinationPipe.canWarp = false;

        // 2. Get player components and store original scale
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        SpriteRenderer sprite = player.GetComponentInChildren<SpriteRenderer>();

        Vector3 originalScale = player.transform.localScale;

        // --- TODO: Disable Player Input Here ---
        // Example: player.GetComponent<PlayerMovement>().enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero; // Stop player movement
            rb.isKinematic = true;            // Disable gravity physics while in pipe
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
        if (sprite != null) sprite.enabled = false;

        if (destinationPipe != null)
        {
            // Teleport to destination pipe instantly
            player.transform.position = destinationPipe.transform.position;
        }

        yield return new WaitForSeconds(undergroundDuration);

        // 5. Spit out animation (Move to exit point)
        if (sprite != null) sprite.enabled = true;
        elapsedTime = 0f;

        Vector3 spitStartPos = player.transform.position;
        // Default to moving slightly up if no exit point is assigned
        Vector3 spitEndPos = destinationPipe.exitPoint != null ? destinationPipe.exitPoint.position : destinationPipe.transform.position + Vector3.up;

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
        if (rb != null) rb.isKinematic = false;

        // Ensure scale is exactly as it was (preserves left/right flipping)
        player.transform.localScale = originalScale;

        // --- TODO: Enable Player Input Here ---
        // Example: player.GetComponent<PlayerMovement>().enabled = true;

        // 7. Unlock pipes
        yield return new WaitForSeconds(0.2f);
        canWarp = true;
        if (destinationPipe != null) destinationPipe.canWarp = true;
    }
}