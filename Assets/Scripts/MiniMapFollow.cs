using UnityEngine;

public class MiniMapFollow : MonoBehaviour
{
    public Transform target;  //player
    public float height = 20f; // high of the camera

    void LateUpdate()
    {
        if (target == null) return;

        transform.position = new Vector3(
            target.position.x,
            target.position.y + height,
            target.position.z
        );

        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}