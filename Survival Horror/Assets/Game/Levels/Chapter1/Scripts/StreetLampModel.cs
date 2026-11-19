using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Builds the visible street-light housing around the existing circuit-driven lamp.</summary>
    [ExecuteAlways]
    public sealed class StreetLampModel : MonoBehaviour
    {
        private const string ModelName = "Streetlight Geometry";

        private void OnEnable() => Build();
        private void OnValidate() => Build();

        private void Build()
        {
            Transform model = transform.Find(ModelName);
            if (!model)
            {
                model = new GameObject(ModelName).transform;
                model.SetParent(transform, false);
            }

            Material metal = GetComponent<Renderer>() ? GetComponent<Renderer>().sharedMaterial : null;
            MakePart(model, "Footing", PrimitiveType.Cylinder, new Vector3(0f, .10f, 0f), new Vector3(.34f, .10f, .34f), Quaternion.identity, metal);
            MakePart(model, "Pole", PrimitiveType.Cylinder, new Vector3(0f, 2.7f, 0f), new Vector3(.13f, 2.7f, .13f), Quaternion.identity, metal);
            MakePart(model, "Top Collar", PrimitiveType.Cylinder, new Vector3(0f, 5.25f, 0f), new Vector3(.19f, .12f, .19f), Quaternion.identity, metal);
            // The cylinders are two units long by default. These centres,
            // rotations and scales make each endpoint overlap its neighbour.
            MakePart(model, "Angled Arm", PrimitiveType.Cylinder, new Vector3(.41f, 5.57f, 0f), new Vector3(.085f, .50f, .085f), Quaternion.Euler(0f, 0f, -55f), metal);
            MakePart(model, "Arm End", PrimitiveType.Cylinder, new Vector3(1.16f, 5.86f, 0f), new Vector3(.085f, .35f, .085f), Quaternion.Euler(0f, 0f, -90f), metal);
            MakePart(model, "Fixture Housing", PrimitiveType.Cylinder, new Vector3(1.52f, 5.68f, 0f), new Vector3(.22f, .32f, .22f), Quaternion.Euler(0f, 0f, -90f), metal);

            Transform bulb = transform.Find("Bulb");
            if (bulb)
            {
                bulb.localPosition = new Vector3(1.52f, 5.47f, 0f);
                bulb.localScale = new Vector3(.38f, .10f, .28f);
                Light light = bulb.GetComponent<Light>();
                if (light)
                {
                    light.type = LightType.Spot;
                    light.color = new Color(1f, .58f, .23f);
                    light.range = 8.5f;
                    light.intensity = 15f;
                    light.spotAngle = 54f;
                    light.innerSpotAngle = 36f;
                    light.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
            }
        }

        private static void MakePart(Transform parent, string partName, PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            Transform part = parent.Find(partName);
            if (!part)
            {
                GameObject partObject = GameObject.CreatePrimitive(type);
                partObject.name = partName;
                part = partObject.transform;
                part.SetParent(parent, false);
                Collider collider = part.GetComponent<Collider>();
                if (collider)
                {
                    if (Application.isPlaying) Destroy(collider);
                    else DestroyImmediate(collider);
                }
            }
            part.SetLocalPositionAndRotation(position, rotation);
            part.localScale = scale;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer && material) renderer.sharedMaterial = material;
        }
    }
}
