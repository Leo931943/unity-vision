using System.Collections.Generic;
using UnityEngine;

/// <summary>Short, self-cleaning goal-side congratulation text and confetti burst.</summary>
public class GoalCelebrationEffect : MonoBehaviour
{
    private readonly List<Transform> confetti = new List<Transform>();
    private readonly List<Vector3> velocities = new List<Vector3>();
    private TextMesh label;
    private float lifetime;

    public static void Play(Vector3 goalPosition)
    {
        GameObject root = new GameObject("GoalCelebration");
        root.transform.position = goalPosition + Vector3.up * 5.1f + Vector3.right * 3.7f;
        root.AddComponent<GoalCelebrationEffect>();
    }

    private void Awake()
    {
        GameObject textObject = new GameObject("GoalText");
        textObject.transform.SetParent(transform, false);
        label = textObject.AddComponent<TextMesh>();
        label.text = "GOAL!\n恭喜進球！";
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = 0.32f;
        label.fontSize = 72;
        label.color = new Color(1f, 0.88f, 0.05f);

        for (int i = 0; i < 56; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Quad);
            piece.name = "Confetti";
            piece.transform.SetParent(transform, false);
            piece.transform.localPosition = new Vector3(Random.Range(-2.3f, 2.3f), Random.Range(0f, 1.6f), Random.Range(-0.8f, 0.8f));
            piece.transform.localScale = new Vector3(Random.Range(0.10f, 0.19f), Random.Range(0.13f, 0.25f), 1f);
            piece.GetComponent<Collider>().enabled = false;
            Renderer renderer = piece.GetComponent<Renderer>();
            renderer.material.color = Color.HSVToRGB((i * 0.13f) % 1f, 0.85f, 1f);
            confetti.Add(piece.transform);
            velocities.Add(new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-1.2f, -0.25f), Random.Range(-0.6f, 0.6f)));
        }
    }

    private void Update()
    {
        lifetime += Time.deltaTime;
        if (Camera.main != null)
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - Camera.main.transform.position);
        float pulse = 1f + Mathf.Sin(lifetime * 9f) * 0.12f;
        label.transform.localScale = Vector3.one * pulse;

        for (int i = 0; i < confetti.Count; i++)
        {
            velocities[i] += Vector3.down * 1.8f * Time.deltaTime;
            confetti[i].position += velocities[i] * Time.deltaTime;
            confetti[i].Rotate(270f * Time.deltaTime, 180f * Time.deltaTime, 90f * Time.deltaTime);
        }

        if (lifetime >= 3.4f)
            Destroy(gameObject);
    }
}
