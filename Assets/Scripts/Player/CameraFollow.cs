using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    private Transform target;

    public Vector3 offset = new Vector3(0, 6, -6);

    public void SetTarget(Transform t)
    {
        target = t;
    }

    void LateUpdate()
    {
        if (target == null) return;

        transform.position = target.position + offset;
        transform.LookAt(target);
    }
}