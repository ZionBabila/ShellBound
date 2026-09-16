using UnityEngine;

public class ReturnSmokeToPool : MonoBehaviour
{
    public void ReturnToPool()
    {
        ObjectPoolManager.ReturnObjectToPool(gameObject);
    }
}
