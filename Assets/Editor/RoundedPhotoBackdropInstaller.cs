using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Rounds the north-east join between the two photo backdrops.</summary>
public static class RoundedPhotoBackdropInstaller
{
    private const string CornerRootName = "RoundedPhotoBackdrop_NE";
    private const string CommandMaterialPath = "Assets/Materials/Mat_CommandStandBackdrop.mat";
    private const string GoalMaterialPath = "Assets/Materials/Mat_AttackingGoalBackdrop.mat";

    [InitializeOnLoadMethod]
    private static void EnsureRoundedJoinAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/PlaygroundScene.unity" ||
                GameObject.Find(CornerRootName) != null)
                return;

            Install();
        };
    }

    [MenuItem("Playground/Round North-East Photo Backdrop Join")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play mode before rounding photo backdrops.");
            return;
        }

        Material commandMaterial = AssetDatabase.LoadAssetAtPath<Material>(CommandMaterialPath);
        Material goalMaterial = AssetDatabase.LoadAssetAtPath<Material>(GoalMaterialPath);
        GameObject command = GameObject.Find("CommandStandPhotoBackdrop");
        GameObject goal = GameObject.Find("AttackingGoalPhotoBackdrop");
        if (command == null || goal == null || commandMaterial == null || goalMaterial == null)
        {
            Debug.LogError("Photo backdrops must be installed before rounding their join.");
            return;
        }

        const float height = 36f;
        const float radius = 40.2f;

        // Leave space for the quarter-circle corner: the east image ends at
        // z=50, while the north image begins at x=0.
        command.transform.position = new Vector3(40.2f, height * 0.5f, 0f);
        command.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        command.transform.localScale = new Vector3(100f, height, 1f);
        goal.transform.position = new Vector3(-90f, height * 0.5f, 90.2f);
        goal.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        goal.transform.localScale = new Vector3(180f, height, 1f);

        GameObject oldCorner = GameObject.Find(CornerRootName);
        if (oldCorner != null)
            Object.DestroyImmediate(oldCorner);

        GameObject root = new GameObject(CornerRootName);
        GameObject environment = GameObject.Find("Environment");
        if (environment != null)
            root.transform.SetParent(environment.transform, true);

        // The two half-arcs meet tangentially, so the previous 90-degree
        // cardboard corner becomes a continuous rounded stadium wall.
        CreateArcPanel(root.transform, "Corner_EastPhoto", commandMaterial, radius, 0f, 45f, height);
        CreateArcPanel(root.transform, "Corner_NorthPhoto", goalMaterial, radius, 45f, 90f, height);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/Scenes/PlaygroundScene.unity");
        Debug.Log("Rounded north-east photo backdrop join installed.");
    }

    private static void CreateArcPanel(Transform parent, string objectName, Material material,
        float radius, float startDegrees, float endDegrees, float height)
    {
        const int segments = 14;
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(startDegrees, endDegrees, t) * Mathf.Deg2Rad;
            Vector3 point = new Vector3(Mathf.Cos(angle) * radius, 0f, 50f + Mathf.Sin(angle) * radius);
            int bottom = i * 2;
            vertices[bottom] = point;
            vertices[bottom + 1] = point + Vector3.up * height;
            uv[bottom] = new Vector2(t, 0f);
            uv[bottom + 1] = new Vector2(t, 1f);

            if (i == segments)
                continue;

            int tri = i * 6;
            triangles[tri] = bottom;
            triangles[tri + 1] = bottom + 2;
            triangles[tri + 2] = bottom + 3;
            triangles[tri + 3] = bottom;
            triangles[tri + 4] = bottom + 3;
            triangles[tri + 5] = bottom + 1;
        }

        Mesh mesh = new Mesh { name = objectName + "_Mesh" };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject panel = new GameObject(objectName);
        panel.transform.SetParent(parent, false);
        panel.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = panel.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }
}
