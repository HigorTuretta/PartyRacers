using UnityEngine;


namespace Hovl
{
    [ExecuteAlways]
    public class HS_ScreenEffect : MonoBehaviour
    {
        public ParticleSystem screenEffect;
        public Camera sourceCamera;

        // If the effect is placed in front of the camera, this is a fallback distance in case calculation fails
        // Also used as the snap distance on start if snapping is enabled
        public float fallbackDistance = 0.05f;

        // Extra coverage so the effect always bleeds past every screen edge.
        public float screenOverscan = 1.18f;

        // Keep the effect beyond the camera near plane. Values below near clip are invisible.
        public float nearPlanePadding = 0.08f;

        // Snap the effect to a fixed distance from the camera automatically on start (play mode only)
        public bool snapOnStart = true;

        // Parent the effect to the camera on start so it follows the camera
        public bool parentToCameraOnStart = true;

        void Reset()
        {
            if (sourceCamera == null)
                sourceCamera = Camera.main;
        }

        void OnEnable()
        {
            if (sourceCamera == null)
                sourceCamera = Camera.main;

            if (!Application.isPlaying)
                return;

            UpdateSize();
        }

        void Start()
        {
            // Only snap when entering Play mode
            if (!Application.isPlaying)
                return;

            if (!snapOnStart)
                return;

            Camera cam = sourceCamera != null ? sourceCamera : Camera.main;
            if (cam == null)
                return;

            float distance = GetSafeDistance(cam);

            // Place the effect directly in front of the camera at a visible distance
            transform.position = cam.transform.position + cam.transform.forward * distance;

            // Optionally keep the effect facing the camera by matching rotation (comment out if not desired)
            // transform.rotation = cam.transform.rotation;

            // Parent to camera so the effect follows it
            if (parentToCameraOnStart)
            {
                // Make the camera the parent and set a local offset forward at a visible distance
                transform.SetParent(cam.transform, true);
                transform.localPosition = Vector3.forward * distance;
                transform.localRotation = Quaternion.identity;
            }

            UpdateSize();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            UpdateSize();
        }

        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (!gameObject.scene.IsValid())
                return;

            if (sourceCamera == null)
                sourceCamera = Camera.main;

            // Keep explicit editor changes live without running every Inspector repaint.
            UpdateSize();
        }

        void UpdateSize()
        {
            if (screenEffect == null)
                return;

            Camera cam = sourceCamera != null ? sourceCamera : Camera.main;
            if (cam == null)
                return;

            float safeDistance = GetSafeDistance(cam);

            // distance from camera to this transform along camera forward (positive in front of camera)
            float dist = cam.transform.InverseTransformPoint(transform.position).z;
            if (dist < safeDistance)
            {
                dist = safeDistance;

                if (Application.isPlaying)
                {
                    if (parentToCameraOnStart)
                    {
                        if (transform.parent != cam.transform)
                            transform.SetParent(cam.transform, true);

                        transform.localPosition = Vector3.forward * safeDistance;
                        transform.localRotation = Quaternion.identity;
                    }
                    else
                    {
                        transform.position = cam.transform.position + cam.transform.forward * safeDistance;
                        transform.rotation = cam.transform.rotation;
                    }
                }
            }

            float height;
            if (cam.orthographic)
            {
                height = 2f * cam.orthographicSize;
            }
            else
            {
                float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
                height = 2f * dist * Mathf.Tan(fovRad * 0.5f);
            }

            float overscan = Mathf.Max(1f, screenOverscan);
            height *= overscan;
            float width = height * cam.aspect;

            // Set particle start size to match the world size (enable3D start size)
            var main = screenEffect.main;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(width);
            main.startSizeY = new ParticleSystem.MinMaxCurve(height);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(1f);

            // If the particle system uses a shape quad / box, update its scale too so emission area matches
            var shape = screenEffect.shape;
            shape.scale = new Vector3(width, height, 1f);
        }

        float GetSafeDistance(Camera cam)
        {
            if (cam == null)
                return Mathf.Max(0.01f, fallbackDistance);

            return Mathf.Max(0.01f, fallbackDistance, cam.nearClipPlane + Mathf.Max(0.01f, nearPlanePadding));
        }
    }
}
