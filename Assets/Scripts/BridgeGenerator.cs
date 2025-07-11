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
    public Vector3 startPosition = new Vector3(0, 0, 5); // z=5から開始（チュートリアル用はz=1-4）
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
    
    // チュートリアル用足場を生成するメソッドを追加
    public void GenerateTutorialPlatforms()
    {
        // 既存の足場をクリア
        ClearExistingPlatforms();
        
        if (!ValidatePrefabs())
        {
            Debug.LogError("BridgeGenerator: Platform prefabs are not assigned!");
            return;
        }
        
        // チュートリアル用の足場を生成（4つ）
        // z=1-3: 通常足場（Normal）
        // z=4: 壊れた足場（Fragile）
        for (int i = 1; i <= 4; i++)
        {
            Vector3 position = new Vector3(0, 0, i);
            Platform.PlatformType type = (i <= 3) ? Platform.PlatformType.Normal : Platform.PlatformType.Fragile;
            Platform.RepairState state = Platform.RepairState.Broken;
            
            CreatePlatform(position, type, state);
        }
        
        // GameManagerの足場リストを更新
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RefreshPlatformList();
        }
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
    
    private void ClearExistingPlatforms()
    {
        ClearExistingBridge();
    }
    
    private bool ValidatePrefabs()
    {
        return normalPlatformPrefab != null && fragilePlatformPrefab != null;
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
            return floorParent;
        }
        
        // Floorオブジェクトが見つからない場合は新しく作成
        floorObject = new GameObject("Floor");
        floorParent = floorObject.transform;
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
    
    private GameObject FindPlatformAtPosition(Vector3 position)
    {
        // 指定された位置にあるプラットフォームを検索
        foreach (GameObject platform in _generatedPlatforms)
        {
            if (platform != null && platform.transform.position == position)
            {
                return platform;
            }
        }
        return null;
    }
    
    private void CreatePlatform(Vector3 position, Platform.PlatformType type, Platform.RepairState state)
    {
        GameObject prefab = (type == Platform.PlatformType.Normal) ? normalPlatformPrefab : fragilePlatformPrefab;
        
        if (prefab == null)
        {
            Debug.LogError("BridgeGenerator: Platform prefabs are not assigned!");
            return;
        }
        
        // プラットフォームを生成
        GameObject platformObj = Instantiate(prefab, position, prefab.transform.rotation);
        Platform platform = platformObj.GetComponent<Platform>();
        
        if (platform != null)
        {
            platform.type = type;
            platform.repairState = state;
            platform.isRepaired = false;
        }
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

    // チュートリアル足場のクリア機能を追加
    public void ClearTutorialPlatforms()
    {
        // チュートリアル用足場（z=1-4）のみを削除
        for (int i = _generatedPlatforms.Count - 1; i >= 0; i--)
        {
            GameObject platform = _generatedPlatforms[i];
            if (platform != null && platform.name.StartsWith("Tutorial_"))
            {
                DestroyImmediate(platform);
                _generatedPlatforms.RemoveAt(i);
            }
        }
    }

    public void GenerateBridgeWithDifficulty(GameManager.DifficultyConfig config)
    {
        // 既存の足場をクリア
        ClearExistingPlatforms();
        
        if (!ValidatePrefabs())
        {
            Debug.LogError("BridgeGenerator: Platform prefabs are not assigned!");
            return;
        }
        
        // 足場を生成
        for (int i = 1; i <= config.bridgeLength; i++)
        {
            Vector3 position = new Vector3(0, 0, i);
            
            // プラットフォームタイプをランダムに決定
            Platform.PlatformType type = Random.Range(0f, 1f) < 0.7f ? 
                Platform.PlatformType.Normal : Platform.PlatformType.Fragile;
            
            Platform.RepairState state = Platform.RepairState.Broken;
            
            CreatePlatform(position, type, state);
        }
        
        // GameManagerの足場リストを更新
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RefreshPlatformList();
        }
    }
}
