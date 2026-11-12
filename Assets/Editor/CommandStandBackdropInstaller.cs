using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Adds the supplied command-stand photograph as a non-interactive background plane.</summary>
public static class CommandStandBackdropInstaller
{
    private const string ObjectName = "CommandStandPhotoBackdrop";
    private const string TexturePath = "Assets/Textures/CommandStand_Backdrop.png";
    private const string MaterialPath = "Assets/Materials/Mat_CommandStandBackdrop.mat";

    [MenuItem("Playground/Add Command Stand Photo Backdrop")]
    public static void Install()
    {
        GameObject existing = GameObject.Find(ObjectName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        // The procedural stand is only a placeholder. The photograph already
        // contains the real command stand and seating, so do not layer both.
        GameObject placeholderGrandstand = GameObject.Find("SpectatorGrandstand");
        if (placeholderGrandstand != null)
            Object.DestroyImmediate(placeholderGrandstand);

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null)
        {
            Debug.LogError("Command stand backdrop image is missing: " + TexturePath);
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            material = new Material(shader) { name = "Mat_CommandStandBackdrop" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.mainTexture = texture;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetInt("_Cull", 0);
        // Use the lower portion of the panorama: the game's own sky continues
        // above it, avoiding a visible rectangular patch of differently lit sky.
        material.SetTextureScale("_BaseMap", new Vector2(1f, 0.65f));
        material.SetTextureOffset("_BaseMap", Vector2.zero);
        EditorUtility.SetDirty(material);

        GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backdrop.name = ObjectName;
        // The east straight's outer edge is X = 40m.  Keep the photo just
        // beyond it so its lower edge meets the outside of the running track.
        backdrop.transform.position = new Vector3(40.2f, 9f, 0f);
        backdrop.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        backdrop.transform.localScale = new Vector3(110f, 18f, 1f);
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
        Debug.Log("Command-stand photo backdrop installed.");
    }
}
