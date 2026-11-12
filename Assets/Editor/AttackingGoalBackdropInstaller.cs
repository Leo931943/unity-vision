using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Legacy one-sided photo backdrop.  The stadium now uses the continuous
/// TrackEdgeCampusPanorama, so this component must never auto-create a panel.
/// </summary>
public static class AttackingGoalBackdropInstaller
{
    private const string ObjectName = "AttackingGoalPhotoBackdrop";
    private const string TexturePath = "Assets/Textures/AttackingGoal_Backdrop.jpg";
    private const string MaterialPath = "Assets/Materials/Mat_AttackingGoalBackdrop.mat";

    [InitializeOnLoadMethod]
    private static void RemoveLegacyBackdropAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/PlaygroundScene.unity")
                return;

            GameObject existing = GameObject.Find(ObjectName);
            if (existing == null)
                return;

            Object.DestroyImmediate(existing);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[Playground] Removed legacy attacking-goal photo backdrop.");
        };
    }

    // Retained only to keep any old editor references compiling.  The old
    // photo panel deliberately has no menu entry, preventing it from being
    // added back over the continuous panorama.
    public static void Install()
    {
        GameObject existing = GameObject.Find(ObjectName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null)
        {
            Debug.LogError("Attacking-goal backdrop image is missing: " + TexturePath);
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            material = new Material(shader) { name = "Mat_AttackingGoalBackdrop" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.mainTexture = texture;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetInt("_Cull", 0);
        material.SetTextureScale("_BaseMap", new Vector2(1f, 0.55f));
        material.SetTextureOffset("_BaseMap", Vector2.zero);
        EditorUtility.SetDirty(material);

        GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backdrop.name = ObjectName;
        // North goal is the one facing the player at spawn; z=90 is the outer
        // edge of its running-track turn, so the photo begins outside the track.
        backdrop.transform.position = new Vector3(0f, 18f, 90.2f);
        backdrop.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        backdrop.transform.localScale = new Vector3(198f, 36f, 1f);
        Object.DestroyImmediate(backdrop.GetComponent<Collider>());

        MeshRenderer renderer = backdrop.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        GameObject environment = GameObject.Find("Environment");
        if (environment != null)
            backdrop.transform.SetParent(environment.transform, true);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/Scenes/PlaygroundScene.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("Attacking-goal photo backdrop installed.");
    }
}
