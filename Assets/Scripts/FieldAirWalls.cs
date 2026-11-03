using UnityEngine;

/// <summary>
/// Guarantees that the playable infield is enclosed when the playground runs.
/// The generated objects have colliders only: no mesh renderer, no visible fence.
/// </summary>
public static class FieldAirWalls
{
    private const float Thickness = 0.6f;
    private const float Height = 6.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForPlaygroundScene()
    {
        // Keep this bootstrap limited to the generated playground and avoid
        // creating duplicates when the editor-built walls are already present.
        if (GameObject.Find("SoccerPitch") == null)
            return;

        AlignGoalsAndPitchMarkings();
        DisableGoalNetColliders();
        ApplyBluePuGoalAprons();
        RemoveLegacyGoalApronStrips();

        if (GameObject.Find("FieldAirWalls_Invisible") != null)
            return;

        GameObject group = new GameObject("FieldAirWalls_Invisible");
        CreateWall(group.transform, "AirWall_EastTouchline", new Vector3(32f, 0f, 0f), 0f,
            new Vector3(Thickness, Height, 100f));
        CreateWall(group.transform, "AirWall_WestTouchline", new Vector3(-32f, 0f, 0f), 0f,
            new Vector3(Thickness, Height, 100f));
        CreateGoalRecess(group.transform, "North", 50f);
        CreateGoalRecess(group.transform, "South", -50f);
    }

    private static void AlignGoalsAndPitchMarkings()
    {
        SetWorldZ("SoccerGoal_North", 47.5f);
        SetWorldZ("SoccerGoal_South", -47.5f);

        GameObject goals = GameObject.Find("SoccerGoals");
        if (goals == null)
            return;

        foreach (SoccerGoalDetector detector in goals.GetComponentsInChildren<SoccerGoalDetector>())
            detector.ballResetPosition = detector.goalName.Contains("北")
                ? new Vector3(0f, 0.25f, 39f)
                : new Vector3(0f, 0.25f, -39f);
    }

    private static void SetWorldZ(string objectName, float z)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null)
            return;

        Vector3 position = target.transform.position;
        target.transform.position = new Vector3(position.x, position.y, z);
    }

    private static void ApplyBluePuGoalAprons()
    {
        Renderer trackRenderer = GameObject.Find("Track_EastStraight")?.GetComponent<Renderer>();
        if (trackRenderer == null || trackRenderer.sharedMaterial == null)
            return;

        Material bluePu = new Material(trackRenderer.sharedMaterial) { name = "Runtime_BluePU_GoalApron" };
        bluePu.SetColor("_BaseColor", new Color(0.03f, 0.62f, 1.0f));
        bluePu.SetColor("_EmissionColor", new Color(0.005f, 0.10f, 0.28f));
        bluePu.EnableKeyword("_EMISSION");

        string[] apronNames = { "Turf_NorthSemicircle", "Turf_SouthSemicircle" };
        foreach (string apronName in apronNames)
        {
            Renderer apronRenderer = GameObject.Find(apronName)?.GetComponent<Renderer>();
            if (apronRenderer != null)
                apronRenderer.sharedMaterial = bluePu;
        }

    }

    private static void RemoveLegacyGoalApronStrips()
    {
        foreach (string name in new[] { "GoalApron_NorthStrip", "GoalApron_SouthStrip" })
        {
            GameObject strip = GameObject.Find(name);
            if (strip != null)
                Object.Destroy(strip);
        }
    }

    private static void DisableGoalNetColliders()
    {
        GameObject goals = GameObject.Find("SoccerGoals");
        if (goals == null)
            return;

        foreach (Collider collider in goals.GetComponentsInChildren<Collider>())
        {
            if (collider.gameObject.name.StartsWith("Net_"))
                collider.enabled = false;
        }
    }

    private static void CreateGoalRecess(Transform parent, string endName, float goalLineZ)
    {
        const float fieldHalfWidth = 32f;
        const float pocketHalfWidth = 4.2f;
        const float pocketDepth = 2.7f;
        float direction = Mathf.Sign(goalLineZ);
        float sideLength = fieldHalfWidth - pocketHalfWidth;

        CreateWall(parent, $"AirWall_{endName}_EndLeft", new Vector3(-(fieldHalfWidth + pocketHalfWidth) * 0.5f, 0f, goalLineZ), 0f,
            new Vector3(sideLength, Height, Thickness));
        CreateWall(parent, $"AirWall_{endName}_EndRight", new Vector3((fieldHalfWidth + pocketHalfWidth) * 0.5f, 0f, goalLineZ), 0f,
            new Vector3(sideLength, Height, Thickness));

        float backZ = goalLineZ + direction * pocketDepth;
        CreateWall(parent, $"AirWall_{endName}_GoalPocketLeft", new Vector3(-pocketHalfWidth, 0f, (goalLineZ + backZ) * 0.5f), 0f,
            new Vector3(Thickness, Height, pocketDepth));
        CreateWall(parent, $"AirWall_{endName}_GoalPocketRight", new Vector3(pocketHalfWidth, 0f, (goalLineZ + backZ) * 0.5f), 0f,
            new Vector3(Thickness, Height, pocketDepth));
        CreateWall(parent, $"AirWall_{endName}_GoalPocketBack", new Vector3(0f, 0f, backZ), 0f,
            new Vector3(pocketHalfWidth * 2f, Height, Thickness));
    }

    private static void CreateWall(Transform parent, string name, Vector3 position, float yRotation, Vector3 size)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        BoxCollider collider = wall.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, size.y * 0.5f, 0f);
        collider.size = size;
    }
}
