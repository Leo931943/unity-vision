using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Temporary best-of-three football match flow. Each round lasts 90 seconds.</summary>
public class SoccerMatchManager : MonoBehaviour
{
    public static SoccerMatchManager Instance { get; private set; }

    [Min(1f)] public float roundDuration = 90f;
    public int maxRounds = 3;
    public int winsNeeded = 2;
    public bool RoundActive { get; private set; }
    public bool IsPracticeMode => matchFinished;
    public Rigidbody MatchBall { get; private set; }
    public string StatusText { get; private set; }
    public float AiPowerShotCooldownRemaining => opponent != null ? opponent.PowerShotCooldownRemaining : 0f;
    // Possession is deliberately short-lived: it means "who is currently
    // carrying the ball", rather than permanently locking the ball to a side.
    public bool PlayerHasPossession => RoundActive && lastTouchByPlayer && Time.time < lastTouchTime + possessionMemory;
    public bool OpponentHasPossession => RoundActive && !lastTouchByPlayer && Time.time < lastTouchTime + possessionMemory;
    public bool IsOpeningPhase => RoundActive && Time.time < roundStartedAt + openingPressureDuration;

    private PlayerController player;
    private SoccerOpponentAI opponent;
    private int roundNumber;
    private int playerWins;
    private int opponentWins;
    private float timeRemaining;
    private bool matchFinished;
    [SerializeField, Min(0.1f)] private float possessionMemory = 1.1f;
    [SerializeField, Min(0f)] private float openingPressureDuration = 10f;
    private bool lastTouchByPlayer;
    private float lastTouchTime = -999f;
    private float roundStartedAt = -999f;
    private string countdownText = "";
    private GUIStyle countdownStyle;
    private GUIStyle matchHudStyle;
    private GUIStyle teamScoreStyle;
    private Texture2D playerScoreBackground;
    private Texture2D opponentScoreBackground;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfNeeded()
    {
        if (GameObject.Find("SoccerPitch") == null || Object.FindFirstObjectByType<SoccerMatchManager>() != null)
            return;

        GameObject manager = new GameObject("SoccerMatchManager");
        manager.AddComponent<SoccerMatchManager>();
    }

    private void Awake()
    {
        Instance = this;
        DisableRetiredPhotoBackdrops();
        player = FindFirstObjectByType<PlayerController>();
        GameObject opponentObject = GameObject.Find("Opponent_FreeRun");
        if (opponentObject != null)
        {
            OpponentFreeRunner preview = opponentObject.GetComponent<OpponentFreeRunner>();
            if (preview != null) preview.enabled = false;
            opponent = opponentObject.GetComponent<SoccerOpponentAI>() ?? opponentObject.AddComponent<SoccerOpponentAI>();
        }

        MatchBall = FindCentralBall();
        DisableExtraBalls();
    }

    // The stadium is now one continuous panorama.  Older scene saves may
    // still contain these independent image quads; hide them at runtime so a
    // legacy white rectangle can never cover the goal again.
    private static void DisableRetiredPhotoBackdrops()
    {
        string[] retiredBackdrops =
        {
            "AttackingGoalPhotoBackdrop",
            "CommandStandPhotoBackdrop",
            "RoundedPhotoBackdrop_NE"
        };

        foreach (string backdropName in retiredBackdrops)
        {
            GameObject backdrop = GameObject.Find(backdropName);
            if (backdrop != null)
                backdrop.SetActive(false);
        }
    }

    private void Start()
    {
        if (player == null || opponent == null || MatchBall == null)
        {
            StatusText = "找不到玩家、AI 或中圈足球，無法開始比賽。";
            return;
        }
        // Playing the scene starts as a single-player warm-up. Enter begins
        // the actual best-of-three match when the player is ready.
        EnterPracticeMode("自由練習模式");
    }

