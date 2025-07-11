using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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
    private const string RANKING_KEY = "BridgeGameRanking";
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
        int[] scores = GetScoresArray(difficulty);
        
        // 新しいスコアを追加してソート
        List<int> scoreList = scores.ToList();
        scoreList.Add(score);
        scoreList = scoreList.OrderByDescending(s => s).Take(10).ToList();
        
        // 配列に戻す
        for (int i = 0; i < scores.Length; i++)
        {
            scores[i] = i < scoreList.Count ? scoreList[i] : 0;
        }
        
        SaveRankingData();
        Debug.Log($"Added score {score} to {difficulty} ranking");
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
        if (PlayerPrefs.HasKey(RANKING_KEY))
        {
            string jsonData = PlayerPrefs.GetString(RANKING_KEY);
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
        PlayerPrefs.SetString(RANKING_KEY, jsonData);
        PlayerPrefs.Save();
    }
    #endregion
}
