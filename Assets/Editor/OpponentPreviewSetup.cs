using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OpponentPreviewSetup
{
    private const string OpponentFolder = "Assets/Character/Opponent";
    private const string ControllerPath = OpponentFolder + "/OpponentRun.controller";
    private const string MaterialPath = OpponentFolder + "/OpponentBlueRobed.mat";

    [MenuItem("Tools/Opponent/Add Free-Running Preview")]
    public static void AddFreeRunningPreview()
    {
        string modelPath = AssetDatabase.FindAssets("t:Model", new[] { OpponentFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(path => path.EndsWith(".fbx"));
        if (string.IsNullOrEmpty(modelPath))
        {
            Debug.LogError("[Opponent] No FBX model was found in Assets/Character/Opponent.");
            return;
        }

        ConfigureHumanoidImport(modelPath);
        AnimationClip runClip = AssetDatabase.LoadAllAssetsAtPath(modelPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));
        if (runClip == null)
        {
            Debug.LogError("[Opponent] The FBX does not contain an animation clip.");
            return;
        }

        AnimatorController controller = GetOrCreateRunController(runClip);
        GameObject existing = GameObject.Find("Opponent_FreeRun");
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        GameObject opponent = (GameObject)PrefabUtility.InstantiatePrefab(model);
        opponent.name = "Opponent_FreeRun";
        opponent.transform.position = new Vector3(0f, 0.02f, 10f);

        Animator animator = opponent.GetComponent<Animator>() ?? opponent.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
        }

        Material material = GetOrCreateMaterial();
        if (material != null)
        {
            foreach (Renderer renderer in opponent.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;
        }

        OpponentFreeRunner runner = opponent.AddComponent<OpponentFreeRunner>();
        runner.pathCenter = opponent.transform.position;
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(opponent.scene);
        Selection.activeGameObject = opponent;
        Debug.Log($"[Opponent] Added running preview using {runClip.name}.");
    }

    private static void ConfigureHumanoidImport(string modelPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null)
            return;

        importer.animationType = ModelImporterAnimationType.Human;
        importer.importAnimation = true;
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.loopTime = true;
            clip.loopPose = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    private static AnimatorController GetOrCreateRunController(AnimationClip runClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorState state = controller.layers[0].stateMachine.AddState("Run");
            state.motion = runClip;
            controller.layers[0].stateMachine.defaultState = state;
        }
        else
        {
            AnimatorState state = controller.layers[0].stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == "Run");
            if (state != null) state.motion = runClip;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static Material GetOrCreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "OpponentBlueRobed" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        string[] texturePaths = AssetDatabase.FindAssets("t:Texture2D", new[] { OpponentFolder })
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        Texture2D baseMap = texturePaths.Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
            .FirstOrDefault(texture => texture != null && !texture.name.Contains("normal") && !texture.name.Contains("metallic") && !texture.name.Contains("roughness"));
        Texture2D normalMap = texturePaths.Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
            .FirstOrDefault(texture => texture != null && texture.name.Contains("normal"));
        Texture2D metallicMap = texturePaths.Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
            .FirstOrDefault(texture => texture != null && texture.name.Contains("metallic"));

        if (baseMap != null) material.SetTexture("_BaseMap", baseMap);
        if (normalMap != null)
        {
            material.SetTexture("_BumpMap", normalMap);
            material.EnableKeyword("_NORMALMAP");
        }
        if (metallicMap != null) material.SetTexture("_MetallicGlossMap", metallicMap);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }
}
