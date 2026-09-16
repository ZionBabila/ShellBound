using UnityEngine;

public class VisibilityProxy : MonoBehaviour
{
    private JosStamSmokeEmitterJobs _mainEmitter;

    public void Initialize(JosStamSmokeEmitterJobs emitter)
    {
        _mainEmitter = emitter;
    }

    private void OnBecameVisible()
    {
        if (_mainEmitter != null)
        {
            _mainEmitter.ReportVisibilityChange(true);
        }
    }

    private void OnBecameInvisible()
    {
        if (_mainEmitter != null)
        {
            _mainEmitter.ReportVisibilityChange(false);
        }
    }
}