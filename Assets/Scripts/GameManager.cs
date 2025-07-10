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
    #endregion

    #region Private Fields
    private bool _isGameOver = false;
    private List<Platform> _allPlatforms = new List<Platform>();
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
        // シーン内のすべての足場を取得
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
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        Application.Quit();
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
        
        Debug.Log("Game Over - Bridge Collapsed!");
    }
    #endregion
}
