using System.Collections;
using UnityEngine;

public class RepairTimingSystem : MonoBehaviour
{
    #region Public Fields
    [Header("Timing Settings")]
    public float timingWindowDuration = 2f; // タイミング入力の全体時間
    public GameObject timingIndicator; // タイミング表示UI
    public Transform perfectZone; // Perfect判定ゾーン
    public Transform goodZone; // Good判定ゾーン
    public Transform cursor; // 移動するカーソル
    #endregion

    #region Private Fields
    private bool _isTimingActive = false;
    private float _timingStartTime;
    private GameManager.DifficultyConfig _currentConfig;
    private System.Action<GameManager.TimingResult> _onTimingComplete;
    #endregion

    #region Public Methods
    public void StartTimingChallenge(GameManager.DifficultyConfig config, System.Action<GameManager.TimingResult> onComplete)
    {
        if (_isTimingActive) return;

        _currentConfig = config;
        _onTimingComplete = onComplete;
        
        StartCoroutine(TimingSequence());
    }
    
    public void OnTimingInput()
    {
        if (!_isTimingActive) return;

        float elapsedTime = Time.time - _timingStartTime;
        float normalizedTime = elapsedTime / timingWindowDuration;
        
        GameManager.TimingResult result = EvaluateTiming(normalizedTime);
        CompleteTiming(result);
    }
    #endregion

    #region Private Methods
    private IEnumerator TimingSequence()
    {
        _isTimingActive = true;
        _timingStartTime = Time.time;
        
        // UIを表示
        if (timingIndicator != null)
        {
            timingIndicator.SetActive(true);
        }
        
        // カーソルアニメーション
        StartCoroutine(AnimateCursor());
        
        // タイミング窓の終了を待つ
        yield return new WaitForSeconds(timingWindowDuration);
        
        // 時間切れの場合はMiss
        if (_isTimingActive)
        {
            CompleteTiming(GameManager.TimingResult.Miss);
        }
    }
    
    private IEnumerator AnimateCursor()
    {
        if (cursor == null) yield break;
        
        Vector3 startPos = new Vector3(-1f, 0, 0);
        Vector3 endPos = new Vector3(1f, 0, 0);
        
        float elapsed = 0f;
        
        while (elapsed < timingWindowDuration && _isTimingActive)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / timingWindowDuration;
            
            cursor.localPosition = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
    }
    
    private GameManager.TimingResult EvaluateTiming(float normalizedTime)
    {
        // Perfect判定: 中央付近の狭い範囲
        float perfectCenter = 0.5f;
        float perfectRange = _currentConfig.timingWindow * 0.5f;
        
        if (Mathf.Abs(normalizedTime - perfectCenter) <= perfectRange)
        {
            return GameManager.TimingResult.Perfect;
        }
        
        // Good判定: Perfectの外側
        float goodRange = _currentConfig.timingWindow;
        
        if (Mathf.Abs(normalizedTime - perfectCenter) <= goodRange)
        {
            return GameManager.TimingResult.Good;
        }
        
        // Bad判定: さらに外側だが有効範囲内
        float badRange = _currentConfig.timingWindow * 2f;
        
        if (Mathf.Abs(normalizedTime - perfectCenter) <= badRange)
        {
            return GameManager.TimingResult.Bad;
        }
        
        // それ以外はMiss
        return GameManager.TimingResult.Miss;
    }
    
    private void CompleteTiming(GameManager.TimingResult result)
    {
        _isTimingActive = false;
        
        // UIを非表示
        if (timingIndicator != null)
        {
            timingIndicator.SetActive(false);
        }
        
        // 結果をコールバック
        _onTimingComplete?.Invoke(result);
    }
    #endregion
}
