using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    #region Singleton
    public static UIManager Instance { get; private set; }
    #endregion

    #region UI Panels - Only Panels
    [Header("UI Panels")]
    public GameObject titlePanel;
    public GameObject tutorialPanel;
    public GameObject gameHUDPanel;
    public GameObject resultPanel;
    public GameObject rankingPanel;
    
    [Header("Timing UI")]
    public GameObject timingUIPanel;
    #endregion

    #region Private Fields
    private GameManager.Difficulty _selectedDifficulty;
    private Coroutine _timingFeedbackCoroutine;
    
    // Timing UI Animation
    private Coroutine _timingAnimationCoroutine;
    private bool _isTimingUIActive;
    
    // Timing UI Elements (自動取得)
    private Transform _timingCursor;
    private RectTransform _timingBar;
    private RectTransform _perfectZone;
    private RectTransform _goodZone;
    private RectTransform _badZone;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
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
        InitializeUI();
        ShowTitleScreen();
    }
    #endregion

    #region Initialization
    private void InitializeUI()
    {
        // パネル内の要素を名前で検索してボタンイベントを設定
        SetupTitleButtons();
        SetupResultButtons();
        SetupRankingButtons();
        
        // Hide all panels initially
        HideAllPanels();
    }

    private void SetupTitleButtons()
    {
        if (titlePanel != null)
        {
            // ButtonsPanelの子としてボタンを検索
            Transform buttonsPanel = titlePanel.transform.Find("ButtonsPanel");
            if (buttonsPanel != null)
            {
                var easyButton = buttonsPanel.Find("EasyButton")?.GetComponent<Button>();
                var normalButton = buttonsPanel.Find("NormalButton")?.GetComponent<Button>();
                var hardButton = buttonsPanel.Find("HardButton")?.GetComponent<Button>();
                var rankingButton = buttonsPanel.Find("RankingButton")?.GetComponent<Button>();
                
                easyButton?.onClick.AddListener(() => StartGame(GameManager.Difficulty.Easy));
                normalButton?.onClick.AddListener(() => StartGame(GameManager.Difficulty.Normal));
                hardButton?.onClick.AddListener(() => StartGame(GameManager.Difficulty.Hard));
                rankingButton?.onClick.AddListener(ShowRanking);
            }
            else
            {
                Debug.LogError("UIManager: ButtonsPanel not found in titlePanel!");
            }
        }
        else
        {
            Debug.LogError("UIManager: titlePanel is null!");
        }
    }

    private void SetupResultButtons()
    {
        if (resultPanel != null)
        {
            FindButtonInPanel(resultPanel, "RestartButton")?.onClick.AddListener(RestartGame);
            FindButtonInPanel(resultPanel, "TitleReturnButton")?.onClick.AddListener(ShowTitleScreen);
        }
    }

    private void SetupRankingButtons()
    {
        if (rankingPanel != null)
        {
            FindButtonInPanel(rankingPanel, "BackToTitleButton")?.onClick.AddListener(ShowTitleScreen);
        }
    }

    private void HideAllPanels()
    {
        titlePanel?.SetActive(false);
        tutorialPanel?.SetActive(false);
        gameHUDPanel?.SetActive(false);
        resultPanel?.SetActive(false);
        rankingPanel?.SetActive(false);
        
        // TimingUIPanelは独立して管理（常に非表示でスタート）
        timingUIPanel?.SetActive(false);
    }
    #endregion

    #region Helper Methods
    private Button FindButtonInPanel(GameObject panel, string buttonName)
    {
        Transform buttonTransform = panel.transform.Find(buttonName);
        return buttonTransform?.GetComponent<Button>();
    }

    private TextMeshProUGUI FindTextInPanel(GameObject panel, string textName)
    {
        Transform textTransform = panel.transform.Find(textName);
        return textTransform?.GetComponent<TextMeshProUGUI>();
    }

    private GameObject FindObjectInPanel(GameObject panel, string objectName)
    {
        Transform objectTransform = panel.transform.Find(objectName);
        return objectTransform?.gameObject;
    }
    #endregion

    #region Public Methods - Screen Management
    public void ShowTitleScreen()
    {
        HideAllPanels();
        titlePanel?.SetActive(true);
    }

    public void ShowTutorial(GameManager.Difficulty difficulty)
    {
        HideAllPanels();
        tutorialPanel?.SetActive(true);
        _selectedDifficulty = difficulty;
        StartCoroutine(TutorialSequence());
    }

    public void ShowGameHUD()
    {
        HideAllPanels();
        gameHUDPanel?.SetActive(true);
    }

    public void ShowResult(float time, float repairRate, float timingBonus, int score, string rank)
    {
        HideAllPanels();
        resultPanel?.SetActive(true);

        // パネル内のテキストを名前で検索して更新
        FindTextInPanel(resultPanel, "TimeText")?.SetText($"Time: {time:F1}s");
        FindTextInPanel(resultPanel, "RepairRateText")?.SetText($"Repair Rate: {repairRate:F1}%");
        FindTextInPanel(resultPanel, "TimingBonusText")?.SetText($"Timing Bonus: {timingBonus:F2}");
        FindTextInPanel(resultPanel, "ScoreText")?.SetText($"Score: {score}");
        FindTextInPanel(resultPanel, "RankText")?.SetText($"Rank: {rank}");
    }

    public void ShowRanking()
    {
        HideAllPanels();
        rankingPanel?.SetActive(true);
        UpdateRankingDisplay();
    }
    #endregion

    #region Public Methods - Game HUD Updates
    public void UpdateTime(float time)
    {
        var timeText = FindTextInPanel(gameHUDPanel, "TimeText");
        timeText?.SetText($"Time: {time:F1}s");
    }

    public void UpdateRepairRate(float rate)
    {
        var repairRateText = FindTextInPanel(gameHUDPanel, "RepairRateText");
        repairRateText?.SetText($"Repair Rate: {rate:F1}%");
    }

    public void UpdateCombo(int combo)
    {
        var comboText = FindTextInPanel(gameHUDPanel, "ComboText");
        comboText?.SetText($"Combo: {combo}");
    }

    public void UpdateCountLeft(int countLeft)
    {
        var countLeftText = FindTextInPanel(gameHUDPanel, "CountLeftText");
        if (countLeft > 0)
        {
            countLeftText?.SetText($"{countLeft}");
            countLeftText?.gameObject.SetActive(true);
        }
        else
        {
            countLeftText?.gameObject.SetActive(false);
        }
    }

    public void ShowTimingFeedback(string feedback, Color color)
    {
        if (_timingFeedbackCoroutine != null)
        {
            StopCoroutine(_timingFeedbackCoroutine);
        }

        _timingFeedbackCoroutine = StartCoroutine(DisplayTimingFeedback(feedback, color));
    }
    #endregion

    #region Private Methods
    private void StartGame(GameManager.Difficulty difficulty)
    {
        _selectedDifficulty = difficulty;
        
        // GameManagerの存在確認
        if (GameManager.Instance == null)
        {
            Debug.LogError("UIManager: GameManager.Instance is null!");
            return;
        }
        
        // 直接ゲーム開始ではなく、チュートリアルを開始
        GameManager.Instance.StartTutorial(difficulty);
    }

    private void RestartGame()
    {
        // プレイヤーを初期位置にリセット
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.ResetToInitialPosition();
        }
        
        // タイトル画面に戻る
        ShowTitleScreen();
    }
    
    // 新しいチュートリアル表示メソッド
    public void ShowTutorialWithPlatforms()
    {
        HideAllPanels();
        tutorialPanel?.SetActive(true);
        
        var tutorialText = FindTextInPanel(tutorialPanel, "TutorialText");
        var progressText = FindTextInPanel(tutorialPanel, "ProgressText");
        
        tutorialText?.SetText("Tutorial: Hit 3 normal platforms, then 1 broken platform.\nNormal platforms are repaired instantly, broken ones drop and need to be caught!");
        progressText?.SetText("Progress: 0/4");
    }
    
    // チュートリアル進捗更新
    public void UpdateTutorialProgress(int completed, int total)
    {
        var progressText = FindTextInPanel(tutorialPanel, "ProgressText");
        progressText?.SetText($"Progress: {completed}/{total}");
        
        if (completed >= total)
        {
            var tutorialText = FindTextInPanel(tutorialPanel, "TutorialText");
            tutorialText?.SetText("Tutorial Complete!\nGame will start in 3 seconds");
        }
    }
    
    // チュートリアル完了後のゲーム開始
    public void StartGameAfterTutorial(GameManager.Difficulty difficulty)
    {
        StartCoroutine(PostTutorialCountdown(difficulty));
    }
    
    private IEnumerator PostTutorialCountdown(GameManager.Difficulty difficulty)
    {
        var tutorialText = FindTextInPanel(tutorialPanel, "TutorialText");
        var progressText = FindTextInPanel(tutorialPanel, "ProgressText");
        
        // カウントダウン表示
        for (int i = 3; i > 0; i--)
        {
            progressText?.SetText($"Game starts in: {i}");
            yield return new WaitForSeconds(1f);
        }
        
        progressText?.SetText("START!");
        yield return new WaitForSeconds(0.5f);
        
        // ゲーム開始（チュートリアル完了後なので通常のゲーム開始）
        ShowGameHUD();
        
        if (GameManager.Instance != null)
        {
            // カウントダウン完了後、Playingステートに変更してゲーム開始
            GameManager.Instance.StartGame(difficulty);
        }
    }

    private IEnumerator TutorialSequence()
    {
        var tutorialText = FindTextInPanel(tutorialPanel, "TutorialText");
        var countdownText = FindTextInPanel(tutorialPanel, "CountdownText");
        
        tutorialText?.SetText("Hit the hammer several times to check the controls");
        countdownText?.SetText("");

        // 簡単な操作確認（3秒間）
        yield return new WaitForSeconds(3f);

        // カウントダウン
        tutorialText?.SetText("Are you ready?");
        
        for (int i = 3; i > 0; i--)
        {
            countdownText?.SetText(i.ToString());
            yield return new WaitForSeconds(1f);
        }

        countdownText?.SetText("START!");
        yield return new WaitForSeconds(0.5f);

        // ゲーム開始
        ShowGameHUD();
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame(_selectedDifficulty);
        }
    }

    private IEnumerator DisplayTimingFeedback(string feedback, Color color)
    {
        var feedbackText = FindTextInPanel(gameHUDPanel, "FeedbackText");
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(true);
            feedbackText.text = feedback;
            feedbackText.color = color;
        }

        yield return new WaitForSeconds(1f);

        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }

    private void UpdateRankingDisplay()
    {
        if (RankingManager.Instance != null)
        {
            var rankingData = RankingManager.Instance.GetRankingData();
            
            if (rankingData != null)
            {
                UpdateRankingTexts("Easy", rankingData.easyScores);
                UpdateRankingTexts("Normal", rankingData.normalScores);
                UpdateRankingTexts("Hard", rankingData.hardScores);
            }
        }
    }

    private void UpdateRankingTexts(string difficulty, int[] scores)
    {
        for (int i = 0; i < 10 && i < scores.Length; i++)
        {
            var rankText = FindTextInPanel(rankingPanel, $"{difficulty}Rank{i + 1}");
            rankText?.SetText($"{i + 1}. {scores[i]}");
        }
    }
    #endregion

    #region Timing UI Management
    public void ShowTimingUI(float duration)
    {
        if (_isTimingUIActive) return;
        
        if (_timingCursor == null || _timingBar == null)
        {
            InitializeTimingUIElements();
        }
        
        _isTimingUIActive = true;
        
        // タイミングUIパネルを表示
        if (timingUIPanel != null)
        {
            timingUIPanel.SetActive(true);
        }
        
        // UI表示後、1フレーム待ってからサイズを更新
        StartCoroutine(DelayedUpdateAndAnimate(duration));
    }
    
    private IEnumerator DelayedUpdateAndAnimate(float duration)
    {
        // 1フレーム待機してUIが確実に更新されるのを待つ
        yield return null;
        
        // サイズを再計算
        UpdateZoneSizes();
        
        // アニメーション開始
        if (_timingCursor != null && _timingBar != null)
        {
            _timingAnimationCoroutine = StartCoroutine(AnimateTimingCursor(duration));
        }
    }
    
    public void HideTimingUI()
    {
        if (!_isTimingUIActive) return;
        
        _isTimingUIActive = false;
        
        // アニメーション停止
        if (_timingAnimationCoroutine != null)
        {
            StopCoroutine(_timingAnimationCoroutine);
            _timingAnimationCoroutine = null;
        }
        
        // タイミングUIパネルを非表示
        if (timingUIPanel != null)
        {
            timingUIPanel.SetActive(false);
        }
    }
    
    private void InitializeTimingUIElements()
    {
        if (timingUIPanel == null)
        {
            Debug.LogError("UIManager: TimingUIPanel is not assigned!");
            return;
        }
        
        // 子オブジェクトから各要素を取得
        _timingBar = timingUIPanel.transform.Find("TimingBar")?.GetComponent<RectTransform>();
        
        if (_timingBar != null)
        {
            _timingCursor = _timingBar.Find("TimingCursor");
            _perfectZone = _timingBar.Find("PerfectZone")?.GetComponent<RectTransform>();
            _goodZone = _timingBar.Find("GoodZone")?.GetComponent<RectTransform>();
            _badZone = _timingBar.Find("BadZone")?.GetComponent<RectTransform>();
            
            // ZoneをTimingBarのサイズに合わせて動的に調整
            UpdateZoneSizes();
        }
        
        // 必要な要素が見つからない場合の警告
        if (_timingBar == null)
        {
            Debug.LogError("UIManager: TimingBar not found! Please create a child object named 'TimingBar' in TimingUIPanel.");
        }
        if (_timingCursor == null)
        {
            Debug.LogError("UIManager: TimingCursor not found! Please create a child object named 'TimingCursor' in TimingBar.");
        }
        if (_perfectZone == null)
        {
            Debug.LogWarning("UIManager: PerfectZone not found! Please create a child object named 'PerfectZone' in TimingBar for visual feedback.");
        }
        if (_goodZone == null)
        {
            Debug.LogWarning("UIManager: GoodZone not found! Please create a child object named 'GoodZone' in TimingBar for visual feedback.");
        }
        if (_badZone == null)
        {
            Debug.LogWarning("UIManager: BadZone not found! Please create a child object named 'BadZone' in TimingBar for visual feedback.");
        }
    }
    
    private void UpdateZoneSizes()
    {
        if (_timingBar == null) return;
        
        // RectTransformの更新を強制的に実行
        Canvas.ForceUpdateCanvases();
        
        // TimingBarの現在の幅を取得
        float barWidth = _timingBar.rect.width;
        float barHeight = _timingBar.rect.height;
        
        // 難易度設定を取得（デフォルト値を使用）
        float timingWindow = 0.5f; // デフォルト値
        GameManager.Difficulty currentDifficulty = GameManager.Difficulty.Normal; // デフォルト値
        
        if (GameManager.Instance != null)
        {
            var config = GameManager.Instance.GetCurrentDifficultyConfig();
            if (config != null)
            {
                timingWindow = config.timingWindow;
            }
            currentDifficulty = GameManager.Instance.GetCurrentDifficulty();
        }
        else
        {
            Debug.LogWarning("UIManager: GameManager.Instance is null - using default difficulty");
        }
        
        // Perfect Zone のサイズを設定（難易度に応じて変更）
        if (_perfectZone != null)
        {
            float perfectRatio = 0.15f; // デフォルト値 (100-85)%
            
            switch (currentDifficulty)
            {
                case GameManager.Difficulty.Easy:
                    perfectRatio = 0.2f; // 20% (100-80)%
                    break;
                case GameManager.Difficulty.Normal:
                    perfectRatio = 0.15f; // 15% (100-85)%
                    break;
                case GameManager.Difficulty.Hard:
                    perfectRatio = 0.1f; // 10% (100-90)%
                    break;
            }
            
            float perfectWidth = barWidth * perfectRatio;
            _perfectZone.sizeDelta = new Vector2(perfectWidth, barHeight);
        }
        
        // Good Zone のサイズを設定（難易度に応じて変更）
        if (_goodZone != null)
        {
            float goodRatio = 0.325f; // デフォルト値 (100-67.5)%
            
            switch (currentDifficulty)
            {
                case GameManager.Difficulty.Easy:
                    goodRatio = 0.4f; // 40% (100-60)%
                    break;
                case GameManager.Difficulty.Normal:
                    goodRatio = 0.325f; // 32.5% (100-67.5)%
                    break;
                case GameManager.Difficulty.Hard:
                    goodRatio = 0.25f; // 25% (100-75)%
                    break;
            }
            
            float goodWidth = barWidth * goodRatio;
            _goodZone.sizeDelta = new Vector2(goodWidth, barHeight);
        }
        
        // Bad Zone のサイズを設定（難易度に応じて変更）
        if (_badZone != null)
        {
            float badZoneRatio = 0.5f; // デフォルト値
            
            switch (currentDifficulty)
            {
                case GameManager.Difficulty.Easy:
                    badZoneRatio = 0.6f; // 60%
                    break;
                case GameManager.Difficulty.Normal:
                    badZoneRatio = 0.5f; // 50%
                    break;
                case GameManager.Difficulty.Hard:
                    badZoneRatio = 0.4f; // 40%
                    break;
            }
            
            float badWidth = barWidth * badZoneRatio;
            _badZone.sizeDelta = new Vector2(badWidth, barHeight);
        }
    }
    
    // TimingBarのサイズが変更された場合に呼び出すためのパブリックメソッド
    public void RefreshTimingUILayout()
    {
        if (_timingBar != null)
        {
            UpdateZoneSizes();
        }
    }
    
    private IEnumerator AnimateTimingCursor(float duration)
    {
        if (_timingCursor == null || _timingBar == null) yield break;
        
        RectTransform cursorRect = _timingCursor.GetComponent<RectTransform>();
        if (cursorRect == null) yield break;
        
        // バーの幅を取得
        float barWidth = _timingBar.rect.width;
        float startX = -barWidth / 2f;
        float endX = barWidth / 2f;
        
        float elapsed = 0f;
        
        while (elapsed < duration && _isTimingUIActive)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // カーソルの位置を更新
            float currentX = Mathf.Lerp(startX, endX, t);
            cursorRect.anchoredPosition = new Vector2(currentX, cursorRect.anchoredPosition.y);
            
            yield return null;
        }
        
        // 完了時の位置設定
        if (_isTimingUIActive)
        {
            cursorRect.anchoredPosition = new Vector2(endX, cursorRect.anchoredPosition.y);
        }
    }
    
    private IEnumerator DelayedTimingSequence(float duration)
    {
        // 0.3秒待機
        yield return new WaitForSeconds(0.3f);
        
        // 実際のアニメーション開始
        StartCoroutine(AnimateTimingCursor(duration));
    }
    #endregion
}
