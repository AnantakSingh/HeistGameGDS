using UnityEngine;

public class SmoothOscillator : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("The maximum angle to rotate in either direction from the start position.")]
    public float angleLimit = 30f;
    
    [Tooltip("How fast the object oscillates. Higher is faster.")]
    public float rotationSpeed = 1f;

    private float startYRotation;

    void Start()
    {
        // Store the original Y rotation of the object
        startYRotation = transform.eulerAngles.y;
    }

    void Update()
    {
        // Calculate the oscillation using a Sine wave
        // Mathf.Sin returns a value between -1 and 1
        float oscillation = Mathf.Sin(Time.time * rotationSpeed);
        
        // Map that -1 to 1 value to our -angleLimit to +angleLimit range
        float currentAngleOffset = oscillation * angleLimit;
        
        // Apply the new rotation to the object
        // We keep the original X and Z rotations intact
        transform.rotation = Quaternion.Euler(
            transform.eulerAngles.x, 
            startYRotation + currentAngleOffset, 
            transform.eulerAngles.z
        );
    }
}
