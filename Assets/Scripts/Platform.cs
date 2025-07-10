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
    
    [Header("Materials")]
    public Material repairedMaterial;
    public Material stepMaterial1;
    public Material stepMaterial2;
    
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
    #endregion

    #region Constants
    private const float FALL_BELOW_DISTANCE = 1f; // 叩ける範囲と同じ1メートル下まで落とす
    private const float CATCH_RANGE_START_RATIO = 0.8f;
    private const float CATCH_RANGE_END_RATIO = 1.2f;
    private const float FALL_CLEANUP_DELAY = 1f;
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
        if (CanCompleteRepair())
        {
            CompleteRepair();
        }
    }

    public bool TryCatchFallingPlatform()
    {
        if (!CanCatchPlatform()) return false;

        Vector3 stoppedPosition = _fallingPlatform.transform.position;
        
        if (IsWithinHeightTolerance(stoppedPosition))
        {
            return CatchPlatformAtPosition(stoppedPosition);
        }
        
        return false; // Outside height tolerance
    }

    public void ChangeMaterialStep(int step)
    {
        if (_renderer == null) return;
        
        switch (step)
        {
            case 3 when stepMaterial1 != null:
                _renderer.material = stepMaterial1;
                break;
            case 6 when stepMaterial2 != null:
                _renderer.material = stepMaterial2;
                break;
        }
    }

    public void MissedCatch()
    {
        if (_isFalling && _fallingPlatform)
        {
            _canCatch = false;
            // 足場キャッチに失敗したため橋全体の崩落を開始
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerBridgeCollapse();
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
    #endregion

    #region Private Methods - Repair Logic
    private bool CanStartRepair()
    {
        return type == PlatformType.Fragile && repairState == RepairState.Broken;
    }

    private bool CanCompleteRepair()
    {
        return type == PlatformType.Fragile && repairState == RepairState.Repairing;
    }

    private void CompleteRepair()
    {
        repairState = RepairState.ReadyForReplacement;
        SetPlatformVisible(false);
        StartCoroutine(DropNewPlatform());
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
        MovePlatformToPosition(position);
        SetPlatformAsRepaired();
        CleanupFallingPlatform();
        return true;
    }

    private void StopFalling()
    {
        if (_fallCoroutine != null)
        {
            StopCoroutine(_fallCoroutine);
            _fallCoroutine = null;
        }
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
        
        if (repairedMaterial != null && _renderer != null)
        {
            _renderer.material = repairedMaterial;
        }
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
        Vector3 targetPos = _originalPosition + Vector3.down * FALL_BELOW_DISTANCE;
        float fallDistance = Vector3.Distance(startPos, targetPos);
        float fallTime = fallDistance / fallSpeed;
        
        float originalDistance = Vector3.Distance(startPos, _originalPosition);
        float catchRangeStart = originalDistance * CATCH_RANGE_START_RATIO;
        float catchRangeEnd = originalDistance * CATCH_RANGE_END_RATIO;
        
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
        
        // 足場が叩ける範囲を通り過ぎたため、即座に崩落を開始
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerBridgeCollapse();
        }
        
        // 落下中の足場をクリーンアップ
        if (_fallingPlatform != null)
        {
            Destroy(_fallingPlatform);
            _fallingPlatform = null;
        }
        repairState = RepairState.Broken;
    }

    private IEnumerator CleanupAfterFall()
    {
        yield return new WaitForSeconds(FALL_CLEANUP_DELAY);
        
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
        
        // 崩落音再生
        if (collapseSound != null)
        {
            AudioSource.PlayClipAtPoint(collapseSound, transform.position);
        }
        
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
                Random.Range(-5f, 5f),
                Random.Range(-5f, 5f),
                Random.Range(-5f, 5f)
            );
            rb.AddTorque(randomTorque, ForceMode.Impulse);
        }
        
        // 一定時間後に削除
        Destroy(gameObject, 5f);
    }
    
    private void NotifyCollapseEvent()
    {
        // ゲームマネージャーやイベントシステムに崩落を通知
        Debug.Log($"Platform {gameObject.name} has collapsed!");
    }
    #endregion

    #region Private Methods - Utilities
    private void SetPlatformVisible(bool visible)
    {
        if (_renderer != null) _renderer.enabled = visible;
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
}
