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
            EnterPracticeMode($"{matchWinner} 最終比分 {playerWins}:{opponentWins}");
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

    private void EnterPracticeMode(string result)
    {
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
        if (string.IsNullOrEmpty(countdownText)) return;
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
        GUI.Label(new Rect(0f, Screen.height * 0.28f, Screen.width, 120f), countdownText, countdownStyle);
    }
}
