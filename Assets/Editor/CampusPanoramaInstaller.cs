using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Builds a single continuous, buildings-only panorama around the pitch.</summary>
public static class CampusPanoramaInstaller
{
    private const string RootName = "TrackEdgeCampusPanoramaV4";
    private const string TexturePath = "Assets/Textures/Campus_TrackEdgePanorama_Cut.png";
    private const string MaterialPath = "Assets/Materials/Mat_TrackEdgeCampusPanorama.mat";

    [InitializeOnLoadMethod]
    private static void EnsureAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/PlaygroundScene.unity" ||
                GameObject.Find(RootName) != null)
                return;
            Install();
        };
    }

    [MenuItem("Playground/Use Continuous Campus Panorama")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play mode before installing the campus panorama.");
            return;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null)
        {
            Debug.LogError("Campus panorama image is missing: " + TexturePath);
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            material = new Material(shader) { name = "Mat_TrackEdgeCampusPanorama" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.mainTexture = texture;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetInt("_Cull", 0);
        // The accepted panorama has already been cropped at the running-track edge.
        material.SetTextureScale("_BaseMap", Vector2.one);
        // Keep the 45-degree left rotation, but place the non-matching image wrap
        // behind the player rather than across the spawn view.
        material.SetTextureOffset("_BaseMap", new Vector2(0.68056f, 0f));
        EditorUtility.SetDirty(material);

        RemoveIfPresent("CampusBuildingsPanorama");
        RemoveIfPresent("TrackEdgeCampusPanorama");
        RemoveIfPresent("TrackEdgeCampusPanoramaV2");
        RemoveIfPresent("TrackEdgeCampusPanoramaV3");
        RemoveIfPresent("CommandStandPhotoBackdrop");
        RemoveIfPresent("AttackingGoalPhotoBackdrop");
        RemoveIfPresent("RoundedPhotoBackdrop_NE");
        RemoveIfPresent("OuterGroundApron");
        RemoveIfPresent("PerimeterDecorations");
        RemoveIfPresent("FloodlightTowers");
        RemoveIfPresent(RootName);

        GameObject root = new GameObject(RootName);
        GameObject environment = GameObject.Find("Environment");
        if (environment != null)
            root.transform.SetParent(environment.transform, true);

        Mesh mesh = BuildTrackEdgeWall(radius: 41f, straightHalfLength: 50f, height: 55f, arcSegments: 32);
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/Scenes/PlaygroundScene.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("Track-edge campus panorama installed (no outer grass). ");
    }

    private static void RemoveIfPresent(string name)
    {
        // GameObject.Find ignores inactive legacy photo panels. Remove every
        // scene object with this name so an old backdrop cannot overlap V4.
        foreach (GameObject existing in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (existing.name == name && existing.scene.IsValid())
                Object.DestroyImmediate(existing);
        }
    }

    private static Mesh BuildTrackEdgeWall(float radius, float straightHalfLength, float height, int arcSegments)
    {
        var points = new System.Collections.Generic.List<Vector3>();
        // North turn: east track edge -> west track edge.
        for (int i = 0; i <= arcSegments; i++)
        {
            float angle = i / (float)arcSegments * Mathf.PI;
            points.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, straightHalfLength + Mathf.Sin(angle) * radius));
        }
        // West straight, then south turn, then east straight back to the start.
        points.Add(new Vector3(-radius, 0f, -straightHalfLength));
        for (int i = 1; i <= arcSegments; i++)
        {
            float angle = Mathf.PI + i / (float)arcSegments * Mathf.PI;
            points.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, -straightHalfLength + Mathf.Sin(angle) * radius));
        }
        points.Add(new Vector3(radius, 0f, straightHalfLength));

        int segments = points.Count - 1;
        float[] distanceAtPoint = new float[points.Count];
        for (int i = 1; i < points.Count; i++)
            distanceAtPoint[i] = distanceAtPoint[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        float totalLength = distanceAtPoint[points.Count - 1];
        Vector3[] vertices = new Vector3[points.Count * 2];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = distanceAtPoint[i] / totalLength;
            Vector3 point = points[i];
            int bottom = i * 2;
            vertices[bottom] = point;
            vertices[bottom + 1] = point + Vector3.up * height;
            uv[bottom] = new Vector2(1f - t, 0f);
            uv[bottom + 1] = new Vector2(1f - t, 1f);

            if (i == segments)
                continue;
            int tri = i * 6;
            triangles[tri] = bottom;
            triangles[tri + 1] = bottom + 3;
            triangles[tri + 2] = bottom + 2;
            triangles[tri + 3] = bottom;
            triangles[tri + 4] = bottom + 1;
            triangles[tri + 5] = bottom + 3;
        }

        Mesh mesh = new Mesh { name = "TrackEdgeCampusPanoramaMesh" };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
