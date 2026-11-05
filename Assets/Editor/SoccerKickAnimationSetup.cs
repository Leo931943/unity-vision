using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SoccerKickAnimationSetup
{
    private const string ControllerPath = "Assets/Character/Meshy_Character/Meshy_Animator.controller";
    private const string ClipPath = "Assets/Character/Meshy_Character/PowerShot.anim";

    [MenuItem("Tools/Soccer/Install Power Shot Animation")]
    public static void Install()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = CreateKickClip();
            AssetDatabase.CreateAsset(clip, ClipPath);
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("[Soccer] Animator controller was not found.");
            return;
        }

        if (!HasParameter(controller, "PowerShot"))
            controller.AddParameter("PowerShot", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState kick = FindState(stateMachine, "PowerShot");
        if (kick == null)
        {
            kick = stateMachine.AddState("PowerShot");
            kick.motion = clip;
            kick.speed = 1f;

            AnimatorStateTransition enter = stateMachine.AddAnyStateTransition(kick);
            enter.hasExitTime = false;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "PowerShot");

            AddExitTransition(kick, FindState(stateMachine, "Idle"), AnimatorConditionMode.Less, 0.15f);
            AddExitTransition(kick, FindState(stateMachine, "Walk"), AnimatorConditionMode.Greater, 0.15f);
        }
        else
        {
            kick.motion = clip;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[Soccer] Power shot animation installed.");
    }

    private static AnimationClip CreateKickClip()
    {
        const float duration = 0.58f;
        AnimationClip clip = new AnimationClip { name = "PowerShot", frameRate = 30, wrapMode = WrapMode.ClampForever };

        // Plant the left leg and swing the right leg back, through the ball, then recover.
        SetCurve(clip, "Right Upper Leg Front-Back", duration, 0f, -0.30f, 0.78f, 0.18f, 0f);
        SetCurve(clip, "Right Lower Leg Stretch", duration, 0f, -0.18f, 0.70f, 0.18f, 0f);
        SetCurve(clip, "Right Foot Up-Down", duration, 0f, 0.10f, -0.48f, -0.08f, 0f);
        SetCurve(clip, "Left Upper Leg Front-Back", duration, 0f, 0.10f, -0.12f, -0.04f, 0f);
        SetCurve(clip, "Left Lower Leg Stretch", duration, 0f, 0.05f, 0.12f, 0.04f, 0f);
        SetCurve(clip, "Left Arm Front-Back", duration, 0f, 0.20f, -0.28f, -0.08f, 0f);
        SetCurve(clip, "Right Arm Front-Back", duration, 0f, -0.18f, 0.25f, 0.08f, 0f);
        SetCurve(clip, "Spine Front-Back", duration, 0f, -0.08f, 0.14f, 0.05f, 0f);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static void SetCurve(AnimationClip clip, string muscle, float duration, params float[] values)
    {
        Keyframe[] keys = new Keyframe[values.Length];
        for (int i = 0; i < values.Length; i++)
            keys[i] = new Keyframe(duration * i / (values.Length - 1), values[i]);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), new AnimationCurve(keys));
    }

    private static bool HasParameter(AnimatorController controller, string name)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
            if (parameter.name == name) return true;
        return false;
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
    {
        foreach (ChildAnimatorState child in stateMachine.states)
            if (child.state.name == name) return child.state;
        return null;
    }

    private static void AddExitTransition(AnimatorState source, AnimatorState destination, AnimatorConditionMode mode, float speed)
    {
        if (destination == null) return;
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = true;
        transition.exitTime = 0.9f;
        transition.duration = 0.05f;
        transition.AddCondition(mode, speed, "Speed");
    }
}
