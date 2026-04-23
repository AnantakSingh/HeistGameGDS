using UnityEngine;

public class MirrorReflection : MonoBehaviour
{
    public Camera playerCamera;
    public Camera mirrorCamera;

    void LateUpdate()
    {
        if (playerCamera == null || mirrorCamera == null) return;

  
        Vector3 localPos = transform.InverseTransformPoint(playerCamera.transform.position);


        localPos.z *= -1f;


        mirrorCamera.transform.position = transform.TransformPoint(localPos);

        Vector3 localForward = transform.InverseTransformDirection(playerCamera.transform.forward);
        Vector3 localUp = transform.InverseTransformDirection(playerCamera.transform.up);

        localForward.z *= -1f;
        localUp.z *= -1f;

        Vector3 worldForward = transform.TransformDirection(localForward);
        Vector3 worldUp = transform.TransformDirection(localUp);

        mirrorCamera.transform.rotation = Quaternion.LookRotation(worldForward, worldUp);
    }
}