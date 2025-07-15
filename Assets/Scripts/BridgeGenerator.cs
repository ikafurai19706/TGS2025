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
    
    /// <summary>
    /// シーン内のすべての足場を完全に削除（初期足場は除く）
    /// </summary>
    private void ClearExistingPlatforms()
    {
        // 生成済みリストから削除（初期足場以外）
        for (int i = _generatedPlatforms.Count - 1; i >= 0; i--)
        {
            GameObject platform = _generatedPlatforms[i];
            if (platform != null)
            {
                // 初期足場（位置が(0,0,0)）は削除しない
                if (platform.transform.position != Vector3.zero)
                {
                    DestroyImmediate(platform);
                    _generatedPlatforms.RemoveAt(i);
                }
            }
        }
        
        // シーン内のすべてのPlatformコンポーネントを持つオブジェクトを削除（初期足場は除く）
        Platform[] allPlatforms = FindObjectsOfType<Platform>();
        foreach (Platform platform in allPlatforms)
        {
            if (platform != null && platform.transform.position != Vector3.zero)
            {
                // 生成リストにない足場も削除対象にする
                if (!_generatedPlatforms.Contains(platform.gameObject))
                {
                    DestroyImmediate(platform.gameObject);
                }
            }
        }
        
        // floorParent配下の子オブジェクトを削除（初期足場は除く）
        if (floorParent != null)
        {
            for (int i = floorParent.childCount - 1; i >= 0; i--)
            {
                Transform child = floorParent.GetChild(i);
                // 初期足場（位置が(0,0,0)）は削除しない
                if (child.position != Vector3.zero)
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
        
        Debug.Log("BridgeGenerator: All platforms cleared from scene (except initial platform at origin)");
    }
    
    /// <summary>
    /// プレハブの有効性をチェック
    /// </summary>
    private bool ValidatePrefabs()
    {
        return normalPlatformPrefab != null && fragilePlatformPrefab != null;
    }
    
    /// <summary>
    /// 指定位置に足場を生成
    /// </summary>
    private void CreatePlatform(Vector3 position, Platform.PlatformType type, Platform.RepairState state)
    {
        GameObject prefab = (type == Platform.PlatformType.Normal) ? normalPlatformPrefab : fragilePlatformPrefab;
        
        // プレハブの元の回転を保持して生成
        GameObject platformObj = Instantiate(prefab, position, prefab.transform.rotation);
        
        // 親オブジェクトを設定
        if (floorParent != null)
        {
            platformObj.transform.SetParent(floorParent);
        }
        
        // Platform コンポーネントの設定
        Platform platform = platformObj.GetComponent<Platform>();
        if (platform != null)
        {
            platform.type = type;
            platform.repairState = state;
            platform.isRepaired = false;
        }
        
        // 生成リストに追加
        _generatedPlatforms.Add(platformObj);
        
        Debug.Log($"BridgeGenerator: Created {type} platform at {position} with original rotation");
    }
    
    /// <summary>
    /// 通常のゲーム用足場を生成
    /// </summary>
    private void GeneratePlatforms()
    {
        int fragileCount = 0;
        int nextFragileIndex = Random.Range(minFragileInterval, maxFragileInterval + 1);
        
        for (int i = 0; i < bridgeLength; i++)
        {
            Vector3 position = startPosition + new Vector3(0, 0, i * platformSpacing);
            
            // 壊れやすい足場を配置するかどうかを決定
            bool shouldBeFrangile = (i == nextFragileIndex) && (fragileCount < bridgeLength / 3);
            
            Platform.PlatformType type = shouldBeFrangile ? Platform.PlatformType.Fragile : Platform.PlatformType.Normal;
            Platform.RepairState state = Platform.RepairState.Broken;
            
            CreatePlatform(position, type, state);
            
            if (shouldBeFrangile)
            {
                fragileCount++;
                nextFragileIndex = i + Random.Range(minFragileInterval, maxFragileInterval + 1);
            }
        }
        
        // GameManagerの足場リストを更新
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RefreshPlatformList();
        }
    }
    
    /// <summary>
    /// 初期位置(0,0,0)に通常足場を生成
    /// </summary>
    public void GenerateInitialPlatform()
    {
        // 初期位置に通常足場を生成
        Vector3 initialPosition = Vector3.zero;
        CreatePlatform(initialPosition, Platform.PlatformType.Normal, Platform.RepairState.Completed);
        
        Debug.Log("BridgeGenerator: Initial platform created at (0,0,0)");
    }
    
    /// <summary>
    /// 完全リセット：生成された足場を削除し、初期足場は保持
    /// </summary>
    public void CompleteReset()
    {
        // 生成された足場を削除（初期足場は保持）
        ClearExistingPlatforms();
        
        // 初期足場が存在しない場合のみ生成
        bool hasInitialPlatform = false;
        Platform[] allPlatforms = FindObjectsOfType<Platform>();
        foreach (Platform platform in allPlatforms)
        {
            if (platform.transform.position == Vector3.zero)
            {
                hasInitialPlatform = true;
                break;
            }
        }
        
        if (!hasInitialPlatform)
        {
            GenerateInitialPlatform();
        }
        
        // GameManagerの足場リストを更新
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RefreshPlatformList();
        }
        
        Debug.Log("BridgeGenerator: Complete reset performed - generated platforms cleared, initial platform preserved");
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
        if (!ValidatePrefabs())
        {
            Debug.LogError("BridgeGenerator: Platform prefabs are not assigned!");
            return;
        }
        
        // 足場を生成
        for (int i = 0; i < config.bridgeLength; i++)
        {
            Vector3 position = startPosition + new Vector3(0, 0, i * platformSpacing);
            
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
