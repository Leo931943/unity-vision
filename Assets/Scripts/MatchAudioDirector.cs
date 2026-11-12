using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Match-only audio flow.  Countdown audio plays first, then the supplied BGM
/// begins looping.  Practice mode and the pre-match screen stay silent.
/// </summary>
public sealed class MatchAudioDirector : MonoBehaviour
{
    private const string CountdownFile = "match_countdown.wav";
    private const string BackgroundFile = "match_background.mp3";
    private const string VictoryFile = "match_victory.mp3";
    private const string DefeatFile = "match_defeat.wav";

    private static MatchAudioDirector instance;
    private AudioSource countdownSource;
    private AudioSource backgroundSource;
    private AudioClip countdownClip;
    private AudioClip backgroundClip;
    private AudioClip victoryClip;
    private AudioClip defeatClip;
    private bool assetsLoaded;
    private bool loadingAssets;
    private bool openingSequenceStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfNeeded()
    {
        if (instance != null)
            return;

        GameObject existing = GameObject.Find("Match Audio Director");
        if (existing != null)
        {
            instance = existing.GetComponent<MatchAudioDirector>();
            if (instance != null)
                return;
        }

        GameObject root = new GameObject("Match Audio Director");
        instance = root.AddComponent<MatchAudioDirector>();
        DontDestroyOnLoad(root);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        countdownSource = gameObject.AddComponent<AudioSource>();
        countdownSource.spatialBlend = 0f;
        countdownSource.volume = 0.9f;
        countdownSource.priority = 80;

        backgroundSource = gameObject.AddComponent<AudioSource>();
        backgroundSource.spatialBlend = 0f;
        backgroundSource.volume = 0.20f;
        backgroundSource.loop = true;
        backgroundSource.priority = 120;

        // Loading is silent and lets the countdown start promptly when Enter
        // is pressed; the BGM is never played from this preload.
        StartCoroutine(EnsureAudioLoaded());
    }

    public static void PrepareForNewMatch()
    {
        CreateIfNeeded();
        instance.StopAllInternal();
    }

    public static void PlayOpeningSequence()
    {
        CreateIfNeeded();
        if (instance.openingSequenceStarted)
            return;

        instance.openingSequenceStarted = true;
        instance.StartCoroutine(instance.PlayOpeningSequenceRoutine());
    }

    public static void StopAll()
    {
        CreateIfNeeded();
        instance.StopAllInternal();
    }

    public static void PlayVictory()
    {
        CreateIfNeeded();
        instance.StartCoroutine(instance.PlayVictoryRoutine());
    }

    public static void PlayDefeat()
    {
        CreateIfNeeded();
        instance.StartCoroutine(instance.PlayDefeatRoutine());
    }

    private IEnumerator PlayOpeningSequenceRoutine()
    {
        yield return EnsureAudioLoaded();

        if (countdownClip != null)
        {
            countdownSource.clip = countdownClip;
            countdownSource.Play();
            yield return new WaitWhile(() => countdownSource.isPlaying);
        }

        if (backgroundClip != null && !backgroundSource.isPlaying)
        {
            backgroundSource.clip = backgroundClip;
            backgroundSource.Play();
        }
    }

    private IEnumerator PlayVictoryRoutine()
    {
        StopAllInternal();
        yield return EnsureAudioLoaded();

        if (victoryClip == null)
            yield break;

        countdownSource.clip = victoryClip;
        countdownSource.Play();
    }

    private IEnumerator PlayDefeatRoutine()
    {
        StopAllInternal();
        yield return EnsureAudioLoaded();

        // Give the final score/result a brief beat before the defeat cue.
        yield return new WaitForSeconds(0.5f);

        if (defeatClip == null)
            yield break;

        countdownSource.clip = defeatClip;
        countdownSource.Play();
    }

    private IEnumerator EnsureAudioLoaded()
    {
        if (assetsLoaded)
            yield break;

        if (loadingAssets)
        {
            yield return new WaitUntil(() => assetsLoaded || !loadingAssets);
            yield break;
        }

        loadingAssets = true;
        yield return LoadClip(CountdownFile, AudioType.WAV, clip => countdownClip = clip);
        yield return LoadClip(BackgroundFile, AudioType.MPEG, clip => backgroundClip = clip);
        yield return LoadClip(VictoryFile, AudioType.MPEG, clip => victoryClip = clip);
        yield return LoadClip(DefeatFile, AudioType.WAV, clip => defeatClip = clip);
        assetsLoaded = true;
        loadingAssets = false;
    }

    private static IEnumerator LoadClip(string fileName, AudioType type, Action<AudioClip> onLoaded)
    {
        string localPath = Path.Combine(Application.streamingAssetsPath, "Audio", fileName);
        string url = new Uri(localPath).AbsoluteUri;
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, type))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Audio] 無法載入 {fileName}: {request.error}");
                yield break;
            }

            onLoaded(DownloadHandlerAudioClip.GetContent(request));
        }
    }

    private void StopAllInternal()
    {
        openingSequenceStarted = false;
        if (countdownSource != null) countdownSource.Stop();
        if (backgroundSource != null) backgroundSource.Stop();
    }
}
