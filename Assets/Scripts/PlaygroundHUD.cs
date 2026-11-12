using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Frameless prompts and an optional H-key help guide.</summary>
public class PlaygroundHUD : MonoBehaviour
{
    public PlayerController player;

    private float goalTimer;
    private string lastGoalText = "";
    private int goalCount;
    private bool showHelp;

    private GUIStyle promptStyle;
    private GUIStyle shortcutStyle;
    private GUIStyle helpStyle;
    private GUIStyle goalStyle;

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        InitStyles();
    }

    private void Update()
    {
        if (goalTimer > 0f) goalTimer -= Time.deltaTime;
        if (WasHelpPressed()) showHelp = !showHelp;
    }

    public void ShowGoalNotification(string goalName)
    {
        goalCount++;
        lastGoalText = $"⚽ GOAL！進球得分！ ({goalName})  累計進球: {goalCount}";
        goalTimer = 3.5f;
    }

    private void InitStyles()
    {
        promptStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        shortcutStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerLeft,
            normal = { textColor = new Color(0.92f, 0.96f, 1f) }
        };
        helpStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15, alignment = TextAnchor.UpperLeft, normal = { textColor = Color.white }
        };
        goalStyle = new GUIStyle(promptStyle)
        {
            fontSize = 25, normal = { textColor = Color.yellow }
        };
    }

    private void OnGUI()
    {
        if (promptStyle == null) InitStyles();

        SoccerMatchManager match = SoccerMatchManager.Instance;
        bool isFreeMode = match == null || match.IsPracticeMode;
        if (isFreeMode)
            DrawShadowedLabel(new Rect(0f, 14f, Screen.width, 42f),
                "自由練習模式　按一下 Enter 跟劉銘傳單挑", promptStyle);

        string playerCooldown = $"抽射冷卻　玩家：{FormatCooldown(player != null ? player.PowerShotCooldownRemaining : 0f)}";
        DrawShadowedLabel(new Rect(22f, Screen.height - 78f, 460f, 28f), playerCooldown, shortcutStyle);
        if (match != null && !match.IsPracticeMode)
            DrawShadowedLabel(new Rect(22f, Screen.height - 106f, 460f, 28f),
                $"劉銘傳：{FormatCooldown(match.AiPowerShotCooldownRemaining)}", shortcutStyle);

        DrawShadowedLabel(new Rect(22f, Screen.height - 46f, 420f, 28f),
            showHelp ? "按下 H 隱藏操作指南" : "按下 H 顯示操作指南", shortcutStyle);

        if (showHelp)
        {
            const string guide = "操作指南\n" +
                                 "W / A / S / D：移動\n" +
                                 "Shift：衝刺　　Space：跳躍\n" +
                                 "E：靠近足球時抽射\n" +
                                 "滑鼠：旋轉視角　滾輪：縮放\n" +
                                 "R：回到中央　Esc：釋放滑鼠";
            DrawShadowedLabel(new Rect(22f, Screen.height - 235f, 430f, 155f), guide, helpStyle);
        }

        if (goalTimer > 0f)
            DrawShadowedLabel(new Rect(0f, Screen.height * 0.38f, Screen.width, 42f), lastGoalText, goalStyle);
    }

    private static void DrawShadowedLabel(Rect rect, string text, GUIStyle style)
    {
        Color originalColor = style.normal.textColor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        style.normal.textColor = originalColor;
        GUI.Label(rect, text, style);
    }

    private static string FormatCooldown(float remaining) => remaining <= 0.05f ? "可使用" : $"{remaining:F1} 秒";

    private static bool WasHelpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.H);
#endif
    }
}
