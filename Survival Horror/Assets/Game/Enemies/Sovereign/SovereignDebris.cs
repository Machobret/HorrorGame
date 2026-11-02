using UnityEngine;

namespace HorrorGame.Enemies
{
    /// <summary>Short, non-colliding stone fragments for Sovereign's prototype entrance.</summary>
    public sealed class SovereignDebris : MonoBehaviour
    {
        private readonly Transform[] pieces = new Transform[18];
        private readonly Vector3[] velocities = new Vector3[18];
        private Material material;
        private float age;

        public static void Burst(Vector3 point, Vector3 normal)
        {
            var root = new GameObject("Sovereign debris break");
            root.transform.position = point;
            var effect = root.AddComponent<SovereignDebris>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader)
            {
                effect.material = new Material(shader);
                effect.material.color = new Color(.32f, .28f, .24f);
            }
            for (int i = 0; i < effect.pieces.Length; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var collider = shard.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                shard.transform.SetParent(root.transform, false);
                shard.transform.localPosition = Random.insideUnitSphere * .12f;
                shard.transform.localRotation = Random.rotation;
                shard.transform.localScale = new Vector3(Random.Range(.05f, .16f), Random.Range(.04f, .10f), Random.Range(.05f, .18f));
                if (effect.material) shard.GetComponent<Renderer>().sharedMaterial = effect.material;
                effect.pieces[i] = shard.transform;
                effect.velocities[i] = normal * Random.Range(1f, 2.5f) + Vector3.up * 2f + Random.insideUnitSphere * 2f;
            }
        }

        private void Update()
        {
            if (HorrorEngine.PauseController.Exists && HorrorEngine.PauseController.Instance.IsPaused) return;
            float dt = Time.deltaTime;
            age += dt;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (!pieces[i]) continue;
                velocities[i] += Vector3.down * (7f * dt);
                pieces[i].position += velocities[i] * dt;
                pieces[i].Rotate(new Vector3(140f, 90f, 180f) * dt);
                if (age > .6f) pieces[i].localScale *= Mathf.Exp(-5f * dt);
            }
            if (age >= 1.2f) Destroy(gameObject);
        }

        private void OnDestroy() { if (material) Destroy(material); }
    }
}
