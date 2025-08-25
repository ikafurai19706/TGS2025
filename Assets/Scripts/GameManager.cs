using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    #region Singleton
    public static GameManager Instance { get; private set; }
    #endregion

    #region Public Fields
    [Header("Game Over Settings")]
    public float gameOverDelay = 3f; // ゲームオーバーまでの遅延
    public GameObject gameOverUI; // ゲームオーバーUI
    
    [Header("Chain Collapse Settings")]
    public float chainCollapseInterval = 0.1f; // 連鎖崩落の間隔（短縮）
    
    [System.Serializable]
    public class DifficultyConfig
    {
        public int bridgeLength = 15;
        public int repairHits = 5;
        public float timeLimit = 60f;
        public float timingWindow = 0.5f; // Perfect判定の時間窓
        public int fragileCount = 4; // Fragile足場の数
    }
    
    [Header("Difficulty Settings")]
    public DifficultyConfig easyConfig = new DifficultyConfig { bridgeLength = 10, repairHits = 5, timeLimit = 30f, timingWindow = 0.8f, fragileCount = 3 };
    public DifficultyConfig normalConfig = new DifficultyConfig { bridgeLength = 15, repairHits = 7, timeLimit = 45f, timingWindow = 0.5f, fragileCount = 5 };
    public DifficultyConfig hardConfig = new DifficultyConfig { bridgeLength = 20, repairHits = 9, timeLimit = 60f, timingWindow = 0.3f, fragileCount = 7 };
    #endregion

    #region Private Fields
    private bool _isGameOver;
    private List<Platform> _allPlatforms = new List<Platform>();
    private BridgeGenerator _bridgeGenerator;
    
    // Game State
    public enum Difficulty { Easy, Normal, Hard }
    public enum GameState { Title, Tutorial, TutorialCountdown, Playing, Result }
    
    private Difficulty _currentDifficulty;
    private GameState _currentState;
    private DifficultyConfig _currentConfig;
    
    // Score System
    private float _gameStartTime;
    private int _totalRepairs;
    private float _totalAccuracy; // 正確度の累計を追加
    private float _totalTimingBonus;
    private int _currentCombo;
    private int _maxCombo;
    
    // Tutorial System
    private List<Platform> _tutorialPlatforms = new List<Platform>();
    private int _tutorialPlatformsCompleted;
    private const int TutorialPlatformCount = 4; // 通常3つ + 壊れた1つ
    private Difficulty _selectedDifficulty;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // シングルトンパターン
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // ゲーム状態を明示的にTitleに初期化
            _currentState = GameState.Title;
        }
        else
        {
            Destroy(gameObject);
        }
        Debug.Log(_currentState);
    }

    private void Start()
    {
        InitializePlatforms();
    }
    #endregion

    #region Initialization
    private void InitializePlatforms()
    {
        // BridgeGeneratorコンポーネントを取得
        _bridgeGenerator = GetComponent<BridgeGenerator>();
        
        if (_bridgeGenerator == null)
        {
            Debug.LogError("GameManager: BridgeGenerator component not found!");
            return;
        }
        
        // 橋生成は削除 - チュートリアル開始時のみ実行されるように変更
        
        // 動的生成された足場も含めて取得するため、少し遅延させる
        StartCoroutine(DelayedPlatformInitialization());
    }
    
    private IEnumerator DelayedPlatformInitialization()
    {
        // 1フレーム待機して、橋生成が完了するのを待つ
        yield return null;
        RefreshPlatformList();
    }
    
    // 足場リストを動的に更新するパブリックメソッド
    public void RefreshPlatformList()
    {
        _allPlatforms.Clear();
        Platform[] platforms = FindObjectsByType<Platform>(FindObjectsSortMode.None);
        _allPlatforms.AddRange(platforms);
    }
    #endregion

    #region Public Methods
    public void TriggerBridgeCollapse()
    {
        if (_isGameOver) return;

        _isGameOver = true;
        
        Debug.Log("TriggerBridgeCollapse: 橋崩落開始");
        
        // 橋崩落音を1秒遅延して再生
        StartCoroutine(PlayDelayedCollapseSound());
        
        StartCoroutine(ChainCollapseBridge());
    }

    // 橋崩落音を遅延再生するコルーチン
    private IEnumerator PlayDelayedCollapseSound()
    {
        // 1秒待機
        yield return new WaitForSeconds(1f);
        
        // 橋全体の崩落音を一回だけ再生
        if (AudioManager.Instance != null)
        {
            Debug.Log("AudioManager found, playing bridge collapse sound (after 1s delay)");
            AudioManager.Instance.PlayBridgeCollapseSfx();
        }
        else
        {
            Debug.LogError("AudioManager.Instance is null!");
        }
    }

    public void RestartGame()
    {
        Debug.Log("Restarting game");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
    
    // Game Control Methods
    public void StartGame(Difficulty difficulty)
    {
        _currentDifficulty = difficulty;
        // チュートリアル中でない場合のみPlayingに変更
        if (_currentState != GameState.Tutorial)
        {
            _currentState = GameState.Playing;
        }
        _currentConfig = GetDifficultyConfig(difficulty);
        
        InitializeGameStats();
        
        // UI初期表示を更新
        if (UIManager.Instance)
        {
            UIManager.Instance.UpdateCombo(_currentCombo);
            UIManager.Instance.UpdateRepairRate(0f);
        }
        
        // チュートリアル中はメインゲーム用の橋生成をスキップ
        if (_currentState != GameState.Tutorial)
        {
            SetupBridgeForDifficulty();
        }
        
        _gameStartTime = Time.time;
        StartCoroutine(GameTimer());
    }
    
    // 現在のゲーム状態を取得するメソッドを追加
    public GameState GetCurrentState()
    {
        return _currentState;
    }
    
    public void SetCurrentState(GameState state)
    {
        _currentState = state;
        
        // AudioManagerに状態変更を通知
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnGameStateChanged(state);
        }
    }
    
    // チュートリアルモードかどうかを判定するメソッドを追加
    public bool IsTutorialMode()
    {
        return _currentState == GameState.Tutorial;
    }
    
    public void OnRepairAttempt(TimingResult timing, float accuracy)
    {
        _totalRepairs++;
        _totalAccuracy += accuracy; // 正確度を累計に追加
        
        float bonus = 0f;
        string feedbackText = "";
        Color feedbackColor = Color.white;
        
        switch (timing)
        {
            case TimingResult.Perfect:
                bonus = 0.02f;
                feedbackText = "PERFECT!";
                feedbackColor = Color.green;
                _currentCombo++;
                break;
            case TimingResult.Good:
                bonus = 0.01f;
                feedbackText = "GOOD";
                feedbackColor = Color.yellow;
                _currentCombo++;
                break;
            case TimingResult.Bad:
                bonus = -0.01f;
                feedbackText = "BAD";
                feedbackColor = Color.red;
                // Badは成功としてカウントしないが、ゲーム続行は可能
                _currentCombo = 0;
                break;
            case TimingResult.Miss:
                bonus = -0.1f;
                feedbackText = "MISS";
                feedbackColor = Color.purple;
                _currentCombo = 0;
                
                // チュートリアル中（Tutorial または TutorialCountdown）のMissは橋崩落を引き起こさない
                if (_currentState != GameState.Tutorial && _currentState != GameState.TutorialCountdown)
                {
                    TriggerBridgeCollapse();
                }
                else
                {
                    // チュートリアル中のMissは警告のみ表示
                    if (UIManager.Instance)
                    {
                        UIManager.Instance.ShowTimingFeedback("MISS - 練習を続けてください", feedbackColor);
                    }
                    return; // 以降の処理をスキップ
                }
                break;
        }
        
        _totalTimingBonus += bonus;
        _maxCombo = Mathf.Max(_maxCombo, _currentCombo);
        
        // UI更新
        if (UIManager.Instance)
        {
            UIManager.Instance.ShowTimingFeedback(feedbackText, feedbackColor);
            UIManager.Instance.UpdateCombo(_currentCombo);
            UIManager.Instance.UpdateRepairRate(GetRepairRate());
        }
        
        // 修繕率チェックを無効化 - Bad以上は続行、Missのみ橋崩落の仕様に合わせる
        // if (GetRepairRate() < 50f && _totalRepairs > 1)
        // {
        //     EndGame(false);
        // }
    }
    
    public void OnBridgeComplete()
    {
        EndGame(true);
    }
    
    public enum TimingResult { Perfect, Good, Bad, Miss }
    
    // PlayerControllerから呼び出されるメソッド
    public DifficultyConfig GetCurrentDifficultyConfig()
    {
        return _currentConfig ?? normalConfig;
    }
    
    // 現在の難易度を取得するメソッドを追加
    public Difficulty GetCurrentDifficulty()
    {
        return _currentDifficulty;
    }
    
    // Tutorial System Methods
    public void StartTutorial(Difficulty difficulty)
    {
        _selectedDifficulty = difficulty;
        _currentDifficulty = difficulty; // この行を追加
        _currentConfig = GetDifficultyConfig(difficulty); // この行を追加して難易度設定を即座に反映
        SetCurrentState(GameState.Tutorial); // 直接設定ではなくSetCurrentStateを使用してAudioManagerに通知
        _tutorialPlatformsCompleted = 0;
        
        // チュートリアル用足場を自動生成
        if (_bridgeGenerator)
        {
            _bridgeGenerator.GenerateTutorialPlatforms();
        }
        else
        {
            Debug.LogError("GameManager: BridgeGenerator not found! Cannot generate tutorial platforms.");
        }
        
        SetupTutorialPlatforms();
        
        if (UIManager.Instance)
        {
            UIManager.Instance.ShowTutorialWithPlatforms();
        }
    }
    
    // チュートリアル中のfragile橋でmiss判定時に修繕を再開するメソッド
    public void RestartTutorialFragileRepair()
    {
        if (_currentState != GameState.Tutorial) return;
        
        // Z=4のfragile橋を探す
        Platform fragilePlatform = FindPlatformAtExactPosition(new Vector3(0, 0, 4));
        if (fragilePlatform && fragilePlatform.type == Platform.PlatformType.Fragile)
        {
            // fragile橋の状態をリセット
            fragilePlatform.ResetToRepairable();
            
            if (UIManager.Instance)
            {
                UIManager.Instance.UpdateTutorialMessage("MISS！もう一度修繕してください。");
            }
            
            Debug.Log("Tutorial fragile bridge repair restarted after miss");
        }
    }
    
    public void OnTutorialPlatformCompleted()
    {
        _tutorialPlatformsCompleted++;
        
        if (UIManager.Instance)
        {
            UIManager.Instance.UpdateTutorialProgress(_tutorialPlatformsCompleted, TutorialPlatformCount);
        }
        
        if (_tutorialPlatformsCompleted >= TutorialPlatformCount)
        {
            CompleteTutorial();
        }
    }
    #endregion

    #region Private Methods
    private IEnumerator ChainCollapseBridge()
    {
        Debug.Log("ChainCollapseBridge: 開始");
        
        // プレイヤーの移動を停止し、Rotation Constraintを解除
        var player = FindFirstObjectByType<PlayerController>();
        if (player)
        {
            player.SetCanMove(false);
            player.OnBridgeCollapse(); // 橋崩落時にRotation Constraintを解除
        }

        // 最初に一度だけ足場リストを更新
        RefreshPlatformList();
        
        // 崩落対象の足場をリストに保存（一度だけ検索）
        List<Platform> platformsToCollapse = new List<Platform>(_allPlatforms);
        
        int collapseCount = 0;
        foreach (Platform platform in platformsToCollapse)
        {
            if (platform && !platform.IsCollapsed())
            {
                collapseCount++;
                Debug.Log($"ChainCollapseBridge: 足場 {platform.name} を崩落開始 (#{collapseCount})");
                float startTime = Time.time;
                
                platform.TriggerCollapse();
                
                Debug.Log($"ChainCollapseBridge: 間隔待機開始 ({chainCollapseInterval}秒)");
                yield return new WaitForSeconds(chainCollapseInterval);
                
                float elapsed = Time.time - startTime;
                Debug.Log($"ChainCollapseBridge: 足場 {platform.name} 処理完了 (実際の経過時間: {elapsed:F3}秒)");
            }
        }
        
        // 最後に追加で生成された足場があるかチェック（fragileをキャッチした場合）
        Platform[] finalCheck = FindObjectsByType<Platform>(FindObjectsSortMode.None);
        foreach (Platform platform in finalCheck)
        {
            if (platform && !platform.IsCollapsed() && !platformsToCollapse.Contains(platform))
            {
                collapseCount++;
                Debug.Log($"ChainCollapseBridge: 追加足場 {platform.name} を崩落開始 (#{collapseCount})");
                platform.TriggerCollapse();
                yield return new WaitForSeconds(chainCollapseInterval);
            }
        }

        Debug.Log("ChainCollapseBridge: 全足場崩落完了");
        
        // ゲームオーバー処理
        yield return new WaitForSeconds(gameOverDelay);
        ShowGameOver();
    }

    private void ShowGameOver()
    {
        if (gameOverUI)
        {
            gameOverUI.SetActive(true);
        }
    }
    
    // Game Management Methods
    private DifficultyConfig GetDifficultyConfig(Difficulty difficulty)
    {
        switch (difficulty)
        {
            case Difficulty.Easy: return easyConfig;
            case Difficulty.Normal: return normalConfig;
            case Difficulty.Hard: return hardConfig;
            default: return normalConfig;
        }
    }
    
    private void InitializeGameStats()
    {
        _totalRepairs = 0;
        _totalAccuracy = 0f; // 正確度の初期化
        _totalTimingBonus = 0f; // 初期値を0に修正
        _currentCombo = 0;
        _maxCombo = 0;
        _isGameOver = false;
    }
    
    private void SetupBridgeForDifficulty()
    {
        if (_bridgeGenerator)
        {
            _bridgeGenerator.GenerateBridgeWithDifficulty(_currentConfig);
        }
    }
    
    private IEnumerator GameTimer()
    {
        while (_currentState == GameState.Playing && !_isGameOver)
        {
            float elapsedTime = Time.time - _gameStartTime;
            
            if (UIManager.Instance)
            {
                UIManager.Instance.UpdateTime(elapsedTime);
            }
            
            // 制限時間チェック
            if (elapsedTime >= _currentConfig.timeLimit)
            {
                EndGame(false);
                break;
            }
            
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    private void EndGame(bool success)
    {
        _currentState = GameState.Result;
        _isGameOver = true;
        
        float gameTime = Time.time - _gameStartTime;
        float repairRate = GetRepairRate();
        float timingBonus = 1f + _totalTimingBonus;
        int finalScore = CalculateScore(gameTime, repairRate, timingBonus);
        string rank = CalculateRank(finalScore, success);
        
        // 10位以内の判定
        bool isTop10 = false;
        if (RankingManager.Instance)
        {
            isTop10 = RankingManager.Instance.IsTop10Score(_currentDifficulty, finalScore);
        }
        
        // リザルト画面表示
        if (UIManager.Instance)
        {
            UIManager.Instance.ShowResult(gameTime, repairRate, timingBonus, finalScore, rank, isTop10, _currentDifficulty);
        }
    }
    
    // イニシャル入力後にランキングに追加するメソッド（UIManagerが直接処理するため非推奨）
    [System.Obsolete("このメソッドは使用されていません。UIManagerが直接RankingManagerにスコアを保存します。")]
    public void SaveScoreWithInitials(string initials)
    {
        // このメソッドは現在使用されていません
        // UIManagerが直接RankingManagerにスコアを保存するため
        Debug.LogWarning("GameManager.SaveScoreWithInitials is deprecated. UIManager handles score saving directly.");
    }
    
    private float GetRepairRate()
    {
        if (_totalRepairs == 0) return 100f;
        // 正確度の平均を修繕率として使用（より詳細な計算）
        return _totalAccuracy / _totalRepairs;
    }
    
    private int CalculateScore(float actualTime, float repairRate, float timingBonus)
    {
        // スコア = {1000 × (1 - 実時間 / 上限時間) × 修繕率} ^ タイミングボーナス
        float timeRatio = Mathf.Clamp01(1f - (actualTime / _currentConfig.timeLimit));
        float baseScore = 1000f * timeRatio * (repairRate / 100f);
        return Mathf.RoundToInt(Mathf.Pow(baseScore, timingBonus));
    }
    
    private string CalculateRank(int score, bool success)
    {
        if (!success) return "F";
        
        if (score >= 800) return "S";
        if (score >= 600) return "A";
        if (score >= 400) return "B";
        if (score >= 200) return "C";
        return "D";
    }
    
    private void SetupTutorialPlatforms()
    {
        _tutorialPlatforms.Clear();
        
        // Z座標1-4の位置にある足場をチュートリアル用として登録
        // z=1-3: 通常足場（3つ）
        // z=4: 壊れた足場（1つ）
        for (int z = 1; z <= 4; z++)
        {
            Vector3 platformPosition = new Vector3(0, 0, z);
            Platform platform = FindPlatformAtExactPosition(platformPosition);
            
            if (platform)
            {
                _tutorialPlatforms.Add(platform);
            }
            else
            {
                Debug.LogWarning($"Tutorial platform not found at Z={z}! Please manually place platforms at z=1-4.");
            }
        }
        
        // チュートリアル足場の配置確認
        if (_tutorialPlatforms.Count < TutorialPlatformCount)
        {
            Debug.LogError($"Not enough tutorial platforms! Expected {TutorialPlatformCount}, found {_tutorialPlatforms.Count}");
            Debug.LogError("Please manually place platforms at z=1-3 (Normal type) and z=4 (Fragile type)");
        }
    }
    
    private Platform FindPlatformAtExactPosition(Vector3 position)
    {
        Platform[] allPlatforms = FindObjectsByType<Platform>(FindObjectsSortMode.None);
        
        foreach (Platform platform in allPlatforms)
        {
            if (Vector3.Distance(platform.transform.position, position) < 0.1f)
            {
                return platform;
            }
        }
        
        return null;
    }
    
    private void CompleteTutorial()
    {
        // カウントダウン中の状態に変更
        SetCurrentState(GameState.TutorialCountdown); // 直接設定ではなくSetCurrentStateを使用
        
        if (UIManager.Instance)
        {
            UIManager.Instance.StartGameAfterTutorial(_selectedDifficulty);
        }
    }
    #endregion
}
