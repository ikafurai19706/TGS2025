using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class CheckItemUI : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI statusText;
    public Image background;
    
    [Header("Status Colors")]
    public Color waitingColor = Color.gray;
    public Color checkingColor = Color.yellow;
    public Color passedColor = Color.green;
    public Color failedColor = Color.red;
    public Color warningColor = Color.orange;
    
    private SystemCheckManager.CheckStatus _currentStatus;
    private Coroutine _animationCoroutine;
    private bool _shouldStopAnimation = false;
    
    private void Start()
    {
        // 自動的にUI要素を検索
        if (itemNameText == null)
            itemNameText = transform.Find("ItemName")?.GetComponent<TextMeshProUGUI>();
        
        if (statusText == null)
            statusText = transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
        
        if (background == null)
            background = transform.Find("Background")?.GetComponent<Image>();
    }
    
    public void SetItemName(string itemName)
    {
        if (itemNameText != null)
            itemNameText.text = itemName;
    }
    
    public void SetStatus(SystemCheckManager.CheckStatus status, string message = "")
    {
        _currentStatus = status;
        
        // チェック中以外の状態になった場合、アニメーションを停止
        if (status != SystemCheckManager.CheckStatus.Checking)
        {
            StopCheckingAnimation();
        }
        
        // ステータス更新
        Color statusColor = GetStatusColor(status);
        string statusMessage = GetStatusMessage(status, message);
        
        // Update status text
        if (statusText != null)
        {
            statusText.text = statusMessage;
            
            // statusTextの色のHSVのVを80%に設定
            Color.RGBToHSV(statusColor, out float h, out float s, out float v);
            Color adjustedStatusColor = Color.HSVToRGB(h, s, v * 0.8f);
            adjustedStatusColor.a = statusColor.a; // 元の透明度を保持
            
            statusText.color = adjustedStatusColor;
        }
        
        // Update background color (lighter version)
        if (background != null)
        {
            Color bgColor = statusColor;
            bgColor.a = 0.25f;
            background.color = bgColor;
        }
        
        // チェック中の場合のみアニメーション開始
        if (status == SystemCheckManager.CheckStatus.Checking)
        {
            StartCheckingAnimation();
        }
    }
    
    private Color GetStatusColor(SystemCheckManager.CheckStatus status)
    {
        switch (status)
        {
            case SystemCheckManager.CheckStatus.Waiting: return waitingColor;
            case SystemCheckManager.CheckStatus.Checking: return checkingColor;
            case SystemCheckManager.CheckStatus.Passed: return passedColor;
            case SystemCheckManager.CheckStatus.Failed: return failedColor;
            case SystemCheckManager.CheckStatus.Warning: return warningColor;
            default: return waitingColor;
        }
    }
    
    private string GetStatusMessage(SystemCheckManager.CheckStatus status, string customMessage = "")
    {
        if (!string.IsNullOrEmpty(customMessage))
            return customMessage;
        
        switch (status)
        {
            case SystemCheckManager.CheckStatus.Waiting: return "Waiting";
            case SystemCheckManager.CheckStatus.Checking: return "Checking...";
            case SystemCheckManager.CheckStatus.Passed: return "OK";
            case SystemCheckManager.CheckStatus.Failed: return "NG";
            case SystemCheckManager.CheckStatus.Warning: return "Warning";
            default: return "Unknown";
        }
    }
    
    private void StartCheckingAnimation()
    {
        // Stop existing animation first
        StopCheckingAnimation();
        
        // アニメーション停止フラグをリセット
        _shouldStopAnimation = false;
        
        // Start pulsing animation for checking status
        if (statusText != null)
        {
            _animationCoroutine = StartCoroutine(PulseAnimation());
        }
    }
    
    private void StopCheckingAnimation()
    {
        // アニメーション停止フラグを設定
        _shouldStopAnimation = true;
        
        // コルーチンを強制停止
        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;
        }
        
        // 透明度を1.0にリセット
        if (statusText != null)
        {
            Color color = statusText.color;
            color.a = 1f;
            statusText.color = color;
        }
    }
    
    private IEnumerator PulseAnimation()
    {
        while (!_shouldStopAnimation)
        {
            // Fade out
            yield return StartCoroutine(FadeText(1f, 0.3f, 0.5f));
            
            // 停止フラグをチェック
            if (_shouldStopAnimation) break;
            
            // Fade in
            yield return StartCoroutine(FadeText(0.3f, 1f, 0.5f));
        }
        
        // コルーチン参照をクリア
        _animationCoroutine = null;
    }
    
    private IEnumerator FadeText(float fromAlpha, float toAlpha, float duration)
    {
        if (statusText == null) yield break;
        
        float elapsed = 0f;
        
        while (elapsed < duration && !_shouldStopAnimation)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            Color currentColor = statusText.color;
            currentColor.a = Mathf.Lerp(fromAlpha, toAlpha, t);
            statusText.color = currentColor;
            
            yield return null;
        }
        
        // 停止フラグが設定されていない場合のみ最終値を設定
        if (!_shouldStopAnimation)
        {
            Color finalColor = statusText.color;
            finalColor.a = toAlpha;
            statusText.color = finalColor;
        }
    }
    
    private void OnDestroy()
    {
        // Stop animation
        _shouldStopAnimation = true;
    }
}
