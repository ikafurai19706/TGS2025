using System;
using System.IO.Ports;
using UnityEngine;
using System.Threading;

public class SerialCommunication : MonoBehaviour
{
    [Header("Serial Settings")]
    public string portName = "COM3"; // Windowsの場合。macOSでは"/dev/tty.usbmodem..."など
    public int baudRate = 115200;
    public int readTimeout = 100;
    
    private SerialPort _serialPort;
    private Thread _readThread;
    private bool _isReading = false;
    private volatile bool _detectedSignalReceived = false;
    
    public static event Action OnDetectedSignal;
    
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
        try
        {
            // macOSの場合、ポート名を自動検出
            if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
            {
                string[] ports = SerialPort.GetPortNames();
                Debug.Log($"Available ports: {string.Join(", ", ports)}");
                
                foreach (string port in ports)
                {
                    if (port.Contains("tty.usbmodem") || port.Contains("tty.usbserial"))
                    {
                        portName = port;
                        Debug.Log($"Auto-detected serial port: {portName}");
                        break;
                    }
                }
            }
            
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
            
            _serialPort.Open();
            
            // Arduinoの場合、リセット後の安定化を待つ
            Thread.Sleep(2000);
            
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
        catch (Exception e)
        {
            Debug.LogError($"Failed to open serial port {portName}: {e.Message}");
            Debug.LogError($"Exception details: {e}");
        }
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
                    string[] lines = data.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
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
}
