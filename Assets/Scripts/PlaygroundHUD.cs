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
    private GUIStyle modePanelStyle;
    private GUIStyle weatherPanelStyle;
    private GUIStyle panelShadowStyle;
    private GUIStyle weatherTitleStyle;
    private GUIStyle weatherTimeStyle;
    private GUIStyle temperatureStyle;
    private GUIStyle weatherStatusStyle;
    private ThirdPersonCamera playerCamera;
    private CwaWeatherService weatherService;

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        playerCamera = FindFirstObjectByType<ThirdPersonCamera>();
        weatherService = CwaWeatherService.Instance;
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
            fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
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
        modePanelStyle = CreatePanelStyle(new Color(0.02f, 0.06f, 0.11f, 0.76f));
        weatherPanelStyle = CreatePanelStyle(new Color(0.015f, 0.04f, 0.08f, 0.88f));
        panelShadowStyle = CreatePanelStyle(new Color(0f, 0f, 0f, 0.50f));
        weatherTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };
        weatherTimeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.72f, 0.81f, 0.91f) }
        };
        temperatureStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 21,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(0.48f, 0.89f, 1f) }
        };
        weatherStatusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.88f, 0.93f, 0.98f) }
        };
    }

    private void OnGUI()
    {
        if (promptStyle == null) InitStyles();
        if (weatherService == null) weatherService = CwaWeatherService.Instance;

        SoccerMatchManager match = SoccerMatchManager.Instance;
        bool isFreeMode = match == null || match.IsPracticeMode;
        if (isFreeMode)
            DrawModeBanner("自由練習模式　按一下 Enter 跟劉銘傳單挑");

        string playerCooldown = $"抽射冷卻　玩家：{FormatCooldown(player != null ? player.PowerShotCooldownRemaining : 0f)}";
        DrawShadowedLabel(new Rect(22f, Screen.height - 78f, 460f, 28f), playerCooldown, shortcutStyle);
        if (match != null && !match.IsPracticeMode)
            DrawShadowedLabel(new Rect(22f, Screen.height - 106f, 460f, 28f),
                $"劉銘傳：{FormatCooldown(match.AiPowerShotCooldownRemaining)}", shortcutStyle);

        bool firstPerson = playerCamera != null && playerCamera.IsFirstPerson;
        string viewMode = firstPerson ? "第一人稱" : "第三人稱";
        string helpPrompt = showHelp ? "H：隱藏操作指南" : "H：顯示操作指南";
        DrawShadowedLabel(new Rect(22f, Screen.height - 46f, 620f, 28f),
            $"{helpPrompt}　　V：切換視角（目前：{viewMode}）", shortcutStyle);

        DrawWeatherPanel();

        if (showHelp)
        {
            const string guide = "操作指南\n" +
                                 "W / A / S / D：移動\n" +
                                 "Shift：衝刺　　Space：跳躍\n" +
                                 "E：靠近足球時抽射\n" +
                                 "滑鼠：旋轉視角　滾輪：縮放　V：第一／第三人稱\n" +
                                 "R：回到中央　Esc：釋放滑鼠";
            DrawShadowedLabel(new Rect(22f, Screen.height - 263f, 500f, 183f), guide, helpStyle);
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

    private static GUIStyle CreatePanelStyle(Color color)
    {
        Texture2D background = CreateRoundedTexture(color, 10);
        return new GUIStyle(GUI.skin.box)
        {
            normal = { background = background },
            border = new RectOffset(10, 10, 10, 10)
        };
    }

    private static Texture2D CreateRoundedTexture(Color color, int radius)
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float cornerCenter = radius - 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nearestX = Mathf.Clamp(x, cornerCenter, size - 1f - cornerCenter);
                float nearestY = Mathf.Clamp(y, cornerCenter, size - 1f - cornerCenter);
                float cornerDistance = Vector2.Distance(new Vector2(x, y), new Vector2(nearestX, nearestY));
                texture.SetPixel(x, y, cornerDistance <= radius ? color : Color.clear);
            }
        }

        texture.Apply();
        return texture;
    }

    private void DrawModeBanner(string text)
    {
        float width = Mathf.Min(570f, Screen.width - 44f);
        Rect bannerRect = new Rect((Screen.width - width) * 0.5f, 10f, width, 42f);
        DrawPanel(bannerRect, modePanelStyle);
        GUI.Label(bannerRect, text, promptStyle);
    }

    private void DrawPanel(Rect rect, GUIStyle panelStyle)
    {
        GUI.Box(new Rect(rect.x + 2f, rect.y + 3f, rect.width, rect.height), GUIContent.none, panelShadowStyle);
        GUI.Box(rect, GUIContent.none, panelStyle);
    }

    private void DrawWeatherPanel()
    {
        if (weatherService == null)
            return;

        float width = Mathf.Clamp(Screen.width * 0.35f, 320f, 390f);
        const float panelHeight = 80f;
        float panelY = Mathf.Max(62f, Screen.height - panelHeight - 18f);
        Rect panelRect = new Rect(Screen.width - width - 18f, panelY, width, panelHeight);
        DrawPanel(panelRect, weatherPanelStyle);

        float contentX = panelRect.x + 16f;
        float contentWidth = panelRect.width - 32f;
        GUI.Label(new Rect(contentX, panelRect.y + 7f, contentWidth, 22f),
            "銘傳大學｜桃園校區", weatherTitleStyle);
        GUI.Label(new Rect(contentX, panelRect.y + 29f, contentWidth - 88f, 22f),
            weatherService.TaiwanTimeText, weatherTimeStyle);
        GUI.Label(new Rect(panelRect.x + panelRect.width - 104f, panelRect.y + 25f, 88f, 28f),
            weatherService.TemperatureText.Replace(" ", ""), temperatureStyle);

        string status = weatherService.HasWeatherData
            ? $"{weatherService.WeatherText}　·　最近測站：{weatherService.ObservationStation}"
            : weatherService.WeatherText;
        GUI.Label(new Rect(contentX, panelRect.y + 54f, contentWidth, 19f), status, weatherStatusStyle);
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
