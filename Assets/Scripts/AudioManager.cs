using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    #region Singleton
    public static AudioManager Instance { get; private set; }
    #endregion

    #region Audio Sources
    [Header("Audio Sources")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;
    #endregion

    #region BGM Clips
    [Header("BGM Clips")]
    public AudioClip titleAndRankingBGM; // タイトル画面・ランキング画面共通BGM
    public AudioClip tutorialBGM; // チュートリアル中のBGM
    public AudioClip gameBGM; // 本ゲーム中のBGM
    #endregion

    #region SFX Clips
    [Header("SFX Clips")]
    public AudioClip normalPlatformHitSfx; // 通常足場を叩く効果音
    public AudioClip fragilePlatformHitSfx; // 脆い足場を叩く効果音
    public AudioClip repairCompleteSfx; // 修繕完了時の効果音
    public AudioClip bridgeCollapseSfx; // 橋崩落時の効果音
    #endregion

    #region Private Fields
    private AudioClip _currentBGM;
    private bool _isBGMPlaying;
    private float _defaultBGMVolume = .5f;
    private float _defaultSfxVolume = .5f;
    private float _fadeSpeed = 2.0f;

    // PlayerPrefsのキー
    private const string BgmVolumeKey = "BGMVolume";
    private const string SfxVolumeKey = "SFXVolume";
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singletonパターンの実装
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // タイトル画面のBGMを開始
        PlayTitleAndRankingBGM();
    }
    #endregion

    #region Initialization
    private void InitializeAudioSources()
    {
        // BGM AudioSourceが設定されていない場合は作成
        if (bgmSource == null)
        {
            GameObject bgmObject = new GameObject("BGM Source");
            bgmObject.transform.SetParent(transform);
            bgmSource = bgmObject.AddComponent<AudioSource>();
        }

        // SFX AudioSourceが設定されていない場合は作成
        if (sfxSource == null)
        {
            GameObject sfxObject = new GameObject("SFX Source");
            sfxObject.transform.SetParent(transform);
            sfxSource = sfxObject.AddComponent<AudioSource>();
        }

        // 保存された音量設定を読み込み
        LoadVolumeSettings();

        // BGM設定
        bgmSource.loop = true;
        bgmSource.volume = _defaultBGMVolume;
        bgmSource.playOnAwake = false;

        // SFX設定
        sfxSource.loop = false;
        sfxSource.volume = _defaultSfxVolume;
        sfxSource.playOnAwake = false;
    }
    #endregion

    #region BGM Control Methods
    /// <summary>
    /// タイトル画面・ランキング画面共通のBGMを再生
    /// </summary>
    public void PlayTitleAndRankingBGM()
    {
        PlayBGM(titleAndRankingBGM);
    }

    /// <summary>
    /// チュートリアル中のBGMを再生
    /// </summary>
    public void PlayTutorialBGM()
    {
        PlayBGM(tutorialBGM);
    }

    /// <summary>
    /// 本ゲーム中のBGMを再生
    /// </summary>
    public void PlayGameBGM()
    {
        PlayBGM(gameBGM, false);
    }

    /// <summary>
    /// BGMを停止
    /// </summary>
    public void StopBGM()
    {
        if (bgmSource.isPlaying)
        {
            StartCoroutine(FadeOutBGM());
        }
    }

    /// <summary>
    /// 指定されたBGMを再生（フェードイン付き）
    /// </summary>
    /// <param name="clip">再生するAudioClip</param>
    /// <param name="isLoop">ループ再生するかどうか</param>
    private void PlayBGM(AudioClip clip, bool isLoop = true)
    {
        if (clip == null)
        {
            Debug.LogWarning("BGM clip is null!");
            return;
        }

        // 同じBGMが既に再生中の場合は何もしない
        if (_currentBGM == clip && bgmSource.isPlaying)
        {
            return;
        }

        StartCoroutine(ChangeBGM(clip, isLoop));
    }

    /// <summary>
    /// BGMを変更（フェードアウト→即座に再生）
    /// </summary>
    /// <param name="newClip">新しいBGMクリップ</param>
    /// <param name="isLoop">ループ再生するかどうか</param>
    private IEnumerator ChangeBGM(AudioClip newClip, bool isLoop = true)
    {
        // 現在のBGMをフェードアウト
        if (bgmSource.isPlaying)
        {
            yield return StartCoroutine(FadeOutBGM());
        }

        // 新しいBGMを設定して即座に再生
        _currentBGM = newClip;
        bgmSource.clip = newClip;
        bgmSource.loop = isLoop;
        bgmSource.volume = _defaultBGMVolume; // フェードインなしで即座に設定
        bgmSource.Play();
        _isBGMPlaying = true;
    }

    /// <summary>
    /// BGMをフェードアウト
    /// </summary>
    private IEnumerator FadeOutBGM()
    {
        float startVolume = bgmSource.volume;
        float currentTime = 0f;
        float fadeDuration = 1f / _fadeSpeed;

        while (currentTime < fadeDuration)
        {
            currentTime += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, currentTime / fadeDuration);
            yield return null;
        }

        bgmSource.volume = 0f;
        bgmSource.Stop();
        _isBGMPlaying = false;
    }
    #endregion

    #region Volume Control
    /// <summary>
    /// BGMの音量を設定
    /// </summary>
    /// <param name="volume">音量（0.0-1.0）</param>
    public void SetBGMVolume(float volume)
    {
        _defaultBGMVolume = Mathf.Clamp01(volume);
        if (bgmSource != null)
        {
            bgmSource.volume = _defaultBGMVolume;
        }
        SaveVolumeSettings();
    }

    /// <summary>
    /// SFXの音量を設定
    /// </summary>
    /// <param name="volume">音量（0.0-1.0）</param>
    public void SetSfxVolume(float volume)
    {
        _defaultSfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
        {
            sfxSource.volume = _defaultSfxVolume;
        }
        SaveVolumeSettings();
    }

    /// <summary>
    /// 現在のBGM音量を取得
    /// </summary>
    /// <returns>BGM音量（0.0-1.0）</returns>
    public float GetBGMVolume()
    {
        return _defaultBGMVolume;
    }

    /// <summary>
    /// 現在のSFX音量を取得
    /// </summary>
    /// <returns>SFX音量（0.0-1.0）</returns>
    public float GetSfxVolume()
    {
        return _defaultSfxVolume;
    }

    /// <summary>
    /// 音量設定を保存
    /// </summary>
    private void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat(BgmVolumeKey, _defaultBGMVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, _defaultSfxVolume);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 音量設定を読み込み
    /// </summary>
    private void LoadVolumeSettings()
    {
        _defaultBGMVolume = PlayerPrefs.GetFloat(BgmVolumeKey, 0.5f);
        _defaultSfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 0.5f);
    }
    #endregion

    #region SFX Methods
    /// <summary>
    /// 効果音を再生
    /// </summary>
    /// <param name="clip">再生するAudioClip</param>
    public void PlaySfx(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            Debug.Log($"Playing SFX: {clip.name}");
            sfxSource.PlayOneShot(clip);
        }
        else
        {
            if (clip == null)
                Debug.LogWarning("PlaySfx: AudioClip is null!");
            if (sfxSource == null)
                Debug.LogWarning("PlaySfx: SFX AudioSource is null!");
        }
    }

    /// <summary>
    /// 通常足場を叩く効果音を再生
    /// </summary>
    public void PlayNormalPlatformHitSfx()
    {
        PlaySfx(normalPlatformHitSfx);
    }

    /// <summary>
    /// 脆い足場を叩く効果音を再生
    /// </summary>
    public void PlayFragilePlatformHitSfx()
    {
        PlaySfx(fragilePlatformHitSfx);
    }

    /// <summary>
    /// 修繕完了時の効果音を再生
    /// </summary>
    public void PlayRepairCompleteSfx()
    {
        PlaySfx(repairCompleteSfx);
    }

    /// <summary>
    /// 橋崩落時の効果音を再生
    /// </summary>
    public void PlayBridgeCollapseSfx()
    {
        Debug.Log("PlayBridgeCollapseSfx called");
        if (bridgeCollapseSfx == null)
        {
            Debug.LogWarning("bridgeCollapseSfx is null! Please assign an audio clip in the AudioManager inspector.");
            
            // 代替案：他の音声ファイルでテスト（音が出るかどうかを確認）
            if (fragilePlatformHitSfx != null)
            {
                Debug.Log("Using fragilePlatformHitSfx as fallback for bridge collapse sound");
                PlaySfx(fragilePlatformHitSfx);
            }
            else if (normalPlatformHitSfx != null)
            {
                Debug.Log("Using normalPlatformHitSfx as fallback for bridge collapse sound");
                PlaySfx(normalPlatformHitSfx);
            }
            else
            {
                Debug.LogError("No fallback audio clips available!");
            }
            return;
        }
        PlaySfx(bridgeCollapseSfx);
    }
    #endregion

    #region Game State Integration
    /// <summary>
    /// ゲーム状態に応じてBGMを変更
    /// </summary>
    /// <param name="gameState">ゲーム状態</param>
    public void OnGameStateChanged(GameManager.GameState gameState)
    {
        switch (gameState)
        {
            case GameManager.GameState.Title:
                PlayTitleAndRankingBGM();
                break;
            case GameManager.GameState.Tutorial:
                PlayTutorialBGM();
                break;
            case GameManager.GameState.TutorialCountdown:
            case GameManager.GameState.Playing:
            case GameManager.GameState.Result:
                PlayGameBGM();
                break;
        }
    }
    #endregion
}
