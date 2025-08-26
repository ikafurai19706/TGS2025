using System;
using System.IO.Ports;
using UnityEngine;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections;
using System.Linq;

public class SerialCommunication : MonoBehaviour
{
    [Header("Serial Settings")]
    public string portName = "COM3"; // Windowsの場合。macOSでは"/dev/tty.usbmodem..."など
    public int baudRate = 115200;
    public int readTimeout = 100;
    
    [Header("Device Detection Settings")]
    public int deviceDetectionTimeout = 1000; // デバイス判別のタイムアウト（ミリ秒）
    
    private SerialPort _serialPort;
    private Thread _readThread;
    private bool _isReading;
    private volatile bool _detectedSignalReceived;
    
    // デバイス判別用
    private string _expectedResponse = "";
    private readonly object _detectionLock = new object();
    private bool _isInitializing;
    
    public static event Action OnDetectedSignal;
    public static event Action<bool> OnInitializationComplete; // 初期化完了イベント（成功/失敗）
    
    private void Start()
    {
        InitializeSerial();
    }
    
    private void Update()
    {
        // メインスレッドでイベントを発行
        if (_detectedSignalReceived)
        {
            _detectedSignalReceived = false;
            OnDetectedSignal?.Invoke();
            Debug.Log("Serial: Detected signal received!");
        }
    }
    
    private void InitializeSerial()
    {
        if (_isInitializing)
        {
            Debug.LogWarning("Serial initialization already in progress");
            return;
        }
        
        _isInitializing = true;
        Debug.Log("Starting serial device detection...");
        
        // コルーチンで非同期初期化を開始
        StartCoroutine(InitializeSerialAsync());
    }
    
    private IEnumerator InitializeSerialAsync()
    {
        yield return new WaitForEndOfFrame();
        
        string[] availablePorts = SerialPort.GetPortNames();
        Debug.Log($"Available ports: {string.Join(", ", availablePorts)}");
        
        // macOSの場合のポートフィルタリング
        List<string> candidatePorts = new List<string>();
        
        if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
        {
            foreach (string port in availablePorts)
            {
                if (port.Contains("tty.usbmodem") || port.Contains("tty.usbserial"))
                {
                    candidatePorts.Add(port);
                }
            }
        }
        else
        {
            // Windows/Linuxの場合はすべてのポートを候補とする
            candidatePorts.AddRange(availablePorts);
        }
        
        if (candidatePorts.Count == 0)
        {
            Debug.LogError("No candidate serial ports found");
            _isInitializing = false;
            OnInitializationComplete?.Invoke(false);
            yield break;
        }
        
        // デバイス判別を非同期で実行
        yield return StartCoroutine(DetectTargetDeviceAsync(candidatePorts.ToArray()));
    }
    
