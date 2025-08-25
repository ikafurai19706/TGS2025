using System.Collections.Generic;
using System.Linq;
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
    public bool generateOnStart; // GameManagerから制御するためfalseに変更
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
        
        // GameManagerから難易度設定を取得して橋を生成
        if (GameManager.Instance)
        {
            GameManager.DifficultyConfig config = GameManager.Instance.GetCurrentDifficultyConfig();
            GenerateBridgeWithDifficulty(config);
        }
        else
        {
            // フォールバック：GameManagerがない場合はデフォルト設定で生成
            Debug.LogWarning("BridgeGenerator: GameManager not found, using default bridge length");
            GeneratePlatforms();
        }
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
        if (GameManager.Instance)
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
            if (platform)
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
            if (platform)
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
        Platform[] allPlatforms = FindObjectsByType<Platform>(FindObjectsSortMode.None);
        foreach (Platform platform in allPlatforms)
        {
            if (platform && platform.transform.position != Vector3.zero)
            {
                // 生成リストにない足場も削除対象にする
                if (!_generatedPlatforms.Contains(platform.gameObject))
                {
                    DestroyImmediate(platform.gameObject);
                }
            }
        }
        
        // floorParent配下の子オブジェクトを削除（初期足場は除く）
        if (floorParent)
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
        return normalPlatformPrefab && fragilePlatformPrefab;
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
        if (floorParent)
        {
            platformObj.transform.SetParent(floorParent);
        }
        
        // Platform コンポーネントの設定
        Platform platform = platformObj.GetComponent<Platform>();
        if (platform)
        {
            platform.type = type;
            platform.repairState = state;
            platform.isRepaired = false;
        }
        
        // 生成リストに追加
        _generatedPlatforms.Add(platformObj);
    }
    
    /// <summary>
    /// 通常のゲーム用足場を生成
    /// </summary>
    private void GeneratePlatforms()
    {
        // 橋長と同じ長さのリストを作成（0: Normal, 1: Fragile）
        List<int> platformTypes = new List<int>();
        
        // 初期化：すべてNormal（0）で埋める
        for (int i = 0; i < bridgeLength; i++)
        {
            platformTypes.Add(0);
        }
        
        // 難易度に応じたFragile足場の数を決定
        int fragileCount = GetFragileCountForDifficulty();
        
        // 橋長がFragile足場数より少ない場合の安全チェック
        if (fragileCount > bridgeLength)
        {
            Debug.LogWarning($"BridgeGenerator: Requested fragile count ({fragileCount}) exceeds bridge length ({bridgeLength}). Setting to bridge length.");
            fragileCount = bridgeLength;
        }
        
        // ランダムにFragile足場の位置を決定
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < bridgeLength; i++)
        {
            availableIndices.Add(i);
        }
        
        List<int> selectedFragilePositions = new List<int>(); // デバッグ用
        
        // 指定された数だけFragile足場を配置
        for (int i = 0; i < fragileCount && availableIndices.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, availableIndices.Count);
            int selectedPosition = availableIndices[randomIndex];
            platformTypes[selectedPosition] = 1; // Fragileに設定
            selectedFragilePositions.Add(selectedPosition); // デバッグ用記録
            availableIndices.RemoveAt(randomIndex); // 重複防止のため削除
        }
        
        // 実際に配置されたFragile足場の数を検証
        int actualFragileCount = platformTypes.Count(t => t == 1);

        // 足場を生成
        Debug.Log($"BridgeGenerator: Starting platform generation for {bridgeLength} platforms");
        for (int i = 0; i < bridgeLength; i++)
        {
            Vector3 position = startPosition + new Vector3(0, 0, i * platformSpacing);
            Platform.PlatformType type = (platformTypes[i] == 1) ? Platform.PlatformType.Fragile : Platform.PlatformType.Normal;
            Platform.RepairState state = Platform.RepairState.Broken;
            
            Debug.Log($"BridgeGenerator: Creating platform {i+1}/{bridgeLength} at position {position} - Type: {type}");
            CreatePlatform(position, type, state);
        }
        
        Debug.Log($"BridgeGenerator: Platform generation loop completed. Generated {_generatedPlatforms.Count} platforms total");
        
        // デバッグ情報を出力
        Debug.Log($"BridgeGenerator: Bridge generation completed!");
        Debug.Log($"  - Bridge Length: {bridgeLength}");
        Debug.Log($"  - Requested Fragile Count: {fragileCount}");
        Debug.Log($"  - Actual Fragile Count: {actualFragileCount}");
        Debug.Log($"  - Fragile Positions: [{string.Join(", ", selectedFragilePositions)}]");
        Debug.Log($"  - Generated Platforms Count: {_generatedPlatforms.Count}");
        
        // 最後の足場の情報を確認
        if (_generatedPlatforms.Count > 0)
        {
            GameObject lastPlatform = _generatedPlatforms[^1];
            Debug.Log($"  - Last Platform Position: {lastPlatform.transform.position}");
            Debug.Log($"  - Expected Last Position: {startPosition + new Vector3(0, 0, (bridgeLength - 1) * platformSpacing)}");
        }
        
        // 不一致がある場合は警告
        if (actualFragileCount != fragileCount)
        {
            Debug.LogError($"BridgeGenerator: Fragile count mismatch! Expected: {fragileCount}, Actual: {actualFragileCount}");
        }
        
        // GameManagerの足場リストを更新
        if (GameManager.Instance)
        {
            GameManager.Instance.RefreshPlatformList();
        }
    }
    
    /// <summary>
    /// 現在の難易度に応じてFragile足場の数を取得
    /// </summary>
    private int GetFragileCountForDifficulty()
    {
        if (GameManager.Instance)
        {
            GameManager.DifficultyConfig config = GameManager.Instance.GetCurrentDifficultyConfig();
            return config.fragileCount;
        }
        
        // GameManagerが利用できない場合のフォールバック
        return 4;
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
        Platform[] allPlatforms = FindObjectsByType<Platform>(FindObjectsSortMode.None);
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
        if (GameManager.Instance)
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
            if (platform && platform.name.StartsWith("Tutorial_"))
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
        
        // 難易度設定から橋の長さを取得
        int currentBridgeLength = config.bridgeLength;
        
        // 橋長と同じ長さのリストを作成（0: Normal, 1: Fragile）
        List<int> platformTypes = new List<int>();
        
        // 初期化：すべてNormal（0）で埋める
        for (int i = 0; i < currentBridgeLength; i++)
        {
            platformTypes.Add(0);
        }
        
        // 難易度に応じたFragile足場の数を決定
        int fragileCount = GetFragileCountForDifficulty();
        
        // 橋長がFragile足場数より少ない場合の安全チェック
        if (fragileCount > currentBridgeLength)
        {
            Debug.LogWarning($"BridgeGenerator: Requested fragile count ({fragileCount}) exceeds bridge length ({currentBridgeLength}). Setting to bridge length.");
            fragileCount = currentBridgeLength;
        }
        
        // ランダムにFragile足場の位置を決定
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < currentBridgeLength; i++)
        {
            availableIndices.Add(i);
        }
        
        // 指定された数だけFragile足場を配置
        for (int i = 0; i < fragileCount && availableIndices.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, availableIndices.Count);
            int selectedPosition = availableIndices[randomIndex];
            platformTypes[selectedPosition] = 1; // Fragileに設定
            availableIndices.RemoveAt(randomIndex); // 重複防止のため削除
        }
        
        // 足場を生成
        for (int i = 0; i < currentBridgeLength; i++)
        {
            Vector3 position = startPosition + new Vector3(0, 0, i * platformSpacing);
            Platform.PlatformType type = (platformTypes[i] == 1) ? Platform.PlatformType.Fragile : Platform.PlatformType.Normal;
            Platform.RepairState state = Platform.RepairState.Broken;
            
            CreatePlatform(position, type, state);
        }
        
        // GameManagerの足場リストを更新
        if (GameManager.Instance)
        {
            GameManager.Instance.RefreshPlatformList();
        }
    }
}
