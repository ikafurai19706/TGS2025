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
    public GameObject countLeftPanel; // CountLeftPanelを独立したパネルとして追加
    
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
        countLeftPanel?.SetActive(false); // CountLeftPanelも非表示にする
        
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
        // 古いTutorialSequenceメソッドは削除されたため、この呼び出しを削除
        // 新しいチュートリアルシステムではGameManager.StartTutorial()が直接UIManager.ShowTutorialWithPlatforms()を呼び出す
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
        FindTextInPanel(resultPanel, "TimeText")?.SetText($"時間: {time:F1}秒");
        FindTextInPanel(resultPanel, "RepairRateText")?.SetText($"修繕率: {repairRate:F1}%");
        FindTextInPanel(resultPanel, "TimingBonusText")?.SetText($"タイミングボーナス: {timingBonus:F2}");
        FindTextInPanel(resultPanel, "ScoreText")?.SetText($"スコア: {score}");
        FindTextInPanel(resultPanel, "RankText")?.SetText($"ランク: {rank}");
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
        timeText?.SetText($"時間: {time:F1}秒");
    }

    public void UpdateRepairRate(float rate)
    {
        var repairRateText = FindTextInPanel(gameHUDPanel, "RepairRateText");
        repairRateText?.SetText($"修繕率: {rate:F1}%");
    }

    public void UpdateCombo(int combo)
    {
        var comboText = FindTextInPanel(gameHUDPanel, "ComboText");
        comboText?.SetText($"コンボ: {combo}");
    }

    public void UpdateCountLeft(int countLeft)
    {
        // CountLeftPanelから直接CountLeftTextを取得
        var countLeftText = FindTextInPanel(countLeftPanel, "CountLeftText");
        
        if (countLeft > 0)
        {
            countLeftText?.SetText($"{countLeft}");
            countLeftText?.gameObject.SetActive(true);
            countLeftPanel?.SetActive(true); // パネル全体を表示
        }
        else
        {
            countLeftText?.gameObject.SetActive(false);
            countLeftPanel?.SetActive(false); // パネル全体を非表示
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
        // 完全なリセットを実行
        CompleteReset();
        
        // タイトル画面に戻る
        ShowTitleScreen();
    }

    /// <summary>
    /// ゲームの完全なリセットを行う
    /// </summary>
    public void CompleteReset()
    {
        // 1. 実行中のコルーチンを停止
        StopAllCoroutines();
        
        // 2. タイミングUI関連のコルーチンを停止
        if (_timingFeedbackCoroutine != null)
        {
            StopCoroutine(_timingFeedbackCoroutine);
            _timingFeedbackCoroutine = null;
        }
        
        if (_timingAnimationCoroutine != null)
        {
            StopCoroutine(_timingAnimationCoroutine);
            _timingAnimationCoroutine = null;
        }
        
        // 3. タイミングUI状態をリセット
        _isTimingUIActive = false;
        
        // 4. プレイヤーを初期位置にリセット
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.ResetToInitialPosition();
        }
        
        // 5. 足場の完全リセット
        var bridgeGenerator = FindFirstObjectByType<BridgeGenerator>();
        if (bridgeGenerator != null)
        {
            bridgeGenerator.CompleteReset();
        }
        else
        {
            Debug.LogWarning("UIManager: BridgeGenerator not found - cannot reset platforms");
        }
        
        // 6. GameManagerの状態をリセット（利用可能なメソッドのみ使用）
        if (GameManager.Instance != null)
        {
            // GameManagerの状態を適切にリセット
            // 具体的なリセット処理はGameManagerで実装される予定
            Debug.Log("GameManager reset requested");
        }
        
        // 7. すべてのパネルを非表示
        HideAllPanels();
        
        // 8. UI要素の状態をリセット
        ResetUIElements();
        
        // 9. 選択された難易度をリセット
        _selectedDifficulty = GameManager.Difficulty.Easy;
        
        Debug.Log("UIManager: Complete reset performed");
    }

    /// <summary>
    /// UI要素の状態をリセット
    /// </summary>
    private void ResetUIElements()
    {
        // ゲームHUDのテキスト要素をリセット
        if (gameHUDPanel != null)
        {
            FindTextInPanel(gameHUDPanel, "TimeText")?.SetText("時間: 0.0秒");
            FindTextInPanel(gameHUDPanel, "RepairRateText")?.SetText("修繕率: 0.0%");
            FindTextInPanel(gameHUDPanel, "ComboText")?.SetText("コンボ: 0");
            
            // CountLeftPanelを経由してCountLeftTextにアクセス
            var countLeftText = FindTextInPanel(countLeftPanel, "CountLeftText");
            countLeftText?.SetText("0");
            
            // フィードバックテキストを非表示
            var feedbackText = FindTextInPanel(gameHUDPanel, "FeedbackText");
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }
            
            // カウントダウン表示を非表示
            if (countLeftText != null)
            {
                countLeftText.gameObject.SetActive(false);
            }
        }
        
        // リザルト画面のテキストをクリア
        if (resultPanel != null)
        {
            FindTextInPanel(resultPanel, "TimeText")?.SetText("時間: 0.0秒");
            FindTextInPanel(resultPanel, "RepairRateText")?.SetText("修繕率: 0.0%");
            FindTextInPanel(resultPanel, "TimingBonusText")?.SetText("タイミングボーナス: 0.00");
            FindTextInPanel(resultPanel, "ScoreText")?.SetText("スコア: 0");
            FindTextInPanel(resultPanel, "RankText")?.SetText("ランク: -");
        }
        
        // チュートリアル画面のテキストをクリア
        if (tutorialPanel != null)
        {
            FindTextInPanel(tutorialPanel, "TutorialText")?.SetText("");
            FindTextInPanel(tutorialPanel, "ProgressText")?.SetText("");
            FindTextInPanel(tutorialPanel, "CountdownText")?.SetText("");
        }
        
        // タイミングUIを非表示
        if (timingUIPanel != null)
        {
            timingUIPanel.SetActive(false);
        }
    }

    /// <summary>
    /// アプリケーション終了時の完全なクリーンアップ
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // アプリがバックグラウンドに移行した場合のクリーンアップ
            CompleteReset();
            ShowTitleScreen();
        }
    }

    /// <summary>
    /// アプリケーションフォーカス変更時の処理
    /// </summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            // フォーカスを失った場合のクリーンアップ
            CompleteReset();
            ShowTitleScreen();
        }
    }
    
    // 新しいチュートリアル表示メソッド
    public void ShowTutorialWithPlatforms()
    {
        HideAllPanels();
        tutorialPanel?.SetActive(true);
        
        var tutorialText = FindTextInPanel(tutorialPanel, "TutorialText");
        var progressText = FindTextInPanel(tutorialPanel, "ProgressText");
        
        tutorialText?.SetText("チュートリアル: 通常の足場を3つ修理し、その後壊れた足場を1つ修理してください。\n通常の足場はすぐに修理されますが、壊れた足場は落下するので受け止める必要があります！");
        progressText?.SetText("進捗: 0/4");
    }
    
    // チュートリアル進捗更新
    public void UpdateTutorialProgress(int completed, int total)
    {
        var progressText = FindTextInPanel(tutorialPanel, "ProgressText");
        progressText?.SetText($"進捗: {completed}/{total}");
        
        if (completed >= total)
        {
            var tutorialText = FindTextInPanel(tutorialPanel, "TutorialText");
            tutorialText?.SetText("チュートリアル完了！\n3秒後にゲームが開始されます");
        }
    }
    
    // チュートリアルメッセージを更新するメソッド
    public void UpdateTutorialMessage(string message)
    {
        var tutorialText = FindTextInPanel(tutorialPanel, "TutorialText");
        tutorialText?.SetText(message);
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
            progressText?.SetText($"ゲーム開始まで: {i}");
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
    
    /// <summary>
    /// TimingCursorの現在のx座標を取得
    /// </summary>
    /// <returns>TimingCursorのx座標（-500~500の範囲）</returns>
    public float GetCurrentCursorPosition()
    {
        if (_timingCursor == null) return 0f;
        
        RectTransform cursorRect = _timingCursor.GetComponent<RectTransform>();
        if (cursorRect == null) return 0f;
        
        return cursorRect.anchoredPosition.x;
    }
    
    /// <summary>
    /// TimingBarの現在の幅を取得
    /// </summary>
    /// <returns>TimingBarの幅</returns>
    public float GetTimingBarWidth()
    {
        if (_timingBar == null) return 1000f; // デフォルト値
        
        return _timingBar.rect.width;
    }
    #endregion
}
