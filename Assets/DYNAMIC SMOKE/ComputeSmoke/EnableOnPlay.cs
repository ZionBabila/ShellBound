using System.Reflection;
using UnityEngine;

public class EnableOnPlay : MonoBehaviour
{
    private enum StartMode 
    { 
        Start,
        Awake, 
        OnEnable
    }

    [SerializeField] private StartMode mode = StartMode.Awake;
    [SerializeField] private Component _componentToActivate;

    private void SetEnabled(bool value)
    {
        if (_componentToActivate == null) return;

        PropertyInfo prop = _componentToActivate
            .GetType()
            .GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);

        if (prop != null && prop.CanWrite)
            prop.SetValue(_componentToActivate, value);
        else
            Debug.LogWarning($"{_componentToActivate.GetType().Name} has no settable 'enabled' property.");
    }

    private void Awake() 
    { 
        if (mode == StartMode.Awake) SetEnabled(true);
    }
    private void Start() 
    {
        if (mode == StartMode.Start) SetEnabled(true);
    }
    private void OnEnable() 
    {
        if (mode == StartMode.OnEnable) SetEnabled(true);
    }
}