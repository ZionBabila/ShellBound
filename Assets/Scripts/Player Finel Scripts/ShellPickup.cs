using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
public class ShellPickup : MonoBehaviour
{
    [Tooltip("The ID of the shell. Must match the 'shellID' of the BaseShell component on the Player's Rig.")]
    public string shellID;
}