using UnityEngine;
using System.Collections;

public class Platform : MonoBehaviour
{
    #region Enums
    public enum PlatformType { Normal, Fragile }
    public enum RepairState { Broken, Repairing, ReadyForReplacement, Completed, Collapsed }
    #endregion

    #region Public Fields
    [Header("Platform Settings")]
    public PlatformType type = PlatformType.Normal;
    public bool isRepaired;
    public RepairState repairState = RepairState.Broken;

    [Header("Fall Settings")]
    public GameObject newPlatformPrefab;
    public float fallSpeed = 1f;
    public float fallHeight = 5f;
    public float allowedHeightError = 0.5f;
    
    [Header("Collapse Settings")]
    public GameObject collapsedPrefab; // 崩落後のモデル
    public ParticleSystem collapseEffect; // 崩落エフェクト
    public AudioClip collapseSound; // 崩落音
    public float collapseDelay = 0.1f; // 崩落開始遅延（短縮）
    #endregion

    #region Private Fields
    private Renderer _renderer;
    private Collider _collider;
    private GameObject _fallingPlatform;
    private bool _isFalling;
    private bool _canCatch;
    private Vector3 _originalPosition;
    private Coroutine _fallCoroutine;
    private bool _fallAnimationStopped; // 落下アニメーション停止フラグを追加
    #endregion

