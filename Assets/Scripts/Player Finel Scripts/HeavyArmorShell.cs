using UnityEngine;

public class HeavyArmorShell : BaseShell
{
    [Header("Armor Settings")]
    [Tooltip("Sprite used when the crab is hiding inside.")]
    public Sprite activeSprite;
    
    private Sprite defaultSprite;
    private SpriteRenderer spriteRenderer;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) 
        {
            defaultSprite = spriteRenderer.sprite;
        }
    }

    public override void ActivateAbility()
    {
        if (CurrentState != ShellState.OnBack) return;
        
        CurrentState = ShellState.InUse;
        if (spriteRenderer != null && activeSprite != null) spriteRenderer.sprite = activeSprite;
            
        Debug.Log("[HeavyArmorShell] Activated! Crab is hiding.");
    }

    public override void DeactivateAbility()
    {
        if (CurrentState != ShellState.InUse) return;
        
        CurrentState = ShellState.OnBack;
        if (spriteRenderer != null && defaultSprite != null) spriteRenderer.sprite = defaultSprite;
            
        Debug.Log("[HeavyArmorShell] Deactivated! Crab is carrying the shell.");
    }
}