using System.Collections;
using UnityEngine;

/// <summary>
/// Automatically disables this GameObject after <see cref="delay"/> seconds
/// whenever it is enabled. Works every time the object is re-activated.
///
/// Usage: Add to any UI element or GameObject. Set the delay in the Inspector.
/// </summary>
public class AutoDisable : MonoBehaviour
{
    [Tooltip("How many seconds to wait before disabling this GameObject.")]
    public float delay = 2f;

    private Coroutine _pending;

    void OnEnable()
    {
        // If somehow re-enabled while a previous countdown is running, restart it.
        if (_pending != null)
            StopCoroutine(_pending);

        _pending = StartCoroutine(DisableAfterDelay());
    }

    IEnumerator DisableAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(false);
        _pending = null;
    }
}
