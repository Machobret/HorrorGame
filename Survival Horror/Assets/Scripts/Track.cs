using UnityEngine;

public class Track : MonoBehaviour
{
    public string playerTag = "Player";
    private Transform player;

    [SerializeField] public float distance = 4f;   // how far behind
    [SerializeField] public float height = 0.75f;     // how high
    [SerializeField] public float followSpeed = 10f;

    void LateUpdate()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag(playerTag);
            if (found != null)
                player = found.transform;

            return;
        }

        // Position: always behind player
        Vector3 targetPosition = player.position
                               - player.forward * distance
                               + Vector3.up * height;

        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);

        // Look at player
        transform.LookAt(player.position + Vector3.up * 1.5f);
    }
}