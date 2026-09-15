using UnityEngine;

[RequireComponent(typeof(Light))]
public class LightWallDimming : MonoBehaviour
{
    public float maxIntensity = 2f;
    public float minIntensity = 0.5f;
    public float checkDistance = 2f;
    public float smoothSpeed = 5f;

    public int rayCount = 16; // more = smoother detection
    public LayerMask wallMask;

    private Light myLight;

    void Start()
    {
        myLight = GetComponent<Light>();
    }

    void Update()
    {
        float closestHit = checkDistance;

        Vector3 origin = transform.position;

        // Cast rays in a circle (Y axis)
        for (int i = 0; i < rayCount; i++)
        {
            float angle = (i / (float)rayCount) * 360f;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;

            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, checkDistance, wallMask))
            {
                if (hit.distance < closestHit)
                    closestHit = hit.distance;

                Debug.DrawRay(origin, direction * hit.distance, Color.red);
            }
            else
            {
                Debug.DrawRay(origin, direction * checkDistance, Color.green);
            }
        }

        // Convert distance → intensity
        float t = closestHit / (checkDistance * 1.75f);
        float targetIntensity = Mathf.Lerp(minIntensity, maxIntensity, t);

        // Smooth transition
        myLight.intensity = Mathf.Lerp(myLight.intensity, targetIntensity, Time.deltaTime * smoothSpeed);
    }
}