    private IEnumerator DetectTargetDeviceAsync(string[] ports)
    {
        Debug.Log($"Starting parallel device detection on {ports.Length} ports...");
        
        // 各ポートのテストタスクを並行して開始
        var testTasks = new List<Task<(bool found, string port, Exception exception)>>();
        var cancellationTokenSource = new CancellationTokenSource();
        
        foreach (string port in ports)
        {
            Debug.Log($"Starting parallel test on port: {port}");
            
            // 1～999の乱数でチェック番号を生成
            int checkNumber = UnityEngine.Random.Range(1, 1000);
            
            // 各ポートのテストを並行して実行
            var task = Task.Run(() => {
                try
                {
                    // キャンセルされた場合は早期終了
                    if (cancellationTokenSource.Token.IsCancellationRequested)
                        return (false, port, null);
                    
                    bool result = TestDeviceOnPort(port, checkNumber);
                    return (result, port, null);
                }
                catch (Exception e)
                {
                    return (false, port, e);
                }
            }, cancellationTokenSource.Token);
            
            testTasks.Add(task);
        }
        
        // すべてのタスクが完了するか、デバイスが見つかるまで待機
        bool deviceFound = false;
        string detectedPort = null;
        
        while (testTasks.Count > 0 && !deviceFound)
        {
            // 完了したタスクを確認
            var completedTasks = testTasks.Where(t => t.IsCompleted).ToList();
            
            foreach (var task in completedTasks)
            {
                testTasks.Remove(task);
                
                try
                {
                    var (found, port, exception) = task.Result;
                    
                    if (exception != null)
                    {
                        Debug.LogError($"Error during device test on port {port}: {exception.Message}");
                    }
                    else if (found)
                    {
                        Debug.Log($"Target device detected on port: {port}");
                        deviceFound = true;
                        detectedPort = port;
                        
                        // 他のタスクをキャンセル
                        cancellationTokenSource.Cancel();
                        break;
                    }
                    else
                    {
                        Debug.Log($"No target device found on port: {port}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error processing task result: {e.Message}");
                }
            }
            
            if (!deviceFound)
            {
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        // 残りのタスクのキャンセルを確実にする
        cancellationTokenSource.Cancel();
        
        // 少し待機して、キャンセル処理が完了するのを待つ
        yield return new WaitForSeconds(0.5f);
        
        if (deviceFound)
        {
            portName = detectedPort;
            Debug.Log($"Device detection completed successfully on port: {portName}");
            
            // 検出されたポートで通常の接続を開始
            yield return StartCoroutine(EstablishConnectionAsync());
            _isInitializing = false;
            OnInitializationComplete?.Invoke(true);
        }
        else
        {
            Debug.LogError("Target device not detected on any port");
            _isInitializing = false;
            OnInitializationComplete?.Invoke(false);
        }
    }
    
    private bool TestDeviceOnPort(string port, int checkNumber = 1)
    {
        SerialPort testPort = null;
        
        try
        {
            testPort = new SerialPort(port, baudRate);
            testPort.ReadTimeout = 1000;
            testPort.WriteTimeout = 1000;
            testPort.Parity = Parity.None;
            testPort.DataBits = 8;
            testPort.StopBits = StopBits.One;
            testPort.Handshake = Handshake.None;
            testPort.DtrEnable = true;
            testPort.RtsEnable = true;
            
            testPort.Open();
            
            // Arduinoの場合、リセット後の安定化を待つ
            Thread.Sleep(2000);
            
            // バッファをクリア
            testPort.DiscardInBuffer();
            testPort.DiscardOutBuffer();
            
            // チェック番号を3桁でゼロ埋め
            string checkCommand = $"Check {checkNumber:000}";
            
            // 期待されるレスポンスを計算（元の数×42の下3桁）
            int expectedNumber = (checkNumber * 42) % 1000;
            _expectedResponse = $"Check {expectedNumber:000}";
            
            Debug.Log($"Sending: {checkCommand}");
            Debug.Log($"Expected response: {_expectedResponse}");
            
            lock (_detectionLock)
            {
                // デバイス判別状態をロックで管理（将来の拡張用）
            }
            
            // チェック信号を送信
            testPort.WriteLine(checkCommand);
            
            // レスポンスを待機
            long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime < deviceDetectionTimeout)
            {
                try
                {
                    if (testPort.BytesToRead > 0)
                    {
                        string response = testPort.ReadExisting();
                        Debug.Log($"Received from {port}: {response}");
                        
                        if (ProcessDetectionResponse(response))
                        {
                            return true;
                        }
                    }
                }
                catch (TimeoutException)
                {
                    // タイムアウトは正常
                }
                
                Thread.Sleep(50);
            }
            
            Debug.Log($"Device detection timeout on port: {port}");
            
        }
        catch (Exception e)
        {
            Debug.LogError($"Error testing port {port}: {e.Message}");
        }
        finally
        {
            if (testPort != null && testPort.IsOpen)
            {
                testPort.Close();
            }
        }
        
        return false;
    }
    
    private bool ProcessDetectionResponse(string data)
    {
        if (string.IsNullOrEmpty(data)) return false;
        
        string[] lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            Debug.Log($"Processing detection line: '{trimmedLine}'");
            
            if (trimmedLine == _expectedResponse)
            {
                Debug.Log($"Device detected! Received expected response: {trimmedLine}");
                return true;
            }
        }
        
        return false;
    }
    
    private IEnumerator EstablishConnectionAsync()
    {
        yield return new WaitForEndOfFrame();
        
        _serialPort = new SerialPort(portName, baudRate);
        _serialPort.ReadTimeout = readTimeout;
        _serialPort.WriteTimeout = 500;
        
        // シリアルポートの追加設定
        _serialPort.Parity = Parity.None;
        _serialPort.DataBits = 8;
        _serialPort.StopBits = StopBits.One;
        _serialPort.Handshake = Handshake.None;
        _serialPort.DtrEnable = true;
        _serialPort.RtsEnable = true;
        
        Debug.Log($"Opening serial port {portName} with settings: {baudRate} baud, {_serialPort.DataBits} data bits, {_serialPort.Parity} parity, {_serialPort.StopBits} stop bits");
        
        // ポートオープンを別スレッドで実行
        Exception openException = null;
        Task openTask = Task.Run(() => {
            try
            {
                _serialPort.Open();
            }
            catch (Exception e)
            {
                openException = e;
            }
        });
        
        while (!openTask.IsCompleted)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        if (openException != null)
        {
            Debug.LogError($"Failed to establish connection: {openException.Message}");
            _isInitializing = false;
            OnInitializationComplete?.Invoke(false);
            yield break;
        }
        
        // Arduinoの場合、リセット後の安定化を待つ（非同期）
        yield return new WaitForSeconds(2.0f);
        
        // バッファをクリア
        _serialPort.DiscardInBuffer();
        _serialPort.DiscardOutBuffer();
        
        // 接続確認のためのログ
        Debug.Log($"Serial port {portName} opened successfully. IsOpen: {_serialPort.IsOpen}");
        Debug.Log($"Port settings - BaudRate: {_serialPort.BaudRate}, DataBits: {_serialPort.DataBits}, Parity: {_serialPort.Parity}");
        
        _isReading = true;
        _readThread = new Thread(ReadSerialData);
        _readThread.Start();
        
        Debug.Log("Serial read thread started");
    }
    
    private void ReadSerialData()
    {
        Debug.Log("ReadSerialData thread started");
        
        while (_isReading && _serialPort != null && _serialPort.IsOpen)
        {
            try
            {
                // バッファに利用可能なバイト数をチェック
                int bytesToRead = _serialPort.BytesToRead;
                if (bytesToRead > 0)
                {
                    Debug.Log($"Bytes available to read: {bytesToRead}");
                }
                
                // ReadLineの代わりにReadExistingを試す
                string data = _serialPort.ReadExisting();
                
                if (!string.IsNullOrEmpty(data))
                {
                    // 受信したすべてのデータをDebug Logに出力
                    Debug.Log($"Serial Received (Raw): '{data}' (Length: {data.Length})");
                    
                    // 改行コードも含めて表示
                    Debug.Log($"Serial Received (Hex): {BitConverter.ToString(System.Text.Encoding.ASCII.GetBytes(data))}");
                    
                    // 行ごとに分割して処理
                    string[] lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        Debug.Log($"Serial Line: '{line}'");
                        
                        // 既存の"Detected"信号の処理も維持
                        if (line.Trim() == "Detected")
                        {
                            _detectedSignalReceived = true;
                        }
                    }
                }
                else
                {
                    // データがない場合は短時間待機
                    Thread.Sleep(10);
                }
            }
            catch (TimeoutException)
            {
                // タイムアウトは正常な動作
            }
            catch (Exception e)
            {
                Debug.LogError($"Serial read error: {e.Message}");
                Debug.LogError($"Exception type: {e.GetType().Name}");
                Debug.LogError($"Stack trace: {e.StackTrace}");
                break;
            }
        }
        
        Debug.Log("ReadSerialData thread ended");
    }
    
    private void OnDestroy()
    {
        CloseSerial();
    }
    
    private void OnApplicationQuit()
    {
        CloseSerial();
    }
    
    private void CloseSerial()
    {
        _isReading = false;
        
        if (_readThread != null && _readThread.IsAlive)
        {
            _readThread.Join(1000); // 1秒待機
        }
        
        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.Close();
            Debug.Log("Serial port closed");
        }
    }
    
    // エディター用：ポート一覧を取得
    public string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }
    
    // 手動で再初期化するメソッド
    public void RetryInitialization()
    {
        if (_isInitializing)
        {
            Debug.LogWarning("Initialization already in progress");
            return;
        }
        
        CloseSerial();
        InitializeSerial();
    }
    
    // 初期化状態を取得するメソッド
    public bool IsInitializing => _isInitializing;
}