    #region Constants
    private const float FallBelowDistance = 1f; // 叩ける範囲と同じ1メートル下まで落とす
    private const float CatchRangeStartRatio = 0.8f;
    private const float CatchRangeEndRatio = 1.2f;
    private const float FallCleanupDelay = 1f;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        InitializeComponents();
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
    }
    #endregion

    #region Public Methods - State Queries
    public bool IsUsable()
    {
        return type != PlatformType.Fragile || isRepaired;
    }

    public bool CanRepair()
    {
        return type == PlatformType.Fragile && 
               (repairState == RepairState.Broken || repairState == RepairState.Repairing);
    }

    public bool CanPlaceNewPlatform()
    {
        return type == PlatformType.Fragile && repairState == RepairState.ReadyForReplacement;
    }

    public bool IsFalling()
    {
        return _isFalling;
    }

    public bool CanCatchFallingPlatform()
    {
        return _isFalling && _canCatch;
    }
    #endregion

    #region Public Methods - Repair Actions
    public void StartRepair()
    {
        if (CanStartRepair())
        {
            repairState = RepairState.Repairing;
        }
    }

    public void Repair()
    {
        if (type == PlatformType.Normal)
        {
            // 通常足場の場合は修繕不要、叩いただけで進行
            // PlayerControllerで直接GameManagerを呼び出すため、ここでは何もしない
        }
        else if (CanCompleteRepair())
        {
            CompleteRepair();
        }
    }

    public bool TryCatchFallingPlatform()
    {
        if (!CanCatchPlatform()) return false;

        Vector3 stoppedPosition = _fallingPlatform.transform.position;
        
        // キャッチした時の判定値を計算
        float catchAccuracy = CalculateCatchAccuracy(stoppedPosition);
        string catchJudgment = EvaluateCatchJudgment(catchAccuracy);
        
        // 判定と判定値を出力
        Debug.Log($"Platform Catch - Position: {stoppedPosition.y:F2}, Accuracy: {catchAccuracy:F1}%, Judgment: {catchJudgment}");
        
        // 高さ制限を削除してTimingCursorと完全に同期
        return CatchPlatformAtPosition(stoppedPosition);
    }

    // 足場の落下を停止するメソッドを追加
    public void StopFalling()
    {
        if (_isFalling && _fallCoroutine != null)
        {
            // 落下アニメーション停止フラグを設定
            _fallAnimationStopped = true;
            
            StopCoroutine(_fallCoroutine);
            _fallCoroutine = null;
            
            // 現在の位置で足場を固定
            if (_fallingPlatform)
            {
                Vector3 currentPosition = _fallingPlatform.transform.position;
            }
        }
        else if (!_isFalling)
        {
            Debug.LogWarning($"Platform {name} - StopFalling called but _isFalling is false");
        }
        else if (_fallCoroutine == null)
        {
            Debug.LogWarning($"Platform {name} - StopFalling called but _fallCoroutine is null");
        }
    }

    // タイミングチャレンジ開始時に呼び出される新しいメソッド
    public void StartFalling()
    {
        if (_isFalling || repairState != RepairState.Repairing) return;
        
        // 足場の落下を開始
        repairState = RepairState.ReadyForReplacement;
        _isFalling = true;
        _canCatch = false; // 最初はキャッチできない
        
        // 元の足場を非表示にする（重要！）
        SetPlatformVisible(false);
        
        // 落下する足場を生成
        if (newPlatformPrefab)
        {
            _originalPosition = transform.position;
            // 落下開始位置を上に設定（fallHeightの高さから開始）
            Vector3 startPosition = _originalPosition + Vector3.up * fallHeight;
            _fallingPlatform = Instantiate(newPlatformPrefab, startPosition, transform.rotation);
            
            // 落下アニメーションを開始
            if (_fallCoroutine != null)
            {
                StopCoroutine(_fallCoroutine);
            }
            _fallCoroutine = StartCoroutine(FallWithTimingBar());
        }
    }

    public void MissedCatch()
    {
        if (_isFalling && _fallingPlatform)
        {
            _canCatch = false;
            
            // チュートリアル中は橋崩落を発生させない
            if (GameManager.Instance != null && !GameManager.Instance.IsTutorialMode())
            {
                // 通常ゲーム中のみ橋全体の崩落を開始
                GameManager.Instance.TriggerBridgeCollapse();
            }
            else if (GameManager.Instance != null && GameManager.Instance.IsTutorialMode())
            {
                // チュートリアル中はログのみ出力
                Debug.Log("Tutorial mode: Bridge collapse prevented on missed catch");
            }
        }
    }

    // 崩落処理を公開メソッドとして追加
    public void TriggerCollapse()
    {
        if (repairState != RepairState.Collapsed)
        {
            StartCoroutine(CollapsePlatform());
        }
    }

    // 崩落状態かどうかを返す
    public bool IsCollapsed()
    {
        return repairState == RepairState.Collapsed;
    }
    
    // チュートリアル中のfragile橋をmiss後に修繕可能状態にリセットするメソッド
    public void ResetToRepairable()
    {
        if (type != PlatformType.Fragile) return;
        
        // 落下中の足場がある場合は削除
        if (_fallingPlatform != null)
        {
            Destroy(_fallingPlatform);
            _fallingPlatform = null;
        }
        
        // 落下状態をリセット
        if (_fallCoroutine != null)
        {
            StopCoroutine(_fallCoroutine);
            _fallCoroutine = null;
        }
        
        _isFalling = false;
        _canCatch = false;
        _fallAnimationStopped = false;
        
        // 修繕状態を最初に戻す
        repairState = RepairState.Broken;
        isRepaired = false;
        
        // 元の足場を表示状態に戻す
        SetPlatformVisible(true);
        
        // 元のマテリアルに戻す（修繕前の状態）
        if (_renderer != null)
        {
            // 初期マテリアルがあれば戻す、なければデフォルトマテリアルを使用
            // ここではrepairedMaterialではない状態に戻したいので、
            // 元のマテリアルを保存していない場合は現在のマテリアルをそのまま使用
        }
        
        Debug.Log($"Fragile platform {name} reset to repairable state");
    }
    #endregion

    #region Private Methods - Repair Logic
    private bool CanStartRepair()
    {
        // 通常足場の場合は壊れた状態から修理可能
        if (type == PlatformType.Normal)
        {
            return repairState == RepairState.Broken;
        }
        
        // 壊れた足場の場合は従来通り
        return type == PlatformType.Fragile && repairState == RepairState.Broken;
    }

    private bool CanCompleteRepair()
    {
        // 通常足場の場合は即座に修理完了可能
        if (type == PlatformType.Normal)
        {
            return repairState == RepairState.Broken;
        }
        
        // 壊れた足場の場合は修理中状態から完了へ
        return type == PlatformType.Fragile && repairState == RepairState.Repairing;
    }

    private void CompleteRepair()
    {
        if (type == PlatformType.Normal)
        {
            // 通常足場の場合は即座に修理完了
            repairState = RepairState.Completed;
            isRepaired = true;
        }
        else if (type == PlatformType.Fragile)
        {
            // 壊れた足場の場合は足場落下処理
            repairState = RepairState.ReadyForReplacement;
            SetPlatformVisible(false);
            StartCoroutine(DropNewPlatform());
        }
        
        // チュートリアルカウントはキャッチ完了時のみ追加するため、ここでは削除
        // 壊れた足場のカウントはCatchPlatformAtPositionで処理される
    }
    #endregion

    #region Private Methods - Platform Catching
    private bool CanCatchPlatform()
    {
        return _isFalling && _fallingPlatform;
    }

    private bool IsWithinHeightTolerance(Vector3 position)
    {
        float heightDifference = Mathf.Abs(position.y - _originalPosition.y);
        return heightDifference <= allowedHeightError;
    }

    private bool CatchPlatformAtPosition(Vector3 position)
    {
        StopFalling();
        
        // fragile足場の場合、新しい足場と入れ替える処理
        if (type == PlatformType.Fragile)
        {
            ReplacePlatformWithNew(position);
        }
        else
        {
            // 通常足場の場合は従来通りの処理
            MovePlatformToPosition(position);
            SetPlatformAsRepaired();
            CleanupFallingPlatform();
        }
        
        // チュートリアル中の場合、キャッチ成功時にカウントを追加
        if (GameManager.Instance != null && GameManager.Instance.IsTutorialMode())
        {
            GameManager.Instance.OnTutorialPlatformCompleted();
            Debug.Log($"Fragile platform tutorial completed: {name} caught successfully");
        }
        
        return true;
    }
    
    private void ReplacePlatformWithNew(Vector3 position)
    {
        if (_fallingPlatform != null)
        {
            // 落下してきた足場を指定位置に移動
            _fallingPlatform.transform.position = position;
            
            // 落下してきた足場のPlatformコンポーネントを取得して設定
            Platform newPlatform = _fallingPlatform.GetComponent<Platform>();
            if (newPlatform != null)
            {
                newPlatform.type = PlatformType.Normal; // 新しい足場は通常足場として設定
                newPlatform.repairState = RepairState.Completed;
                newPlatform.isRepaired = true;
                
                // 親オブジェクトを設定（元の足場と同じ親に）
                if (transform.parent != null)
                {
                    _fallingPlatform.transform.SetParent(transform.parent);
                }
            }
            
            // 元のfragile足場を削除
            Destroy(gameObject);
        }
        
        _isFalling = false;
        _canCatch = false;
    }

    private void MovePlatformToPosition(Vector3 position)
    {
        transform.position = position;
    }

    private void SetPlatformAsRepaired()
    {
        repairState = RepairState.Completed;
        isRepaired = true;
        SetPlatformVisible(true);
    }

    private void CleanupFallingPlatform()
    {
        if (_fallingPlatform != null)
        {
            Destroy(_fallingPlatform);
            _fallingPlatform = null;
        }
        _isFalling = false;
        _canCatch = false;
    }
    #endregion

    #region Private Methods - Fall Animation
    private IEnumerator DropNewPlatform()
    {
        if (newPlatformPrefab == null) yield break;
        
        SetupFallParameters();
        CreateFallingPlatform();
        
        _fallCoroutine = StartCoroutine(FallPlatform());
        yield return _fallCoroutine;
    }

    private void SetupFallParameters()
    {
        _originalPosition = transform.position;
        _isFalling = true;
    }

    private void CreateFallingPlatform()
    {
        Vector3 dropPosition = _originalPosition + Vector3.up * fallHeight;
        _fallingPlatform = Instantiate(newPlatformPrefab, dropPosition, transform.rotation);
    }

    private IEnumerator FallPlatform()
    {
        if (_fallingPlatform == null) yield break;
        
        var fallData = CalculateFallParameters();
        
        yield return StartCoroutine(AnimateFall(fallData));
        
        if (_isFalling)
        {
            HandleFallComplete();
        }
    }

    private FallAnimationData CalculateFallParameters()
    {
        Vector3 startPos = _fallingPlatform.transform.position;
        Vector3 targetPos = _originalPosition + Vector3.down * FallBelowDistance;
        float fallDistance = Vector3.Distance(startPos, targetPos);
        float fallTime = fallDistance / fallSpeed;
        
        float originalDistance = Vector3.Distance(startPos, _originalPosition);
        float catchRangeStart = originalDistance * CatchRangeStartRatio;
        float catchRangeEnd = originalDistance * CatchRangeEndRatio;
        
        return new FallAnimationData
        {
            StartPosition = startPos,
            TargetPosition = targetPos,
            FallTime = fallTime,
            CatchRangeStart = catchRangeStart,
            CatchRangeEnd = catchRangeEnd
        };
    }

    private IEnumerator AnimateFall(FallAnimationData data)
    {
        float elapsed = 0f;
        
        while (elapsed < data.FallTime && _isFalling)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / data.FallTime;
            
            UpdateCatchability(data);
            UpdateFallPosition(data, t);
            
            yield return null;
        }
    }

    private void UpdateCatchability(FallAnimationData data)
    {
        float currentDistance = Vector3.Distance(data.StartPosition, _fallingPlatform.transform.position);
        _canCatch = currentDistance >= data.CatchRangeStart && currentDistance <= data.CatchRangeEnd;
    }

    private void UpdateFallPosition(FallAnimationData data, float t)
    {
        float fallT = 1 - Mathf.Pow(1 - t, 2); // EaseInQuad for gravity effect
        _fallingPlatform.transform.position = Vector3.Lerp(data.StartPosition, data.TargetPosition, fallT);
    }

    private void HandleFallComplete()
    {
        _fallingPlatform.transform.position = CalculateFallParameters().TargetPosition;
        _canCatch = false;
        _isFalling = false;
        _fallCoroutine = null;
        
        // チュートリアル中は橋崩落を発生させない
        if (GameManager.Instance != null && !GameManager.Instance.IsTutorialMode())
        {
            // 通常ゲーム中のみ橋全体の崩落を開始
            GameManager.Instance.TriggerBridgeCollapse();
        }
        else if (GameManager.Instance != null && GameManager.Instance.IsTutorialMode())
        {
            // チュートリアル中はログのみ出力
            Debug.Log("Tutorial mode: Bridge collapse prevented on fall timeout");
        }
        
        // 落下中の足場をクリーンアップ
        if (_fallingPlatform != null)
        {
            Destroy(_fallingPlatform);
            _fallingPlatform = null;
        }
        repairState = RepairState.Broken;
    }

    // TimingBarと同期した落下アニメーション
    private IEnumerator FallWithTimingBar()
    {
        if (_fallingPlatform == null) yield break;
        
        // PlayerControllerのタイミングウィンドウ時間を取得
        float timingDuration = 2f; // デフォルト値
        var playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            // PlayerControllerのTimingWindowDurationプロパティを参照
            timingDuration = playerController.TimingWindowDuration;
        }
        
        var fallData = CalculateTimingFallParameters(timingDuration);
        
        yield return StartCoroutine(AnimateTimingFall(fallData, timingDuration));
        
        if (_isFalling)
        {
            HandleFallComplete();
        }
    }

    private FallAnimationData CalculateTimingFallParameters(float duration)
    {
        Vector3 startPos = _fallingPlatform.transform.position;
        Vector3 targetPos = _originalPosition + Vector3.down * FallBelowDistance;
        
        // キャッチ可能範囲をTimingBarの中央部分に設定
        float catchRangeStart = duration * 0.3f; // 30%の時点から
        float catchRangeEnd = duration * 0.7f;   // 70%の時点まで
        
        return new FallAnimationData
        {
            StartPosition = startPos,
            TargetPosition = targetPos,
            FallTime = duration,
            CatchRangeStart = catchRangeStart,
            CatchRangeEnd = catchRangeEnd
        };
    }

    private IEnumerator AnimateTimingFall(FallAnimationData data, float duration)
    {
        float elapsed = 0f;
        
        while (elapsed < duration && _isFalling && !_fallAnimationStopped)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // TimingBarの進行に合わせてキャッチ可能性を更新
            UpdateTimingCatchability(elapsed, data);
            
            // 落下位置を更新（TimingBarの進行と同期）
            UpdateTimingFallPosition(data, t);
            
            yield return null;
        }
    }

    private void UpdateTimingCatchability(float elapsed, FallAnimationData data)
    {
        // タイミングウィンドウの中央付近でキャッチ可能に
        _canCatch = elapsed >= data.CatchRangeStart && elapsed <= data.CatchRangeEnd;
    }

    private void UpdateTimingFallPosition(FallAnimationData data, float t)
    {
        // 4段階の移動処理：2→0.5、0.5→0、0→-0.5、-0.5→-2
        // 判定付近（0.5→0→-0.5）のみ遅くなるように設定
        
        Vector3 currentPosition;
        float yOffset;
        
        if (t <= 0.2f)
        {
            // 0%～20%：y=+2からy=+0.5まで高速落下
            float adjustedT = t / 0.2f; // 0.0 ～ 1.0 に正規化
            yOffset = Mathf.Lerp(2f, 0.5f, adjustedT);
        }
        else if (t <= 0.5f)
        {
            // 20%～50%：y=+0.5からy=0まで低速落下（判定付近）
            float adjustedT = (t - 0.2f) / 0.3f; // 0.0 ～ 1.0 に正規化
            yOffset = Mathf.Lerp(0.5f, 0f, adjustedT);
        }
        else if (t <= 0.8f)
        {
            // 50%～80%：y=0からy=-0.5まで低速落下（判定付近）
            float adjustedT = (t - 0.5f) / 0.3f; // 0.0 ～ 1.0 に正規化
            yOffset = Mathf.Lerp(0f, -0.5f, adjustedT);
        }
        else
        {
            // 80%～100%：y=-0.5からy=-2まで高速落下
            float adjustedT = (t - 0.8f) / 0.2f; // 0.0 ～ 1.0 に正規化
            yOffset = Mathf.Lerp(-0.5f, -2f, adjustedT);
        }
        
        // 元の位置にyオフセットを適用
        currentPosition = _originalPosition + new Vector3(0, yOffset, 0);
        
        _fallingPlatform.transform.position = currentPosition;
    }

    private IEnumerator CleanupAfterFall()
    {
        yield return new WaitForSeconds(FallCleanupDelay);
        
        if (_fallingPlatform != null)
        {
            Destroy(_fallingPlatform);
            _fallingPlatform = null;
        }
        repairState = RepairState.Broken;
    }
    #endregion

    #region Private Methods - Collapse Logic

    private IEnumerator CollapsePlatform()
    {
        // 崩落開始の遅延
        yield return new WaitForSeconds(collapseDelay);

        var state = GameManager.Instance.GetCurrentState();
        switch (state)
        {
            case GameManager.GameState.Title:
            case GameManager.GameState.Tutorial:
                yield break;
        }
        
        // 崩落状態に設定
        repairState = RepairState.Collapsed;
        isRepaired = false;
        
        // 落下中の足場を削除
        CleanupFallingPlatform();
        
        // 崩落エフェクトを再生
        PlayCollapseEffects();
        
        // Rigidbodyを使った物理落下
        EnablePhysicsFall();
        
        // ゲームマネージャーに崩落を通知
        NotifyCollapseEvent();
    }
    
    private void PlayCollapseEffects()
    {
        // パーティクルエフェクト再生
        if (collapseEffect != null)
        {
            collapseEffect.Play();
        }
        
        // 崩落音はGameManagerで一回だけ再生するため、ここでは再生しない
        // （個別の足場音再生を無効化）
        
        // カメラシェイクをトリガー（PlayerControllerに通知）
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.TriggerCollapseShake();
        }
    }
    
    private void EnablePhysicsFall()
    {
        // 既存のRigidbodyを取得
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // 物理演算を有効化
            rb.isKinematic = false;
            rb.useGravity = true;
            
            // Mesh Colliderがある場合は凸面に設定
            MeshCollider meshCollider = GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                meshCollider.convex = true;
            }
            
            // 少しランダムな力を加えて自然な崩落感を演出
            Vector3 randomForce = new Vector3(
                Random.Range(-2f, 2f),
                0f,
                Random.Range(-2f, 2f)
            );
            rb.AddForce(randomForce, ForceMode.Impulse);
            
            // 回転も追加
            Vector3 randomTorque = new Vector3(
                Random.Range(-3f, 3f),
                Random.Range(-3f, 3f),
                Random.Range(-3f, 3f)
            );
            rb.AddTorque(randomTorque, ForceMode.Impulse);
        }
        
        // 一定時間後に削除
        Destroy(gameObject, 5f);
    }
    
    private void NotifyCollapseEvent()
    {
        // ゲームマネージャーやイベントシステムに崩落を通知
    }
    #endregion

    #region Private Methods - Utilities
    private void SetPlatformVisible(bool visible)
    {
        // 子オブジェクトが1つしかない場合の最適化
        if (transform.childCount > 0)
        {
            transform.GetChild(0).gameObject.SetActive(visible);
        }
    }
    #endregion

    #region Helper Classes
    private struct FallAnimationData
    {
        public Vector3 StartPosition;
        public Vector3 TargetPosition;
        public float FallTime;
        public float CatchRangeStart;
        public float CatchRangeEnd;
    }
    #endregion

    #region Private Methods - Catch Accuracy
    private float CalculateCatchAccuracy(Vector3 stoppedPosition)
    {
        // TimingCursorの位置に基づいて正確度を計算
        float cursorX = 0f;
        float barWidth = 1000f; // デフォルト値
        
        if (UIManager.Instance != null)
        {
            cursorX = UIManager.Instance.GetCurrentCursorPosition();
            barWidth = UIManager.Instance.GetTimingBarWidth();
        }
        
        // TimingBarの実際の幅の半分を最大値として使用
        float maxDistance = barWidth / 2f;
        
        // x座標(-maxDistance~maxDistance)を正確度(0~100%)に変換
        // x=0で100%、|x|=maxDistanceで0%
        float accuracyPercentage = Mathf.Max(0f, (maxDistance - Mathf.Abs(cursorX)) / maxDistance * 100f);
        
        return accuracyPercentage;
    }

    private string EvaluateCatchJudgment(float catchAccuracy)
    {
        // 現在の難易度に応じた判定閾値を設定
        float perfectThreshold, goodThreshold, badThreshold;
        
        GameManager.Difficulty currentDifficulty = GameManager.Difficulty.Normal;
        if (GameManager.Instance != null)
        {
            currentDifficulty = GameManager.Instance.GetCurrentDifficulty();
        }
        
        switch (currentDifficulty)
        {
            case GameManager.Difficulty.Easy:
                perfectThreshold = 80f;  // 80%以上でPerfect
                goodThreshold = 60f;     // 60%以上でGood
                badThreshold = 40f;      // 40%以上でBad
                break;
            case GameManager.Difficulty.Normal:
                perfectThreshold = 85f;  // 85%以上でPerfect
                goodThreshold = 67.5f;   // 67.5%以上でGood
                badThreshold = 50f;      // 50%以上でBad
                break;
            case GameManager.Difficulty.Hard:
                perfectThreshold = 90f;  // 90%以上でPerfect
                goodThreshold = 75f;     // 75%以上でGood
                badThreshold = 60f;      // 60%以上でBad
                break;
            default:
                perfectThreshold = 85f;
                goodThreshold = 67.5f;
                badThreshold = 50f;
                break;
        }
        
        if (catchAccuracy >= perfectThreshold)
        {
            return "Perfect";
        }
        else if (catchAccuracy >= goodThreshold)
        {
            return "Good";
        }
        else if (catchAccuracy >= badThreshold)
        {
            return "Bad";
        }
        else
        {
            return "Miss";
        }
    }
    #endregion
}
