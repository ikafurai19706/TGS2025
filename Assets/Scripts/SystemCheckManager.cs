using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.InputSystem;

public class SystemCheckManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject checkItemPrefab; // チェック項目のプレハブ
    public Transform checkItemParent; // チェック項目の親オブジェクト（グリッドレイアウト）
    public Button startGameButton; // ゲーム開始ボタン
    public TextMeshProUGUI statusText; // 全体ステータステキスト
    
    [Header("Settings")]
    public string gameSceneName = "SampleScene"; // ゲームシーン名
    public float checkInterval = 0.5f; // チェック間隔（秒）
    
    private List<CheckItem> _checkItems = new List<CheckItem>();
    private bool _allChecksCompleted;
    private bool _allChecksPassed;
    
    // チェック項目の種類
    public enum CheckType
    {
        SerialConnection,
        InputSystem,
        Graphics,
        Audio,
        FileSystem,
        Network
    }
    
    // チェック項目の状態
    public enum CheckStatus
    {
        Waiting,    // 待機中
        Checking,   // チェック中
        Passed,     // 成功
        Failed,     // 失敗
        Warning     // 警告
    }
    
    [Serializable]
    public class CheckItem
    {
        public CheckType type;
        public string itemName;
        public string description;
        public CheckStatus status = CheckStatus.Waiting;
        public GameObject uiObject;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI statusText;
        public Image statusIcon;
        public string errorMessage = "";
    }
    
    private void Start()
    {
        InitializeCheckItems();
        SetupUI();
        StartCoroutine(RunSystemChecks());
    }
    
    private void InitializeCheckItems()
    {
        // チェック項目を定義
        var items = new[]
        {
            new CheckItem { type = CheckType.SerialConnection, itemName = "Serial Communication", description = "Device connection check" },
            new CheckItem { type = CheckType.InputSystem, itemName = "Input System", description = "Keyboard/Mouse detection" },
            new CheckItem { type = CheckType.Graphics, itemName = "Graphics", description = "GPU/Rendering system" },
            new CheckItem { type = CheckType.Audio, itemName = "Audio", description = "Audio system" },
            new CheckItem { type = CheckType.FileSystem, itemName = "File System", description = "Data file check" },
            new CheckItem { type = CheckType.Network, itemName = "Network", description = "Network environment check" }
        };
        
        _checkItems.AddRange(items);
    }
    
    private void SetupUI()
    {
        // 各チェック項目のUIを作成
        foreach (var item in _checkItems)
        {
            CreateCheckItemUI(item);
        }
        
        // ゲーム開始ボタンを無効化
        if (startGameButton != null)
        {
            startGameButton.interactable = false;
            startGameButton.onClick.AddListener(StartGame);
        }
        
        // 初期ステータステキスト
        if (statusText != null)
        {
            statusText.text = "Starting system check...";
        }
    }
    
    private void CreateCheckItemUI(CheckItem item)
    {
        if (checkItemPrefab == null || checkItemParent == null) return;
        
        // プレハブからUIオブジェクトを作成
        GameObject uiObj = Instantiate(checkItemPrefab, checkItemParent);
        item.uiObject = uiObj;
        
        // CheckItemUIコンポーネントを取得または追加
        CheckItemUI checkItemUI = uiObj.GetComponent<CheckItemUI>();
        if (checkItemUI == null)
        {
            checkItemUI = uiObj.AddComponent<CheckItemUI>();
        }
        
        // UI要素を取得（フォールバック用）
        item.nameText = uiObj.transform.Find("ItemName")?.GetComponent<TextMeshProUGUI>();
        item.statusText = uiObj.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
        item.statusIcon = uiObj.transform.Find("StatusIcon")?.GetComponent<Image>();
        
        // CheckItemUIクラスを使って初期設定
        checkItemUI.SetItemName(item.itemName);
        checkItemUI.SetStatus(item.status);
    }
    
    private void UpdateCheckItemUI(CheckItem item)
    {
        if (item.uiObject == null) return;
        
        // エラーメッセージがある場合はログに出力
        if (!string.IsNullOrEmpty(item.errorMessage) && item.status == CheckStatus.Failed)
        {
            Debug.LogError($"[{item.itemName}] {item.errorMessage}");
        }
        
        // CheckItemUIコンポーネントを使用して更新
        CheckItemUI checkItemUI = item.uiObject.GetComponent<CheckItemUI>();
        if (checkItemUI != null)
        {
            // Failedの場合はエラーメッセージを表示せず、ログのみに出力
            string statusMessage = (item.status == CheckStatus.Failed) ? "" : 
                                 (!string.IsNullOrEmpty(item.errorMessage) ? item.errorMessage : "");
            checkItemUI.SetStatus(item.status, statusMessage);
        }
        else
        {
            // フォールバック：直接UI要素を更新
            UpdateCheckItemUIFallback(item);
        }
    }
    
    private void UpdateCheckItemUIFallback(CheckItem item)
    {
        if (item.statusText != null)
        {
            switch (item.status)
            {
                case CheckStatus.Waiting:
                    item.statusText.text = "Waiting";
                    item.statusText.color = Color.gray;
                    break;
                case CheckStatus.Checking:
                    item.statusText.text = "Checking";
                    item.statusText.color = Color.yellow;
                    break;
                case CheckStatus.Passed:
                    item.statusText.text = "OK";
                    item.statusText.color = Color.green;
                    break;
                case CheckStatus.Failed:
                    item.statusText.text = "NG";
                    item.statusText.color = Color.red;
                    break;
                case CheckStatus.Warning:
                    item.statusText.text = "Warning";
                    item.statusText.color = Color.orange;
                    break;
            }
        }
        
        if (item.statusIcon != null)
        {
            switch (item.status)
            {
                case CheckStatus.Waiting:
                    item.statusIcon.color = Color.gray;
                    break;
                case CheckStatus.Checking:
                    item.statusIcon.color = Color.yellow;
                    break;
                case CheckStatus.Passed:
                    item.statusIcon.color = Color.green;
                    break;
                case CheckStatus.Failed:
                    item.statusIcon.color = Color.red;
                    break;
                case CheckStatus.Warning:
                    item.statusIcon.color = Color.orange;
                    break;
            }
        }
    }
    
    private IEnumerator RunSystemChecks()
    {
        yield return new WaitForSeconds(1f); // 初期待機
        
        foreach (var item in _checkItems)
        {
            yield return StartCoroutine(PerformCheck(item));
            yield return new WaitForSeconds(checkInterval);
        }
        
        // 全チェック完了
        _allChecksCompleted = true;
        _allChecksPassed = _checkItems.TrueForAll(item => item.status == CheckStatus.Passed || item.status == CheckStatus.Warning);
        
        UpdateFinalStatus();
    }
    
    private IEnumerator PerformCheck(CheckItem item)
    {
        item.status = CheckStatus.Checking;
        UpdateCheckItemUI(item);
        
        Debug.Log($"Checking: {item.itemName}");
        
        // チェック処理を実行
        Coroutine checkCoroutine = null;
        
        switch (item.type)
        {
            case CheckType.SerialConnection:
                checkCoroutine = StartCoroutine(CheckSerialConnection(item));
                break;
            case CheckType.InputSystem:
                checkCoroutine = StartCoroutine(CheckInputSystem(item));
                break;
            case CheckType.Graphics:
                checkCoroutine = StartCoroutine(CheckGraphics(item));
                break;
            case CheckType.Audio:
                checkCoroutine = StartCoroutine(CheckAudio(item));
                break;
            case CheckType.FileSystem:
                checkCoroutine = StartCoroutine(CheckFileSystem(item));
                break;
            case CheckType.Network:
                checkCoroutine = StartCoroutine(CheckNetwork(item));
                break;
        }
        
        if (checkCoroutine != null)
        {
            yield return checkCoroutine;
        }
        
        // チェック完了後にUIを更新
        UpdateCheckItemUI(item);
        
        Debug.Log($"Check completed: {item.itemName} - {item.status}");
    }
    
    private IEnumerator CheckSerialConnection(CheckItem item)
    {
        yield return new WaitForSeconds(0.5f);
        
        // SerialCommunicationのSingletonインスタンスを取得
        var serialComm = SerialCommunication.Instance;
        if (serialComm != null)
        {
            Debug.Log("SerialCommunication instance found, checking initialization...");
            
            // 初期化完了イベントを監視
            bool initializationCompleted = false;
            bool initializationSuccess = false;
            
            // イベントリスナーを追加
            Action<bool> onInitComplete = (success) => {
                initializationCompleted = true;
                initializationSuccess = success;
            };
            
            SerialCommunication.OnInitializationComplete += onInitComplete;
            
            // 既に初期化が完了している場合もチェック
            if (!serialComm.IsInitializing)
            {
                // 接続状態もチェック
                if (serialComm.IsConnected)
                {
                    initializationCompleted = true;
                    initializationSuccess = true;
                }
                else
                {
                    initializationCompleted = true;
                    initializationSuccess = false;
                }
            }
            
            // タイムアウト付きで初期化完了を待つ
            float timeout = 15f;
            float elapsed = 0f;
            
            while (!initializationCompleted && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
                
                // プログレス表示
                if (item.statusText != null)
                {
                    item.statusText.text = $"Searching devices... {elapsed:F1}s";
                }
            }
            
            // イベントリスナーを削除
            SerialCommunication.OnInitializationComplete -= onInitComplete;
            
            if (elapsed >= timeout)
            {
                item.errorMessage = "Serial communication initialization timeout (15 seconds)";
                item.status = CheckStatus.Failed;
            }
            else if (initializationSuccess)
            {
                Debug.Log("Serial communication initialized successfully");
                item.status = CheckStatus.Passed;
            }
            else
            {
                item.errorMessage = "Serial device not found";
                item.status = CheckStatus.Warning;
            }
        }
        else
        {
            item.status = CheckStatus.Warning;
            item.errorMessage = "Serial communication instance not found";
            Debug.Log("SerialCommunication instance not found");
        }
    }
    
    private IEnumerator CheckInputSystem(CheckItem item)
    {
        yield return new WaitForSeconds(0.3f);
        
        bool checkResult = PerformInputSystemCheck(item);
        item.status = checkResult ? CheckStatus.Passed : CheckStatus.Failed;
    }
    
    private bool PerformInputSystemCheck(CheckItem item)
    {
        try
        {
            // 新しいInput SystemのAPIを使用
            bool hasKeyboard = Keyboard.current != null;
            bool hasMouse = Mouse.current != null;
            
            if (hasKeyboard || hasMouse)
            {
                string deviceInfo = "";
                if (hasKeyboard) deviceInfo += "Keyboard ";
                if (hasMouse) deviceInfo += "Mouse ";
                Debug.Log($"Input devices detected: {deviceInfo.Trim()}");
                return true;
            }
            else
            {
                item.errorMessage = "Input devices not detected";
                return false;
            }
        }
        catch (Exception e)
        {
            item.errorMessage = $"Input system error: {e.Message}";
            return false;
        }
    }
    
    private IEnumerator CheckGraphics(CheckItem item)
    {
        yield return new WaitForSeconds(0.4f);
        
        bool checkResult = PerformGraphicsCheck(item);
        item.status = checkResult ? CheckStatus.Passed : CheckStatus.Failed;
    }
    
    private bool PerformGraphicsCheck(CheckItem item)
    {
        try
        {
            string deviceName = SystemInfo.graphicsDeviceName;
            int memory = SystemInfo.graphicsMemorySize;
            
            if (!string.IsNullOrEmpty(deviceName) && memory > 0)
            {
                Debug.Log($"Graphics: {deviceName}, VRAM: {memory}MB");
                return true;
            }
            else
            {
                item.errorMessage = "Could not get graphics device information";
                return false;
            }
        }
        catch (Exception e)
        {
            item.errorMessage = $"Graphics check error: {e.Message}";
            return false;
        }
    }
    
    private IEnumerator CheckAudio(CheckItem item)
    {
        yield return new WaitForSeconds(0.3f);
        
        bool checkResult = PerformAudioCheck(item);
        item.status = checkResult ? CheckStatus.Passed : CheckStatus.Failed;
    }
    
    private bool PerformAudioCheck(CheckItem item)
    {
        try
        {
            AudioConfiguration config = AudioSettings.GetConfiguration();
            
            if (config.sampleRate > 0)
            {
                Debug.Log($"Audio: Sample Rate {config.sampleRate}Hz");
                return true;
            }
            else
            {
                item.errorMessage = "Audio system not available";
                return false;
            }
        }
        catch (Exception e)
        {
            item.errorMessage = $"Audio check error: {e.Message}";
            return false;
        }
    }
    
    private IEnumerator CheckFileSystem(CheckItem item)
    {
        yield return new WaitForSeconds(0.2f);
        
        bool checkResult = PerformFileSystemCheck(item);
        item.status = checkResult ? CheckStatus.Passed : CheckStatus.Failed;
    }
    
    private bool PerformFileSystemCheck(CheckItem item)
    {
        try
        {
            string persistentPath = Application.persistentDataPath;
            bool persistentExists = System.IO.Directory.Exists(persistentPath);
            
            if (persistentExists)
            {
                Debug.Log($"Persistent Data Path: {persistentPath}");
                return true;
            }
            else
            {
                item.errorMessage = "File system access problem";
                return false;
            }
        }
        catch (Exception e)
        {
            item.errorMessage = $"File system error: {e.Message}";
            return false;
        }
    }
    
    private IEnumerator CheckNetwork(CheckItem item)
    {
        yield return new WaitForSeconds(0.6f);
        
        bool checkResult = PerformNetworkCheck(item);
        if (item.status != CheckStatus.Warning) // PerformNetworkCheckで警告が設定されない場合
        {
            item.status = checkResult ? CheckStatus.Passed : CheckStatus.Failed;
        }
    }
    
    private bool PerformNetworkCheck(CheckItem item)
    {
        try
        {
            NetworkReachability reachability = Application.internetReachability;
            
            if (reachability != NetworkReachability.NotReachable)
            {
                Debug.Log($"Network: {reachability}");
                return true;
            }
            else
            {
                item.status = CheckStatus.Warning;
                item.errorMessage = "No network connection (offline mode)";
                return true;
            }
        }
        catch (Exception e)
        {
            item.errorMessage = $"Network check error: {e.Message}";
            return false;
        }
    }
    
    private void UpdateFinalStatus()
    {
        if (statusText != null)
        {
            if (_allChecksPassed)
            {
                statusText.text = "All items checked. Ready to start the game.";
                Color greenColor = Color.green;
                Color.RGBToHSV(greenColor, out float h, out float s, out float v);
                greenColor = Color.HSVToRGB(h, s, v * 0.8f);
                statusText.color = greenColor;
            }
            else
            {
                statusText.text = "Some items have errors. Please check the details.";
                Color redColor = Color.red;
                Color.RGBToHSV(redColor, out float h, out float s, out float v);
                redColor = Color.HSVToRGB(h, s, v * 0.8f);
                statusText.color = redColor;
            }
        }
        
        // エラーがあってもゲーム開始は可能にする（開発用）
        if (startGameButton != null)
        {
            startGameButton.interactable = true;
        }
    }
    
    private void StartGame()
    {
        Debug.Log("Starting game...");
        SceneManager.LoadScene(gameSceneName);
    }
    
    // 外部からチェック結果を取得するメソッド
    public bool IsAllChecksCompleted()
    {
        return _allChecksCompleted;
    }
    
    public bool IsAllChecksPassed()
    {
        return _allChecksPassed;
    }
    
    public List<CheckItem> GetFailedChecks()
    {
        return _checkItems.FindAll(item => item.status == CheckStatus.Failed);
    }
}
