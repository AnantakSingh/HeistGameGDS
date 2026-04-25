using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        
        // If the camera doesn't have the "MainCamera" tag, find any camera in the scene
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }
        
        if (mainCamera == null)
        {
            Debug.LogError("FaceCamera script could not find a Camera in the scene!");
        }
    }

    void LateUpdate()
    {
        if (mainCamera != null)
        {
            // By making the object look along the camera's forward vector instead of directly AT the camera, 
            // we ensure the 3D text doesn't appear mirrored or backwards.
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                             mainCamera.transform.rotation * Vector3.up);
        }
    }
}
