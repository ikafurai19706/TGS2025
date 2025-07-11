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
        public int minRepairHits = 3;
        public int maxRepairHits = 7;
        public float timeLimit = 60f;
        public float timingWindow = 0.5f; // Perfect判定の時間窓
    }
    
    [Header("Difficulty Settings")]
    public DifficultyConfig easyConfig = new DifficultyConfig { bridgeLength = 10, minRepairHits = 3, maxRepairHits = 5, timeLimit = 60f, timingWindow = 0.8f };
    public DifficultyConfig normalConfig = new DifficultyConfig { bridgeLength = 15, minRepairHits = 4, maxRepairHits = 7, timeLimit = 60f, timingWindow = 0.5f };
    public DifficultyConfig hardConfig = new DifficultyConfig { bridgeLength = 20, minRepairHits = 5, maxRepairHits = 9, timeLimit = 60f, timingWindow = 0.3f };
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
    private int _successfulRepairs;
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
        }
        else
        {
            Destroy(gameObject);
        }
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
        StartCoroutine(ChainCollapseBridge());
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
    
    // チュートリアルモードかどうかを判定するメソッドを追加
    public bool IsTutorialMode()
    {
        return _currentState == GameState.Tutorial;
    }
    
    public void OnRepairAttempt(TimingResult timing)
    {
        _totalRepairs++;
        
        float bonus = 0f;
        string feedbackText = "";
        Color feedbackColor = Color.white;
        
        switch (timing)
        {
            case TimingResult.Perfect:
                bonus = 0.02f;
                feedbackText = "PERFECT!";
                feedbackColor = Color.yellow;
                _successfulRepairs++;
                _currentCombo++;
                break;
            case TimingResult.Good:
                bonus = 0.01f;
                feedbackText = "GOOD";
                feedbackColor = Color.green;
                _successfulRepairs++;
                _currentCombo++;
                break;
            case TimingResult.Bad:
                bonus = -0.01f;
                feedbackText = "BAD";
                feedbackColor = Color.orange;
                // Badは失敗として扱う（_successfulRepairs++を削除）
                _currentCombo = 0;
                break;
            case TimingResult.Miss:
                bonus = -0.1f;
                feedbackText = "MISS";
                feedbackColor = Color.red;
                _currentCombo = 0;
                // Miss時は橋崩落を開始
                TriggerBridgeCollapse();
                break;
        }
        
        _totalTimingBonus += bonus;
        _maxCombo = Mathf.Max(_maxCombo, _currentCombo);
        
        // UI更新
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowTimingFeedback(feedbackText, feedbackColor);
            UIManager.Instance.UpdateCombo(_currentCombo);
            UIManager.Instance.UpdateRepairRate(GetRepairRate());
        }
        
        // 修繕率チェック
        if (GetRepairRate() < 50f && _totalRepairs > 1)
        {
            EndGame(false);
        }
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
        _currentState = GameState.Tutorial;
        _tutorialPlatformsCompleted = 0;
        
        // チュートリアル用足場を自動生成
        if (_bridgeGenerator != null)
        {
            _bridgeGenerator.GenerateTutorialPlatforms();
        }
        else
        {
            Debug.LogError("GameManager: BridgeGenerator not found! Cannot generate tutorial platforms.");
        }
        
        SetupTutorialPlatforms();
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowTutorialWithPlatforms();
        }
    }
    
    public void OnTutorialPlatformCompleted()
    {
        _tutorialPlatformsCompleted++;
        
        if (UIManager.Instance != null)
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
        // プレイヤーの移動を停止
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.SetCanMove(false);
        }

        // すべての足場を順次崩落
        foreach (Platform platform in _allPlatforms)
        {
            if (platform != null && !platform.IsCollapsed())
            {
                platform.TriggerCollapse();
                yield return new WaitForSeconds(chainCollapseInterval);
            }
        }

        // ゲームオーバー処理
        yield return new WaitForSeconds(gameOverDelay);
        ShowGameOver();
    }

    private void ShowGameOver()
    {
        if (gameOverUI != null)
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
        _successfulRepairs = 0;
        _totalTimingBonus = 1f; // 初期値1.0
        _currentCombo = 0;
        _maxCombo = 0;
        _isGameOver = false;
    }
    
    private void SetupBridgeForDifficulty()
    {
        if (_bridgeGenerator != null)
        {
            _bridgeGenerator.GenerateBridgeWithDifficulty(_currentConfig);
        }
    }
    
    private IEnumerator GameTimer()
    {
        while (_currentState == GameState.Playing && !_isGameOver)
        {
            float elapsedTime = Time.time - _gameStartTime;
            
            if (UIManager.Instance != null)
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
        
        // ランキングに記録
        if (RankingManager.Instance != null)
        {
            RankingManager.Instance.AddScore(_currentDifficulty, finalScore);
        }
        
        // リザルト画面表示
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowResult(gameTime, repairRate, timingBonus, finalScore, rank);
        }
    }
    
    private float GetRepairRate()
    {
        if (_totalRepairs == 0) return 100f;
        return (_successfulRepairs / (float)_totalRepairs) * 100f;
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
            
            if (platform != null)
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
        _currentState = GameState.TutorialCountdown;
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.StartGameAfterTutorial(_selectedDifficulty);
        }
    }
    #endregion
}
