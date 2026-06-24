using UnityEngine;

[System.Serializable]
public class UniqueID : MonoBehaviour {
    [Tooltip("A unique identifier for this object, used to link it to other systems like the GameManager.")]
    public string id;
}
public class AcidBarrel : MonoBehaviour
{
    [Header("Drop Settings")]
    [Tooltip("The acid drop prefab to spawn.")]
    public GameObject acidDropPrefab;
    
    [Tooltip("How often (in seconds) a drop is spawned.")]
    public float dropRate = 2.0f;
    
    [Tooltip("Specific point where the drop spawns. If empty, uses this object's position + dropOffset.")]
    public Transform dropPoint;
    
    [Tooltip("Offset used for the drop position if no dropPoint Transform is assigned.")]
    public Vector2 dropOffset = new Vector2(0, -0.5f);

    [Header("Identification")]
    [Tooltip("A unique ID for this barrel. The GameManager will use this ID to find the correct respawn point.")]
    public string barrelID;

    private float timer;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= dropRate)
        {
            SpawnDrop();
            timer = 0f;
        }
    }

    private void SpawnDrop()
    {
        if (acidDropPrefab == null) return;
        
        // Determine the spawn position: either the assigned transform or a calculated offset
        Vector3 spawnPos = dropPoint != null ? dropPoint.position : transform.position + (Vector3)dropOffset;
        
        GameObject dropObject = Instantiate(acidDropPrefab, spawnPos, Quaternion.identity);
        
        // Pass a reference of this barrel to the newly created drop
        AcidDrop acidDropScript = dropObject.GetComponent<AcidDrop>();
        if (acidDropScript != null)
        {
            acidDropScript.sourceBarrelID = barrelID;
        }
    }

    private void OnDrawGizmos()
    {
        // Draw a green visual representation of the drop point in the Editor
        Gizmos.color = Color.green;
        
        Vector3 spawnPos = dropPoint != null ? dropPoint.position : transform.position + (Vector3)dropOffset;
        
        // Draw a small sphere where the drop will appear
        Gizmos.DrawWireSphere(spawnPos, 0.15f);
        
        // Draw a line from the barrel center to the drop point
        Gizmos.DrawLine(transform.position, spawnPos);
    }
}