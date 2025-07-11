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
    #endregion

    #region Private Fields
    private GameManager.Difficulty _selectedDifficulty;
    private Coroutine _timingFeedbackCoroutine;
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
            FindButtonInPanel(titlePanel, "EasyButton")?.onClick.AddListener(() => StartGame(GameManager.Difficulty.Easy));
            FindButtonInPanel(titlePanel, "NormalButton")?.onClick.AddListener(() => StartGame(GameManager.Difficulty.Normal));
            FindButtonInPanel(titlePanel, "HardButton")?.onClick.AddListener(() => StartGame(GameManager.Difficulty.Hard));
            FindButtonInPanel(titlePanel, "RankingButton")?.onClick.AddListener(ShowRanking);
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
        // 直接ゲーム開始ではなく、チュートリアルを開始
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartTutorial(difficulty);
        }
    }

    private void RestartGame()
    {
        // リスタート時もチュートリアルから開始
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartTutorial(_selectedDifficulty);
        }
    }
    
    // 新しいチュートリアル表示メソッド
    public void ShowTutorialWithPlatforms()
    {
        HideAllPanels();
        tutorialPanel?.SetActive(true);
        
        var tutorialText = FindTextInPanel(tutorialPanel, "TutorialText");
        var progressText = FindTextInPanel(tutorialPanel, "ProgressText");
        
        tutorialText?.SetText("ハンマーを使って足場を修理してみましょう！\n通常の足場3つと壊れた足場1つを順番に叩いてください");
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
        
        // ゲーム開始
        ShowGameHUD();
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame(difficulty);
        }
    }

    private IEnumerator TutorialSequence()
    {
        var tutorialText = FindTextInPanel(tutorialPanel, "TutorialText");
        var countdownText = FindTextInPanel(tutorialPanel, "CountdownText");
        
        tutorialText?.SetText("ハンマーを数回叩いて操作を確認してください");
        countdownText?.SetText("");

        // 簡単な操作確認（3秒間）
        yield return new WaitForSeconds(3f);

        // カウントダウン
        tutorialText?.SetText("準備はいいですか？");
        
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
}