    private void Update()
    {
        if (matchFinished && WasReplayPressed())
        {
            RestartMatch();
            return;
        }

        if (!RoundActive) return;
        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0f)
            FinishRound(-1, "時間到，這局平手！");
    }

    public void RegisterGoal(SoccerGoalDetector detector)
    {
        if (!RoundActive || detector == null) return;
        GoalCelebrationEffect.Play(detector.transform.position);
        // Player attacks north; AI attacks south.
        bool playerScored = detector.goalName.Contains("北");
        FinishRound(playerScored ? 0 : 1, playerScored ? "玩家進球，拿下本局！" : "AI 進球，拿下本局！");
    }

    public void RegisterBallTouch(bool byPlayer)
    {
        if (!RoundActive || player == null || opponent == null) return;
        lastTouchByPlayer = byPlayer;
        lastTouchTime = Time.time;
        player.SetPossessionSlowdown(byPlayer);
        opponent.SetPossessionSlowdown(!byPlayer);
    }

    private IEnumerator BeginRoundAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        roundNumber++;
        ResetKickoff();
        opponent.SetWaitingForKickoff(true);
        player.enabled = false;
        StatusText = $"第 {roundNumber}/{maxRounds} 局準備開球";
        if (roundNumber == 1)
            MatchAudioDirector.PlayOpeningSequence();

        for (int count = 3; count >= 1; count--)
        {
            countdownText = count.ToString();
            yield return new WaitForSeconds(1f);
        }
        countdownText = "開始！";
        yield return new WaitForSeconds(0.45f);
        countdownText = "";
        timeRemaining = roundDuration;
        RoundActive = true;
        roundStartedAt = Time.time;
        player.enabled = true;
        opponent.SetWaitingForKickoff(false);
        StatusText = $"第 {roundNumber}/{maxRounds} 局開始！";
    }

    private void FinishRound(int winner, string result)
    {
        if (!RoundActive) return;
        RoundActive = false;
        if (winner == 0) playerWins++;
        else if (winner == 1) opponentWins++;

        // A finished best-of-three match goes straight to practice mode. Do not
        // leave the AI visible during the normal between-round delay.
        if (playerWins >= winsNeeded || opponentWins >= winsNeeded || roundNumber >= maxRounds)
        {
            string matchWinner = playerWins == opponentWins ? "比賽平手！" : playerWins > opponentWins ? "玩家贏得比賽！" : "AI 贏得比賽！";
            bool playerWonMatch = playerWins > opponentWins;
            bool playerLostMatch = opponentWins > playerWins;
            if (playerWonMatch)
                MatchAudioDirector.PlayVictory();
            else if (playerLostMatch)
                MatchAudioDirector.PlayDefeat();
            EnterPracticeMode($"{matchWinner} 最終比分 {playerWins}:{opponentWins}", playerWonMatch || playerLostMatch);
            return;
        }

        StatusText = result;
        StartCoroutine(ContinueMatch());
    }

    private IEnumerator ContinueMatch()
    {
        yield return new WaitForSeconds(2.5f);
        StartCoroutine(BeginRoundAfterDelay(0.5f));
    }

    private void ResetKickoff()
    {
        SetBallPosition(Vector3.zero);
        player.SetMatchSpawn(new Vector3(0f, 0.02f, -2.1f), Vector3.forward);
        opponent.ResetForKickoff(new Vector3(0f, 0.02f, 2.1f), Vector3.back);
        player.SetPossessionSlowdown(false);
        opponent.SetPossessionSlowdown(false);
        lastTouchByPlayer = false;
        lastTouchTime = -999f;
    }

    private void EnterPracticeMode(string result, bool keepMatchEndMusic = false)
    {
        if (!keepMatchEndMusic)
            MatchAudioDirector.StopAll();
        matchFinished = true;
        RoundActive = false;
        player.enabled = true;
        opponent.gameObject.SetActive(false);
        SetBallPosition(Vector3.zero);
        player.SetMatchSpawn(new Vector3(0f, 0.02f, -2.1f), Vector3.forward);
        StatusText = $"{result}\n練習模式：AI 已離場；按 [Enter] 再玩一次。";
    }

    private void RestartMatch()
    {
        MatchAudioDirector.PrepareForNewMatch();
        matchFinished = false;
        playerWins = 0;
        opponentWins = 0;
        roundNumber = 0;
        opponent.gameObject.SetActive(true);
        StartCoroutine(BeginRoundAfterDelay(0.35f));
    }

    private static bool WasReplayPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    private Rigidbody FindCentralBall()
    {
        foreach (Rigidbody candidate in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            if (candidate.CompareTag("SoccerBall") && candidate.name.Contains("Center")) return candidate;
        foreach (Rigidbody candidate in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            if (candidate.CompareTag("SoccerBall")) return candidate;
        return null;
    }

    private void DisableExtraBalls()
    {
        foreach (Rigidbody candidate in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            if (candidate.CompareTag("SoccerBall") && candidate != MatchBall) candidate.gameObject.SetActive(false);
    }

    private void SetBallPosition(Vector3 position)
    {
        MatchBall.linearVelocity = Vector3.zero;
        MatchBall.angularVelocity = Vector3.zero;
        MatchBall.transform.position = new Vector3(position.x, 0.22f, position.z);
    }

    public string GetHudText()
    {
        string timer = RoundActive ? Mathf.CeilToInt(Mathf.Max(0, timeRemaining)).ToString() + " 秒" : "準備中";
        string headline = matchFinished ? "⚽ 練習模式" : $"⚽ 比賽：第 {roundNumber}/{maxRounds} 局  玩家 {playerWins}:{opponentWins} AI  剩餘 {timer}";
        return $"{headline}\n{StatusText}";
    }

    private void OnGUI()
    {
        if (countdownStyle == null)
        {
            countdownStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 92,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        if (matchHudStyle == null)
        {
            matchHudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white }
            };
        }

        if (teamScoreStyle == null)
        {
            teamScoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            playerScoreBackground = MakeColorTexture(new Color(0.8f, 0.1f, 0.12f, 0.92f));
            opponentScoreBackground = MakeColorTexture(new Color(0.08f, 0.28f, 0.8f, 0.92f));
        }

        if (!matchFinished && roundNumber > 0)
        {
            string clock = RoundActive
                ? $"倒數 {FormatClock(timeRemaining)}"
                : string.IsNullOrEmpty(countdownText) ? "準備下一局" : $"開球倒數 {countdownText}";
            float cardWidth = 190f;
            float gap = 12f;
            float leftX = Screen.width * 0.5f - cardWidth - gap * 0.5f;
            DrawTeamScore(new Rect(leftX, 14f, cardWidth, 58f), "玩家", playerWins, playerScoreBackground);
            DrawTeamScore(new Rect(leftX + cardWidth + gap, 14f, cardWidth, 58f), "劉銘傳", opponentWins, opponentScoreBackground);
            DrawTopCenterLabel(new Rect(0f, 76f, Screen.width, 30f), clock, matchHudStyle);
        }

        if (!string.IsNullOrEmpty(countdownText))
            GUI.Label(new Rect(0f, Screen.height * 0.28f, Screen.width, 120f), countdownText, countdownStyle);
    }

    private static void DrawTopCenterLabel(Rect rect, string text, GUIStyle style)
    {
        Color originalColor = style.normal.textColor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        style.normal.textColor = originalColor;
        GUI.Label(rect, text, style);
    }

    private void DrawTeamScore(Rect rect, string name, int score, Texture2D background)
    {
        GUI.DrawTexture(rect, background);
        GUI.Label(rect, $"{name}　{score}", teamScoreStyle);
    }

    private static Texture2D MakeColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private static string FormatClock(float secondsRemaining)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }
}
