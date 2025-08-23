using UnityEngine;
using System.Collections.Generic;

public class RankingManager : MonoBehaviour
{
    #region Singleton
    public static RankingManager Instance { get; private set; }
    #endregion

    #region Data Structure
    [System.Serializable]
    public class RankingData
    {
        public int[] easyScores = new int[10];
        public int[] normalScores = new int[10];
        public int[] hardScores = new int[10];
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
    public void AddScore(GameManager.Difficulty difficulty, int score)
    {
        int[] targetArray = GetScoresArray(difficulty);
        
        // 新しいスコアを追加
        List<int> scoreList = new List<int>(targetArray);
        scoreList.Add(score);
        
        // 降順でソート
        scoreList.Sort((a, b) => b.CompareTo(a));
        
        // 上位10位まで保持
        for (int i = 0; i < Mathf.Min(10, scoreList.Count); i++)
        {
            targetArray[i] = scoreList[i];
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
        int[] scores = GetScoresArray(difficulty);
        return scores.Length > 0 ? scores[0] : 0;
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
    private int[] GetScoresArray(GameManager.Difficulty difficulty)
    {
        switch (difficulty)
        {
            case GameManager.Difficulty.Easy: return _rankingData.easyScores;
            case GameManager.Difficulty.Normal: return _rankingData.normalScores;
            case GameManager.Difficulty.Hard: return _rankingData.hardScores;
            default: return _rankingData.normalScores;
        }
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
