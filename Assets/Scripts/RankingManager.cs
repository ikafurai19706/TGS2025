using UnityEngine;
using System.Collections.Generic;

public class RankingManager : MonoBehaviour
{
    #region Singleton
    public static RankingManager Instance { get; private set; }
    #endregion

    #region Data Structure
    [System.Serializable]
    public class RankingEntry
    {
        public string initials;
        public int score;
        
        public RankingEntry(string initials, int score)
        {
            this.initials = initials;
            this.score = score;
        }
    }
    
    [System.Serializable]
    public class RankingData
    {
        public List<RankingEntry> easyScores = new List<RankingEntry>();
        public List<RankingEntry> normalScores = new List<RankingEntry>();
        public List<RankingEntry> hardScores = new List<RankingEntry>();
    }
    #endregion

    #region Private Fields
    private RankingData _rankingData;
    private const string RankingKey = "BridgeGameRanking";
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadRankingData();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region Public Methods
    // 10位以内かどうかを判定する
    public bool IsTop10Score(GameManager.Difficulty difficulty, int score)
    {
        List<RankingEntry> targetList = GetScoresList(difficulty);
        
        // 10位未満の場合は必ずランクイン
        if (targetList.Count < 10)
            return true;
            
        // 10位のスコアと比較
        return score > targetList[9].score;
    }
    
    // イニシャルとスコアを追加
    public void AddScore(GameManager.Difficulty difficulty, int score, string initials = "")
    {
        List<RankingEntry> targetList = GetScoresList(difficulty);
        
        // 新しいエントリを追加
        targetList.Add(new RankingEntry(initials, score));
        
        // スコア順でソート（降順）
        targetList.Sort((a, b) => b.score.CompareTo(a.score));
        
        // 上位10位まで保持
        if (targetList.Count > 10)
        {
            targetList.RemoveRange(10, targetList.Count - 10);
        }
        
        // データを保存
        SaveRankingData();
    }

    public RankingData GetRankingData()
    {
        return _rankingData;
    }

    public int GetBestScore(GameManager.Difficulty difficulty)
    {
        List<RankingEntry> scores = GetScoresList(difficulty);
        return scores.Count > 0 ? scores[0].score : 0;
    }

    public void ClearRanking()
    {
        _rankingData = new RankingData();
        SaveRankingData();
    }
    
    // パスワード付きリセット機能を追加
    public bool ClearRankingWithPassword(string password)
    {
        const string correctPassword = "reset123"; // リセット用パスワード
        Debug.Log(password);
        if (password == correctPassword)
        {
            ClearRanking();
            return true;
        }
        
        return false;
    }
    #endregion

    #region Private Methods
    private List<RankingEntry> GetScoresList(GameManager.Difficulty difficulty)
    {
        switch (difficulty)
        {
            case GameManager.Difficulty.Easy: return _rankingData.easyScores;
            case GameManager.Difficulty.Normal: return _rankingData.normalScores;
            case GameManager.Difficulty.Hard: return _rankingData.hardScores;
            default: return _rankingData.normalScores;
        }
    }

    // 後方互換性のための変換メソッド
    private int[] GetScoresArray(GameManager.Difficulty difficulty)
    {
        List<RankingEntry> entries = GetScoresList(difficulty);
        int[] scores = new int[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            scores[i] = entries[i].score;
        }
        return scores;
    }

    private void LoadRankingData()
    {
        if (PlayerPrefs.HasKey(RankingKey))
        {
            string jsonData = PlayerPrefs.GetString(RankingKey);
            _rankingData = JsonUtility.FromJson<RankingData>(jsonData);
        }
        else
        {
            _rankingData = new RankingData();
        }
    }

    private void SaveRankingData()
    {
        string jsonData = JsonUtility.ToJson(_rankingData);
        PlayerPrefs.SetString(RankingKey, jsonData);
        PlayerPrefs.Save();
    }
    #endregion
}
