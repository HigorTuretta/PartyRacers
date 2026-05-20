using UnityEngine;

[DisallowMultipleComponent]
public class ElectroGelBillboard : MonoBehaviour
{
    [SerializeField] private bool lockY;
    [SerializeField] private float spinSpeedZ;

    private Camera referenceCamera;

    private void LateUpdate()
    {
        if (referenceCamera == null || !referenceCamera.isActiveAndEnabled)
            referenceCamera = Camera.main;

        if (referenceCamera == null)
            return;

        Vector3 forward = referenceCamera.transform.forward;

        if (lockY)
            forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        if (Mathf.Abs(spinSpeedZ) > 0.001f)
            transform.Rotate(0f, 0f, spinSpeedZ * Time.deltaTime, Space.Self);
    }

    public void SetSpinSpeed(float speed)
    {
        spinSpeedZ = speed;
    }
}
