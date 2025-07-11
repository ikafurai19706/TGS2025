using System.Collections.Generic;
using UnityEngine;

public class BridgeGenerator : MonoBehaviour
{
    #region Public Fields
    [Header("Bridge Settings")]
    public GameObject normalPlatformPrefab;
    public GameObject fragilePlatformPrefab;
    public Transform floorParent; // Floorオブジェクトの参照
    
    [Header("Generation Settings")]
    public int bridgeLength = 15;
    public int minFragileInterval = 1;
    public int maxFragileInterval = 4;
    public Vector3 startPosition = new Vector3(0, 0, 1); // z=1から開始
    public float platformSpacing = 1f;
    
    [Header("Auto Generation")]
    public bool generateOnStart = false; // GameManagerから制御するためfalseに変更
    #endregion

    #region Private Fields
    private List<GameObject> _generatedPlatforms = new List<GameObject>();
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (generateOnStart)
        {
            GenerateBridge();
        }
    }
    #endregion

    #region Public Methods
    public void GenerateBridge()
    {
        ClearExistingBridge();
        
        if (normalPlatformPrefab == null || fragilePlatformPrefab == null)
        {
            Debug.LogError("BridgeGenerator: Platform prefabs are not assigned!");
            return;
        }
        
        GeneratePlatforms();
    }
    
    public void GenerateBridgeWithDifficulty(GameManager.DifficultyConfig config)
    {
        // 難易度設定を適用
        bridgeLength = config.bridgeLength;
        
        // 難易度に応じて壊れた足場の間隔を調整
        if (config == GameManager.Instance?.easyConfig)
        {
            minFragileInterval = 3;
            maxFragileInterval = 5;
        }
        else if (config == GameManager.Instance?.hardConfig)
        {
            minFragileInterval = 1;
            maxFragileInterval = 3;
        }
        else // Normal
        {
            minFragileInterval = 1;
            maxFragileInterval = 4;
        }
        
        GenerateBridge();
    }
    
    public void ClearBridge()
    {
        ClearExistingBridge();
    }
    #endregion

    #region Private Methods
    private void ClearExistingBridge()
    {
        foreach (GameObject platform in _generatedPlatforms)
        {
            if (platform != null)
            {
                DestroyImmediate(platform);
            }
        }
        _generatedPlatforms.Clear();
    }
    
    private void GeneratePlatforms()
    {
        // Floorオブジェクトを親として設定
        Transform parentTransform = GetFloorParent();
        
        List<int> fragilePositions = GenerateFragilePositions();
        
        for (int i = 0; i < bridgeLength; i++)
        {
            Vector3 position = startPosition + new Vector3(0, 0, i * platformSpacing);
            bool isFragile = fragilePositions.Contains(i);
            
            GameObject platformPrefab = isFragile ? fragilePlatformPrefab : normalPlatformPrefab;
            // Prefabの元の回転を保持
            GameObject platform = Instantiate(platformPrefab, position, platformPrefab.transform.rotation, parentTransform);
            
            // 名前を設定して識別しやすくする
            platform.name = $"{(isFragile ? "Fragile" : "Normal")}_Platform_{i:D2}";
            
            _generatedPlatforms.Add(platform);
        }
        
        // GameManagerに足場リストの更新を通知
        NotifyGameManager();
        
        Debug.Log($"Bridge generated with {bridgeLength} platforms under {parentTransform.name}. Fragile platforms at positions: {string.Join(", ", fragilePositions)}");
    }
    
    private Transform GetFloorParent()
    {
        // 明示的に指定されたFloorオブジェクトがあればそれを使用
        if (floorParent != null)
        {
            return floorParent;
        }
        
        // 指定されていない場合は"Floor"という名前のオブジェクトを検索
        GameObject floorObject = GameObject.Find("Floor");
        if (floorObject != null)
        {
            floorParent = floorObject.transform;
            Debug.Log($"Found Floor object: {floorObject.name}");
            return floorParent;
        }
        
        // Floorオブジェクトが見つからない場合は新しく作成
        floorObject = new GameObject("Floor");
        floorParent = floorObject.transform;
        Debug.Log("Created new Floor object as parent for platforms");
        return floorParent;
    }
    
    private void NotifyGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RefreshPlatformList();
        }
    }
    
    private List<int> GenerateFragilePositions()
    {
        List<int> fragilePositions = new List<int>();
        int currentPosition = 0;
        
        while (currentPosition < bridgeLength)
        {
            // 1~4個おきにランダムで壊れた足場を配置
            int interval = Random.Range(minFragileInterval, maxFragileInterval + 1);
            currentPosition += interval;
            
            if (currentPosition < bridgeLength)
            {
                fragilePositions.Add(currentPosition);
                currentPosition++; // 壊れた足場の次の位置から再開
            }
        }
        
        return fragilePositions;
    }
    #endregion

    #region Editor Methods
    #if UNITY_EDITOR
    [ContextMenu("Generate Bridge")]
    private void GenerateBridgeEditor()
    {
        GenerateBridge();
    }
    
    [ContextMenu("Clear Bridge")]
    private void ClearBridgeEditor()
    {
        ClearBridge();
    }
    #endif
    #endregion
}
