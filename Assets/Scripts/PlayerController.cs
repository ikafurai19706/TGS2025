using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    #region Public Fields
    [Header("Input")]
    public InputActionAsset inputActions;
    
    [Header("Animation")]
    public Transform hammer;
    public Transform cameraTransform;
    #endregion

    #region Private Fields
    private float _inputX;
    private float _inputZ;
    private Rigidbody _rigidbody;
    private InputAction _moveAction;
    private bool _isMoving;
    private bool _canMove = true; // 移動可能フラグを追加

    private bool _isRepairing;
    private int _repairCount;
    private Platform _targetPlatform;
    private Coroutine _swingCoroutine;
    
    // Timing System
    private RepairTimingSystem _timingSystem;
    private bool _isInTimingChallenge = false;
    #endregion

    #region Constants
    private const int REPAIR_NEEDED = 9;
    private const float HAMMER_SWING_DURATION = 0.04f;
    private const float MOVEMENT_DISTANCE = 1.0f;
    private const float MOVEMENT_DURATION = 0.16f;
    private const float RAYCAST_OFFSET_Y = -0.5f;
    private const float RAYCAST_OFFSET_Z = 1f;
    private const float RAYCAST_DISTANCE = 1f;
    private const float CAMERA_SHAKE_DURATION = 0.08f;
    private const float CAMERA_SHAKE_MAGNITUDE = 0.08f;
    private const float COLLAPSE_SHAKE_DURATION = 0.5f;
    private const float COLLAPSE_SHAKE_MAGNITUDE = 0.2f;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeInput();
    }

    private void OnDestroy()
    {
        CleanupInput();
    }

    private void Start()
    {
        InitializeComponents();
    }

    private void Update()
    {
        HandleInput();
    }
    #endregion

    #region Initialization
    private void InitializeInput()
    {
        var actionMap = inputActions.FindActionMap("Player", true);
        _moveAction = actionMap.FindAction("Move", true);
        _moveAction.Enable();
    }

    private void CleanupInput()
    {
        if (_moveAction != null)
        {
            _moveAction.Disable();
        }
    }

    private void InitializeComponents()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _timingSystem = GetComponent<RepairTimingSystem>();
        
        if (_timingSystem == null)
        {
            Debug.LogWarning("PlayerController: RepairTimingSystem component not found!");
        }
    }
    #endregion

    #region Input Handling
    private void HandleInput()
    {
        if (_isRepairing)
        {
            HandleRepairInput();
            return; // 修繕中は移動処理を実行しない
        }

        HandlePlatformDetection();
        HandleMovementInput();
    }

    private void HandleRepairInput()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            // タイミングチャレンジ中の場合
            if (_isInTimingChallenge && _timingSystem != null)
            {
                _timingSystem.OnTimingInput();
                return;
            }
            
            // 9回の修繕が完了している場合は落下中の足場をキャッチ
            if (_repairCount >= REPAIR_NEEDED)
            {
                TryCatchFallingPlatform();
                return;
            }
            
            ProcessRepairHit();
        }
    }

    private void HandlePlatformDetection()
    {
        if (TryDetectPlatform(out Platform platform))
        {
            Debug.Log($"Platform detected: {platform.name}, CanRepair: {platform.CanRepair()}, IsFalling: {platform.IsFalling()}, RepairState: {platform.repairState}");
            
            if (platform.CanRepair())
            {
                StartPlatformRepair(platform);
            }
            else if (platform.IsFalling())
            {
                StartFallingPlatformCatch(platform);
            }
            // 足場が検出されたが修理もキャッチもできない場合の処理を追加
            else if (platform.repairState == Platform.RepairState.Completed)
            {
                // 修理完了済みの足場の場合は何もしない（通行可能）
                Debug.Log($"Platform {platform.name} is completed, allowing passage");
            }
            else
            {
                // 予期しない状態の場合はログを出力
                Debug.LogWarning($"Platform {platform.name} in unexpected state: {platform.repairState}");
            }
        }
        else
        {
            HandleFallingPlatformCatch();
        }
    }

    private void HandleMovementInput()
    {
        if (_canMove && Keyboard.current.spaceKey.wasPressedThisFrame && !_isMoving)
        {
            StartCoroutine(MoveWithHammerSwing(MOVEMENT_DISTANCE, MOVEMENT_DURATION));
        }
    }

    private void HandleFallingPlatformCatch()
    {
        if (_targetPlatform != null && _targetPlatform.IsFalling())
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TryCatchFallingPlatform();
            }
        }
    }
    #endregion

    #region Repair Logic
    private void ProcessRepairHit()
    {
        _repairCount++;
        
        UpdateMaterialProgress();
        PerformHammerSwing();
        
        if (_repairCount == REPAIR_NEEDED)
        {
            CompleteRepair();
        }
    }

    private void UpdateMaterialProgress()
    {
        if ((_repairCount == 3 || _repairCount == 6) && _targetPlatform != null)
        {
            _targetPlatform.ChangeMaterialStep(_repairCount);
        }
    }

    private void PerformHammerSwing()
    {
        StopCurrentSwing();
        _swingCoroutine = StartCoroutine(SwingHammer(HAMMER_SWING_DURATION));
    }

    private void CompleteRepair()
    {
        // タイミングチャレンジを開始
        if (_timingSystem != null && GameManager.Instance != null)
        {
            _isInTimingChallenge = true;
            var config = GameManager.Instance.GetCurrentDifficultyConfig();
            _timingSystem.StartTimingChallenge(config, OnTimingChallengeComplete);
        }
        else
        {
            // タイミングシステムがない場合は従来の処理
            _targetPlatform.Repair();
        }
    }
    
    private void OnTimingChallengeComplete(GameManager.TimingResult result)
    {
        _isInTimingChallenge = false;
        
        // GameManagerに結果を通知
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRepairAttempt(result);
        }
        
        // 結果に応じて処理を分岐
        if (result != GameManager.TimingResult.Miss)
        {
            // Miss以外の場合は修繕を完了
            _targetPlatform.Repair();
            
            // チュートリアルモードの場合、足場完了を通知
            if (GameManager.Instance.IsTutorialMode())
            {
                GameManager.Instance.OnTutorialPlatformCompleted();
            }
        }
        
        // 修繕状態をリセット（Miss以外の場合は落下キャッチに移行）
        if (result == GameManager.TimingResult.Miss)
        {
            ResetRepairState();
        }
    }

    private void StartPlatformRepair(Platform platform)
    {
        _isRepairing = true;
        _repairCount = 0;
        _targetPlatform = platform;
        platform.StartRepair();
    }

    private void StartFallingPlatformCatch(Platform platform)
    {
        _targetPlatform = platform;
        _isRepairing = true;
        _repairCount = REPAIR_NEEDED;
    }
    #endregion

    #region Platform Detection
    private bool TryDetectPlatform(out Platform platform)
    {
        // プレイヤーの現在のZ座標を基に、次の足場の位置を計算
        float playerZ = transform.position.z;
        int nextPlatformIndex = GetNextPlatformIndexFromPosition(playerZ);
        
        // 次の足場の確定位置（中心座標）
        Vector3 platformPosition = new Vector3(0, 0, nextPlatformIndex);
        
        // その位置にある足場を検索
        platform = FindPlatformAtPosition(platformPosition);
        
        if (platform != null)
        {
            Debug.Log($"Player at Z={playerZ:F2}, detected NEXT platform index {nextPlatformIndex} at position ({platformPosition.x}, {platformPosition.y}, {platformPosition.z}): {platform.name}");
            return true;
        }
        
        return false;
    }
    
    private int GetNextPlatformIndexFromPosition(float playerZ)
    {
        // 現在いる足場のインデックスを取得
        int currentPlatformIndex = Mathf.FloorToInt(playerZ + 0.5f);
        
        // 次の足場のインデックスを返す
        return currentPlatformIndex + 1;
    }
    
    private int GetPlatformIndexFromPosition(float playerZ)
    {
        // 足場nの範囲は (n-0.5) <= z < (n+0.5)
        // 例: 足場1の範囲は 0.5 <= z < 1.5
        // 例: 足場2の範囲は 1.5 <= z < 2.5
        
        // プレイヤーのZ座標に0.5を足してから切り捨てることで正確な足場インデックスを取得
        return Mathf.FloorToInt(playerZ + 0.5f);
    }
    
    private Platform FindPlatformAtPosition(Vector3 position)
    {
        // 指定位置付近の小さな範囲内でPlatformコンポーネントを持つオブジェクトを検索
        Collider[] colliders = Physics.OverlapSphere(position, 0.5f);
        
        foreach (Collider collider in colliders)
        {
            Platform platform = collider.GetComponent<Platform>();
            if (platform != null)
            {
                // 位置がほぼ一致するかチェック（少し誤差を許容）
                Vector3 platformPos = platform.transform.position;
                if (Vector3.Distance(platformPos, position) < 0.1f)
                {
                    return platform;
                }
            }
        }
        
        return null;
    }

    private void TryCatchFallingPlatform()
    {
        StopCurrentSwing();
        _swingCoroutine = StartCoroutine(SwingHammer(HAMMER_SWING_DURATION));
        
        if (_targetPlatform.TryCatchFallingPlatform())
        {
            ResetRepairState();
        }
    }

    private void ResetRepairState()
    {
        _isRepairing = false;
        _repairCount = 0;
        _targetPlatform = null;
    }
    #endregion

    #region Animation
    private void StopCurrentSwing()
    {
        if (_swingCoroutine != null)
        {
            StopCoroutine(_swingCoroutine);
        }
    }

    private IEnumerator MoveWithHammerSwing(float distance, float duration)
    {
        _isMoving = true;
        yield return StartCoroutine(SwingHammer(HAMMER_SWING_DURATION));
        yield return StartCoroutine(MoveZWithEaseOut(distance, duration));
        _isMoving = false;
    }

    private IEnumerator SwingHammer(float duration)
    {
        if (hammer == null) yield break;
        
        yield return StartCoroutine(SwingDown(duration));
        TriggerCameraShake();
        yield return StartCoroutine(SwingUp(duration * 2f));
    }

    private IEnumerator SwingDown(float duration)
    {
        Quaternion startRot = hammer.localRotation;
        Quaternion endRot = Quaternion.Euler(90, hammer.localEulerAngles.y, hammer.localEulerAngles.z);
        
        yield return StartCoroutine(AnimateRotation(startRot, endRot, duration, EaseInQuad));
    }

    private IEnumerator SwingUp(float duration)
    {
        Quaternion startRot = hammer.localRotation;
        Quaternion endRot = Quaternion.Euler(45, hammer.localEulerAngles.y, hammer.localEulerAngles.z);
        
        yield return StartCoroutine(AnimateRotation(startRot, endRot, duration, EaseOutQuad));
    }

    private IEnumerator AnimateRotation(Quaternion start, Quaternion end, float duration, System.Func<float, float> easingFunction)
    {
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = easingFunction(t);
            hammer.localRotation = Quaternion.Slerp(start, end, easeT);
            yield return null;
        }
        
        hammer.localRotation = end;
    }

    private void TriggerCameraShake()
    {
        if (cameraTransform != null)
        {
            StartCoroutine(CameraShake(CAMERA_SHAKE_DURATION, CAMERA_SHAKE_MAGNITUDE));
        }
    }

    // 崩落時の強いカメラシェイクを追加
    public void TriggerCollapseShake()
    {
        if (cameraTransform != null)
        {
            StartCoroutine(CameraShake(COLLAPSE_SHAKE_DURATION, COLLAPSE_SHAKE_MAGNITUDE));
        }
    }

    // 移動制御用の公開メソッド
    public void SetCanMove(bool canMove)
    {
        _canMove = canMove;
    }

    private IEnumerator CameraShake(float duration, float magnitude)
    {
        Vector3 originalPos = cameraTransform.localPosition;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            float x = Random.Range(-0.5f, 0.5f) * magnitude;
            float y = Random.Range(-0.5f, 0.5f) * magnitude;
            cameraTransform.localPosition = originalPos + new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        cameraTransform.localPosition = originalPos;
    }

    private IEnumerator MoveZWithEaseOut(float distance, float duration)
    {
        Vector3 start = transform.position;
        Vector3 end = start + new Vector3(0, 0, distance);
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = EaseOutQuad(t);
            transform.position = Vector3.Lerp(start, end, easeT);
            yield return null;
        }
        
        transform.position = end;
    }
    #endregion

    #region Easing Functions
    private static float EaseInQuad(float t)
    {
        return t * t;
    }

    private static float EaseOutQuad(float t)
    {
        return 1 - Mathf.Pow(1 - t, 2);
    }
    #endregion
}
