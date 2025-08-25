using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

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
    public GameObject initialsInputPanel; // イニシャル入力パネルを追加
    public GameObject commonPanel; // CommonPanelを追加
    
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
    
    // Ranking UI State
    private GameManager.Difficulty _currentRankingTab = GameManager.Difficulty.Easy;
    
    // Password Dialog State
    private bool _isPasswordDialogActive = false;
    private string _currentPasswordInput = "";
    
    // Initials Input State
    private bool _isInitialsInputActive = false;
    private string _currentInitialsInput = "";
    private GameManager.Difficulty _pendingDifficulty;
    
    // Current game result data (for initials input)
    private int _currentScore = 0;
    private float _currentTime = 0f;
    private float _currentRepairRate = 0f;
    private float _currentTimingBonus = 0f;

    // Volume Panel State
    private bool _isVolumePanelActive = false;
    private Slider _bgmSlider;
    private Slider _sfxSlider;
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
        SetupCommonPanel();
        
        // Hide all panels initially
        HideAllPanels();
    }

    private void SetupTitleButtons()
    {
        if (titlePanel != null)
        {
            // RankingButtonはTitlePanel直下にある
            var rankingButton = titlePanel.transform.Find("RankingButton")?.GetComponent<Button>();
            rankingButton?.onClick.AddListener(ShowRanking);
            
            // DifficultyPanelの子として難易度ボタンを検索
            Transform difficultyPanel = titlePanel.transform.Find("DifficultyPanel");
            if (difficultyPanel != null)
            {
                var easyButton = difficultyPanel.Find("EasyButton")?.GetComponent<Button>();
                var normalButton = difficultyPanel.Find("NormalButton")?.GetComponent<Button>();
                var hardButton = difficultyPanel.Find("HardButton")?.GetComponent<Button>();
                
                easyButton?.onClick.AddListener(() => StartGame(GameManager.Difficulty.Easy));
                normalButton?.onClick.AddListener(() => StartGame(GameManager.Difficulty.Normal));
                hardButton?.onClick.AddListener(() => StartGame(GameManager.Difficulty.Hard));
            }
            else
            {
                Debug.LogError("UIManager: DifficultyPanel not found in titlePanel!");
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
            // BackButtonを設定 - パスワードダイアログも非アクティブにする
            FindButtonInPanel(rankingPanel, "BackButton")?.onClick.AddListener(OnBackButtonPressed);
            
            // ResetButtonを設定（RankingPanel直下）
            FindButtonInPanel(rankingPanel, "ResetButton")?.onClick.AddListener(ShowPasswordDialog);
            
            // タブボタンを設定
            Transform tabsPanel = rankingPanel.transform.Find("TabsPanel");
            if (tabsPanel != null)
            {
                var easyTabButton = tabsPanel.Find("EasyTabButton")?.GetComponent<Button>();
                var normalTabButton = tabsPanel.Find("NormalTabButton")?.GetComponent<Button>();
                var hardTabButton = tabsPanel.Find("HardTabButton")?.GetComponent<Button>();
                
                easyTabButton?.onClick.AddListener(() => SwitchRankingTab(GameManager.Difficulty.Easy));
                normalTabButton?.onClick.AddListener(() => SwitchRankingTab(GameManager.Difficulty.Normal));
                hardTabButton?.onClick.AddListener(() => SwitchRankingTab(GameManager.Difficulty.Hard));
            }
            else
            {
                Debug.LogError("UIManager: TabsPanel not found in rankingPanel!");
            }
        }
    }

    private void SetupCommonPanel()
    {
        if (commonPanel != null)
        {
            // VolumeButtonを設定
            Transform volumePanel = commonPanel.transform.Find("VolumePanel");
            if (volumePanel != null)
            {
                var volumeButton = volumePanel.Find("VolumeButton")?.GetComponent<Button>();
                volumeButton?.onClick.AddListener(ToggleVolumePanel);

                // BGMスライダーを設定（名前で直接検索）
                Transform bgmGroup = volumePanel.Find("BGM");
                if (bgmGroup != null)
                {
                    _bgmSlider = bgmGroup.Find("Slider")?.GetComponent<Slider>();
                    if (_bgmSlider != null)
                    {
                        _bgmSlider.minValue = 0f;
                        _bgmSlider.maxValue = 20f;
                        _bgmSlider.wholeNumbers = true; // 整数値のみ
                        // 既存のリスナーをクリアしてから追加
                        _bgmSlider.onValueChanged.RemoveAllListeners();
                        _bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
                        Debug.Log("UIManager: BGM Slider setup completed");
                    }
                    else
                    {
                        Debug.LogError("UIManager: BGM Slider not found in BGM group");
                    }
                }
                else
                {
                    Debug.LogError("UIManager: BGM group not found in VolumePanel");
                }

                // SFXスライダーを設定（名前で直接検索）
                Transform sfxGroup = volumePanel.Find("SFX");
                if (sfxGroup != null)
                {
                    _sfxSlider = sfxGroup.Find("Slider")?.GetComponent<Slider>();
                    if (_sfxSlider != null)
                    {
                        _sfxSlider.minValue = 0f;
                        _sfxSlider.maxValue = 20f;
                        _sfxSlider.wholeNumbers = true; // 整数値のみ
                        // 既存のリスナーをクリアしてから追加
                        _sfxSlider.onValueChanged.RemoveAllListeners();
                        _sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
                        Debug.Log("UIManager: SFX Slider setup completed");
                    }
                    else
                    {
                        Debug.LogError("UIManager: SFX Slider not found in SFX group");
                    }
                }
                else
                {
                    Debug.LogError("UIManager: SFX group not found in VolumePanel");
                }

                // 初期状態では音量調整パネルを非表示
                SetVolumePanelVisibility(false);
            }
            else
            {
                Debug.LogError("UIManager: VolumePanel not found in CommonPanel");
            }

            // CommonPanelを常に表示
            commonPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("UIManager: CommonPanel is not assigned");
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
        initialsInputPanel?.SetActive(false);
        
        // TimingUIPanelは独立して管理（常に非表示でスタート）
        timingUIPanel?.SetActive(false);
        
        // CommonPanelは常に表示状態を維持
        // commonPanel?.SetActive(false); // コメントアウト
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
        
        // ゲーム状態をTitleに変更
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetCurrentState(GameManager.GameState.Title);
        }
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
        
        // ゲーム状態をPlayingに変更
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetCurrentState(GameManager.GameState.Playing);
        }
    }

    public void ShowResult(float time, float repairRate, float timingBonus, int score, string rank, bool isTop10, GameManager.Difficulty difficulty)
    {
        // リザルト画面を表示
        HideAllPanels();
        resultPanel?.SetActive(true);

        // ゲーム状態をResultに変更
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetCurrentState(GameManager.GameState.Result);
        }

        // パネル内のテキストを名前で検索して更新
        FindTextInPanel(resultPanel, "TimeText")?.SetText($"時間: {time:F1}秒");
        FindTextInPanel(resultPanel, "RepairRateText")?.SetText($"修繕率: {repairRate:F1}%");
        FindTextInPanel(resultPanel, "TimingBonusText")?.SetText($"タイミングボーナス: {timingBonus:F2}");
        FindTextInPanel(resultPanel, "ScoreText")?.SetText($"スコア: {score}");
        FindTextInPanel(resultPanel, "RankText")?.SetText($"ランク: {rank}");
        
        // 10位以内の場合はイニシャル入力画面を表示
        if (isTop10)
        {
            ShowInitialsInput(difficulty);
        }
        else
        {
            // 10位以内でない場合のみ、ランキングに名前なしでスコアを保存
            if (RankingManager.Instance != null)
            {
                RankingManager.Instance.AddScore(difficulty, score, "");
            }
        }
        
        // 現在のスコア情報を記録
        _currentScore = score;
        _currentTime = time;
        _currentRepairRate = repairRate;
        _currentTimingBonus = timingBonus;
    }

    public void ShowRanking()
    {
        HideAllPanels();
        rankingPanel?.SetActive(true);
        
        // PasswordDialogPanelを必ず非表示にする
        HidePasswordDialog();
        
        // ゲーム状態をTitleに変更（ランキングもタイトル扱い）
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetCurrentState(GameManager.GameState.Title);
        }
        
        // デフォルトでEasyタブを選択
        _currentRankingTab = GameManager.Difficulty.Easy;
        SwitchRankingTab(_currentRankingTab);
    }
    
    // ランキングタブ切り替えメソッドを追加
    public void SwitchRankingTab(GameManager.Difficulty difficulty)
    {
        _currentRankingTab = difficulty;
        
        // すべての難易度パネルを非表示
        var easyPanel = FindObjectInPanel(rankingPanel, "EasyPanel");
        var normalPanel = FindObjectInPanel(rankingPanel, "NormalPanel");
        var hardPanel = FindObjectInPanel(rankingPanel, "HardPanel");
        
        easyPanel?.SetActive(false);
        normalPanel?.SetActive(false);
        hardPanel?.SetActive(false);
        
        // 選択された難易度のパネルのみ表示
        GameObject targetPanel = null;
        switch (difficulty)
        {
            case GameManager.Difficulty.Easy:
                targetPanel = easyPanel;
                break;
            case GameManager.Difficulty.Normal:
                targetPanel = normalPanel;
                break;
            case GameManager.Difficulty.Hard:
                targetPanel = hardPanel;
                break;
        }
        
        targetPanel?.SetActive(true);
        
        // ランキングデータを更新
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
            // ゲーム状態を明示的にTitleに初期化
            GameManager.Instance.SetCurrentState(GameManager.GameState.Title);
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

    /*
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
        // エディター内やDevelopment Buildでは、フォーカスが外れてもゲーム状態を維持
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        return;
        #endif

        // フォーカスを失った場合のリセット処理を削除
        // ゲーム状態を維持するため、何も実行しない
        if (!hasFocus)
        {
            // リリースビルドでのみフォーカスを失った場合のクリーンアップを実行
            CompleteReset();
            ShowTitleScreen();
        }
    }
    */
    
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
                // 現在選択されているタブのランキングのみ更新
                switch (_currentRankingTab)
                {
                    case GameManager.Difficulty.Easy:
                        UpdateRankingTextsForPanel("EasyPanel", rankingData.easyScores);
                        break;
                    case GameManager.Difficulty.Normal:
                        UpdateRankingTextsForPanel("NormalPanel", rankingData.normalScores);
                        break;
                    case GameManager.Difficulty.Hard:
                        UpdateRankingTextsForPanel("HardPanel", rankingData.hardScores);
                        break;
                }
            }
        }
    }

    private void UpdateRankingTextsForPanel(string panelName, System.Collections.Generic.List<RankingManager.RankingEntry> entries)
    {
        var panel = FindObjectInPanel(rankingPanel, panelName);
        if (panel == null) return;
        
        // Section1 (Rank1-5) を更新
        var section1 = FindObjectInPanel(panel, "Section1");
        if (section1 != null)
        {
            for (int i = 0; i < 5; i++)
            {
                var rankText = FindTextInPanel(section1, $"Rank{i + 1}");
                if (i < entries.Count && entries[i].score > 0)
                {
                    string initials = string.IsNullOrEmpty(entries[i].initials) ? "---" : entries[i].initials;
                    rankText?.SetText($"{i + 1}. {initials} {entries[i].score}");
                }
                else
                {
                    rankText?.SetText($"{i + 1}. --- ---");
                }
            }
        }
        
        // Section2 (Rank6-10) を更新
        var section2 = FindObjectInPanel(panel, "Section2");
        if (section2 != null)
        {
            for (int i = 5; i < 10; i++)
            {
                var rankText = FindTextInPanel(section2, $"Rank{i + 1}");
                if (i < entries.Count && entries[i].score > 0)
                {
                    string initials = string.IsNullOrEmpty(entries[i].initials) ? "---" : entries[i].initials;
                    rankText?.SetText($"{i + 1}. {initials} {entries[i].score}");
                }
                else
                {
                    rankText?.SetText($"{i + 1}. --- ---");
                }
            }
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

    private void Update()
    {
        // パスワードダイアログがアクティブな場合のEnterキー処理のみ
        if (_isPasswordDialogActive)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
            {
                ConfirmPasswordFromInputField();
            }
        }
        
        // イニシャル入力パネルがアクティブな場合の処理
        if (_isInitialsInputActive)
        {
            HandleInitialsInput();
            
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
            {
                ConfirmInitialsInput();
            }
        }
    }
    
    private void HandlePasswordInput()
    {
        // 新しいInput Systemを使用したキーボード入力処理
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        
        // Enterキーの処理
        if (keyboard.enterKey.wasPressedThisFrame)
        {
            ConfirmPassword();
            return;
        }
        
        // Backspaceキーの処理
        if (keyboard.backspaceKey.wasPressedThisFrame)
        {
            if (_currentPasswordInput.Length > 0)
            {
                _currentPasswordInput = _currentPasswordInput.Substring(0, _currentPasswordInput.Length - 1);
                Debug.Log($"Password after backspace: '{_currentPasswordInput}' (length: {_currentPasswordInput.Length})");
                UpdatePasswordDisplay();
            }
            return;
        }
        
        // 英数字キーの処理（最大10文字まで）
        if (_currentPasswordInput.Length < 10)
        {
            // 数字キー (0-9)
            for (int i = 0; i <= 9; i++)
            {
                var digitKey = (Key)((int)Key.Digit0 + i);
                if (keyboard[digitKey].wasPressedThisFrame)
                {
                    _currentPasswordInput += i.ToString();
                    Debug.Log($"Digit {i} pressed. Password: '{_currentPasswordInput}' (length: {_currentPasswordInput.Length})");
                    UpdatePasswordDisplay();
                    return;
                }
            }
            
            // アルファベットキー (a-z)
            for (int i = 0; i < 26; i++)
            {
                var letterKey = (Key)((int)Key.A + i);
                if (keyboard[letterKey].wasPressedThisFrame)
                {
                    char letter = (char)('a' + i);
                    _currentPasswordInput += letter;
                    Debug.Log($"Letter {letter} pressed. Password: '{_currentPasswordInput}' (length: {_currentPasswordInput.Length})");
                    UpdatePasswordDisplay();
                    return;
                }
            }
        }
    }
    
    private void UpdatePasswordDisplay()
    {
        Debug.Log($"UpdatePasswordDisplay called. Current password: '{_currentPasswordInput}' (length: {_currentPasswordInput.Length})");
        
        // パスワードダイアログパネル内のPasswordInputFieldテキストを更新
        var passwordDialogPanel = FindObjectInPanel(rankingPanel, "PasswordDialogPanel");
        if (passwordDialogPanel != null)
        {
            Debug.Log("PasswordDialogPanel found");
            var passwordInputField = passwordDialogPanel.transform.Find("PasswordInputField");
            if (passwordInputField != null)
            {
                Debug.Log("PasswordInputField found");
                // InputFieldコンポーネントを取得してテキストを直接設定
                var inputField = passwordInputField.GetComponent<TMPro.TMP_InputField>();
                if (inputField != null)
                {
                    // InputFieldのテキストを直接設定（アスタリスク表示）
                    string displayText = new string('*', _currentPasswordInput.Length);
                    Debug.Log($"Setting InputField text to: '{displayText}'");
                    inputField.text = displayText;
                    
                    // InputFieldの内部状態も強制的にリセット
                    inputField.SetTextWithoutNotify(displayText);
                    
                    // カーソル位置をテキストの末尾に設定
                    inputField.caretPosition = displayText.Length;
                    inputField.stringPosition = displayText.Length;
                    
                    Debug.Log($"InputField text after setting: '{inputField.text}'");
                }
                else
                {
                    Debug.Log("TMP_InputField component not found, trying TextMeshProUGUI");
                    // TMP_InputFieldが見つからない場合は、従来のTextコンポーネントを使用
                    var passwordText = FindTextInPanel(passwordDialogPanel, "PasswordInputField");
                    if (passwordText != null)
                    {
                        string displayText = new string('*', _currentPasswordInput.Length);
                        Debug.Log($"Setting TextMeshProUGUI text to: '{displayText}'");
                        // 入力中のパスワードを表示（セキュリティのためにアスタリスクに置き換え）
                        passwordText.SetText(displayText);
                    }
                    else
                    {
                        Debug.LogError("Neither TMP_InputField nor TextMeshProUGUI found for PasswordInputField");
                    }
                }
            }
            else
            {
                Debug.LogError("PasswordInputField not found in PasswordDialogPanel");
            }
        }
        else
        {
            Debug.LogError("PasswordDialogPanel not found");
        }
    }
    
    private void ConfirmPassword()
    {
        if (RankingManager.Instance != null)
        {
            bool success = RankingManager.Instance.ClearRankingWithPassword(_currentPasswordInput);
            
            if (success)
            {
                // パスワードが正しい場合、ランキングをリセット
                HidePasswordDialog();
                
                // ランキング表示を更新
                UpdateRankingDisplay();
                
                // 成功メッセージを表示
                StartCoroutine(ShowResetSuccessMessage());
            }
            else
            {
                // パスワードが間違っている場合
                StartCoroutine(ShowPasswordErrorMessage());
                
                // 入力フィールドを完全にクリア
                ResetPasswordInputField();
            }
        }
        else
        {
            Debug.LogError("UIManager: RankingManager.Instance is null!");
            HidePasswordDialog();
        }
    }
    
    private IEnumerator ShowResetSuccessMessage()
    {
        // パスワードダイアログパネル内の成功メッセージを表示
        var passwordDialogPanel = FindObjectInPanel(rankingPanel, "PasswordDialogPanel");
        if (passwordDialogPanel != null)
        {
            var messageText = FindTextInPanel(passwordDialogPanel, "MessageText");
            if (messageText != null)
            {
                messageText.SetText("ランキングをリセットしました");
                messageText.color = Color.green;
                messageText.gameObject.SetActive(true);
                
                yield return new WaitForSeconds(2f);
                
                messageText.gameObject.SetActive(false);
            }
        }
    }
    
    private IEnumerator ShowPasswordErrorMessage()
    {
        // パスワードダイアログパネル内のエラーメッセージを表示
        var passwordDialogPanel = FindObjectInPanel(rankingPanel, "PasswordDialogPanel");
        if (passwordDialogPanel != null)
        {
            var messageText = FindTextInPanel(passwordDialogPanel, "MessageText");
            if (messageText != null)
            {
                messageText.SetText("パスワードが間違っています");
                messageText.color = Color.red;
                messageText.gameObject.SetActive(true);
                
                yield return new WaitForSeconds(2f);
                
                messageText.gameObject.SetActive(false);
            }
        }
    }
    
    // パスワードダイアログを表示するメソッド
    public void ShowPasswordDialog()
    {
        Debug.Log("ShowPasswordDialog called");
        
        // すでに表示中であれば何もしない
        if (_isPasswordDialogActive) return;
        
        _isPasswordDialogActive = true;
        
        // ランキングパネルを表示
        if (rankingPanel != null)
        {
            rankingPanel.SetActive(true);
            
            // PasswordDialogPanelを有効化
            var passwordDialogPanel = FindObjectInPanel(rankingPanel, "PasswordDialogPanel");
            if (passwordDialogPanel != null)
            {
                passwordDialogPanel.SetActive(true);
            }
            else
            {
                Debug.LogError("UIManager: PasswordDialogPanel not found in rankingPanel!");
            }
            
            // パスワード入力フィールドの完全な初期化
            ResetPasswordInputField();
        }
    }
    
    // パスワードダイアログを非表示にするメソッド
    public void HidePasswordDialog()
    {
        Debug.Log("HidePasswordDialog called");
        
        _isPasswordDialogActive = false;
        
        // PasswordDialogPanelを無効化
        if (rankingPanel != null)
        {
            var passwordDialogPanel = FindObjectInPanel(rankingPanel, "PasswordDialogPanel");
            if (passwordDialogPanel != null)
            {
                passwordDialogPanel.SetActive(false);
            }
        }
    }
    
    // パスワード入力フィールドを完全にリセットするメソッド
    private void ResetPasswordInputField()
    {
        var passwordDialogPanel = FindObjectInPanel(rankingPanel, "PasswordDialogPanel");
        if (passwordDialogPanel != null)
        {
            var passwordInputField = passwordDialogPanel.transform.Find("PasswordInputField");
            if (passwordInputField != null)
            {
                // InputFieldコンポーネントを取得
                var inputField = passwordInputField.GetComponent<TMPro.TMP_InputField>();
                if (inputField != null)
                {
                    // InputFieldを完全にリセット
                    inputField.text = "";
                    inputField.SetTextWithoutNotify("");
                    
                    // カーソル位置をリセット
                    inputField.caretPosition = 0;
                    inputField.stringPosition = 0;
                    
                    // フォーカスを外す
                    inputField.DeactivateInputField();
                    
                    // 一度非アクティブにしてからアクティブにする（完全リセットのため）
                    inputField.gameObject.SetActive(false);
                    inputField.gameObject.SetActive(true);
                }
                else
                {
                    // TMP_InputFieldが見つからない場合は、Textコンポーネントをリセット
                    var passwordText = FindTextInPanel(passwordDialogPanel, "PasswordInputField");
                    if (passwordText != null)
                    {
                        passwordText.SetText("");
                    }
                }
            }
        }
        
        // 内部状態もリセット
        _currentPasswordInput = "";
    }
    
    // InputFieldから直接パスワードを取得して確認するメソッド
    private void ConfirmPasswordFromInputField()
    {
        Debug.Log("ConfirmPasswordFromInputField called");
        
        var passwordDialogPanel = FindObjectInPanel(rankingPanel, "PasswordDialogPanel");
        if (passwordDialogPanel != null)
        {
            var passwordInputField = passwordDialogPanel.transform.Find("PasswordInputField");
            if (passwordInputField != null)
            {
                var inputField = passwordInputField.GetComponent<TMPro.TMP_InputField>();
                if (inputField != null)
                {
                    // InputFieldから直接テキストを取得
                    string inputPassword = inputField.text;
                    Debug.Log($"Password from InputField: '{inputPassword}' (length: {inputPassword.Length})");
                    
                    if (RankingManager.Instance != null)
                    {
                        bool success = RankingManager.Instance.ClearRankingWithPassword(inputPassword);
                        
                        if (success)
                        {
                            // パスワードが正しい場合、ランキングをリセット
                            HidePasswordDialog();
                            
                            // ランキング表示を更新
                            UpdateRankingDisplay();
                            
                            // 成功メッセージを表示
                            StartCoroutine(ShowResetSuccessMessage());
                        }
                        else
                        {
                            // パスワードが間違っている場合
                            StartCoroutine(ShowPasswordErrorMessage());
                            
                            // 入力フィールドを完全にクリア
                            ResetPasswordInputField();
                        }
                    }
                    else
                    {
                        Debug.LogError("UIManager: RankingManager.Instance is null!");
                        HidePasswordDialog();
                    }
                }
                else
                {
                    Debug.LogError("TMP_InputField component not found for PasswordInputField");
                    HidePasswordDialog();
                }
            }
            else
            {
                Debug.LogError("PasswordInputField not found in PasswordDialogPanel");
                HidePasswordDialog();
            }
        }
        else
        {
            Debug.LogError("PasswordDialogPanel not found");
            HidePasswordDialog();
        }
    }
    
    // BackButton専用の処理メソッド
    private void OnBackButtonPressed()
    {
        Debug.Log("OnBackButtonPressed called");
        
        // パスワードダイアログがアクティブな場合は非アクティブにする
        if (_isPasswordDialogActive)
        {
            HidePasswordDialog();
        }
        
        // タイトル画面に戻る
        ShowTitleScreen();
    }

    #region Initials Input Management
    public void ShowInitialsInput(GameManager.Difficulty difficulty)
    {
        initialsInputPanel?.SetActive(true);
        _pendingDifficulty = difficulty;
        _isInitialsInputActive = true;
        
        // イニシャル入力パネルの初期化
        FindTextInPanel(initialsInputPanel, "InitialsText")?.SetText("___");
        FindTextInPanel(initialsInputPanel, "InstructionText")?.SetText("名前をアルファベットで入力してください");
        
        // 確定ボタンのイベント設定
        var confirmButton = FindButtonInPanel(initialsInputPanel, "ConfirmButton");
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(ConfirmInitialsInput);
        }
        
        // キャンセルボタンのイベント設定
        var cancelButton = FindButtonInPanel(initialsInputPanel, "CancelButton");
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => {
                // キャンセルの場合は名前なしでスコアを保存
                if (RankingManager.Instance != null)
                {
                    RankingManager.Instance.AddScore(_pendingDifficulty, _currentScore, "");
                }
                HideInitialsInputPanel();
            });
        }
    }
    
    public void ShowInitialsInputPanel(GameManager.Difficulty difficulty)
    {
        HideAllPanels();
        initialsInputPanel?.SetActive(true);
        _pendingDifficulty = difficulty;
        _isInitialsInputActive = true;
    }

    public void HideInitialsInputPanel()
    {
        initialsInputPanel?.SetActive(false);
        _isInitialsInputActive = false;
        _currentInitialsInput = "";
    }

    private void ConfirmInitialsInput()
    {
        // イニシャルの長さチェック（ここでは3文字固定と仮定）
        if (_currentInitialsInput.Length == 3)
        {
            // RankingManagerに直接スコアとイニシャルを保存（GameManager経由ではなく）
            if (RankingManager.Instance != null)
            {
                RankingManager.Instance.AddScore(_pendingDifficulty, _currentScore, _currentInitialsInput);
            }
            
            // パネルを隠す（タイトル画面には戻らない）
            HideInitialsInputPanel();
        }
        else
        {
            // エラーメッセージ表示
            FindTextInPanel(initialsInputPanel, "InstructionText")?.SetText("3文字で入力してください");
        }
    }

    private void HandleInitialsInput()
    {
        // 新しいInput Systemを使用したキーボード入力処理
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        
        // Backspaceキーの処理
        if (keyboard.backspaceKey.wasPressedThisFrame)
        {
            if (_currentInitialsInput.Length > 0)
            {
                _currentInitialsInput = _currentInitialsInput.Substring(0, _currentInitialsInput.Length - 1);
                UpdateInitialsDisplay();
            }
            return;
        }
        
        // 英字キーの処理（大文字に変換）
        for (char c = 'A'; c <= 'Z'; c++)
        {
            var key = (Key)System.Enum.Parse(typeof(Key), c.ToString());
            if (keyboard[key].wasPressedThisFrame)
            {
                // すでに3文字入力されている場合は無視
                if (_currentInitialsInput.Length >= 3) return;
                
                _currentInitialsInput += c;
                UpdateInitialsDisplay();
                return;
            }
        }
    }
    
    private void UpdateInitialsDisplay()
    {
        // イニシャル入力パネル内のInitialsTextを更新
        var initialsText = FindTextInPanel(initialsInputPanel, "InitialsText");
        initialsText?.SetText(_currentInitialsInput.PadRight(3, '_')); // 3文字まで_で埋める
    }
    
    private int GetCurrentScore()
    {
        // UIManagerで保持している現在のスコア情報を返す
        return _currentScore;
    }
    #endregion

    #region Volume Control Methods
    /// <summary>
    /// 音量調整パネルの表示/非表示を切り替え
    /// </summary>
    public void ToggleVolumePanel()
    {
        _isVolumePanelActive = !_isVolumePanelActive;
        SetVolumePanelVisibility(_isVolumePanelActive);
        
        if (_isVolumePanelActive)
        {
            // パネルが開かれた時、現在の音量値をスライダーに反映
            UpdateVolumeSliders();
        }
    }

    /// <summary>
    /// 音量調整パネルの表示状態を設定
    /// </summary>
    /// <param name="isVisible">表示するかどうか</param>
    private void SetVolumePanelVisibility(bool isVisible)
    {
        if (commonPanel != null)
        {
            Transform volumePanel = commonPanel.transform.Find("VolumePanel");
            if (volumePanel != null)
            {
                // BGMとSFXのスライダーグループの表示/非表示を制御
                Transform bgmGroup = volumePanel.Find("BGM");
                Transform[] children = new Transform[volumePanel.childCount];
                for (int i = 0; i < volumePanel.childCount; i++)
                {
                    children[i] = volumePanel.GetChild(i);
                }

                // BGMグループを表示/非表示
                if (bgmGroup != null)
                {
                    bgmGroup.gameObject.SetActive(isVisible);
                }

                // SFXグループを表示/非表示
                int bgmIndex = -1;
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i].name.Contains("BGM"))
                    {
                        bgmIndex = i;
                        break;
                    }
                }
                
                if (bgmIndex >= 0 && bgmIndex + 1 < children.Length)
                {
                    children[bgmIndex + 1].gameObject.SetActive(isVisible);
                }
            }
        }
    }

    /// <summary>
    /// 現在の音量値をスライダーに反映
    /// </summary>
    private void UpdateVolumeSliders()
    {
        if (AudioManager.Instance != null)
        {
            if (_bgmSlider != null)
            {
                // AudioManagerから0.0~1.0の値を取得し、0~20の範囲に変換
                _bgmSlider.value = AudioManager.Instance.GetBGMVolume() * 20f;
            }
            
            if (_sfxSlider != null)
            {
                // AudioManagerから0.0~1.0の値を取得し、0~20の範囲に変換
                _sfxSlider.value = AudioManager.Instance.GetSfxVolume() * 20f;
            }
        }
    }

    /// <summary>
    /// BGM音量スライダーの値が変更された時の処理
    /// </summary>
    /// <param name="value">新しい音量値（0~20）</param>
    private void OnBGMVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            // 0~20の値を0.0~1.0の範囲に変換してAudioManagerに渡す
            float normalizedValue = value / 20f;
            AudioManager.Instance.SetBGMVolume(normalizedValue);
        }
    }

    /// <summary>
    /// SFX音量スライダーの値が変更された時の処理
    /// </summary>
    /// <param name="value">新しい音量値（0~20）</param>
    private void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            // 0~20の値を0.0~1.0の範囲に変換してAudioManagerに渡す
            float normalizedValue = value / 20f;
            AudioManager.Instance.SetSfxVolume(normalizedValue);
        }
    }
    #endregion
}
