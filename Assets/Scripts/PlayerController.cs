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
            // 9回の修繕が完了している場合は落下中の足場をキャッチ
            if (_repairCount >= REPAIR_NEEDED)
            {
                TryCatchFallingPlatform();
            }
            else
            {
                ProcessRepairHit();
            }
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
                return; // 修理開始後は他の処理をスキップ
            }
            else if (platform.IsFalling())
            {
                StartFallingPlatformCatch(platform);
                return; // キャッチ開始後は他の処理をスキップ
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
        _targetPlatform.Repair();
        // 修繕状態は維持して落下中の足場をキャッチできるようにする
        // _isRepairing = false; この行を削除
        // Keep _targetPlatform for falling platform catch
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
        Vector3 origin = transform.position + new Vector3(0, RAYCAST_OFFSET_Y, RAYCAST_OFFSET_Z);
        
        if (Physics.Raycast(origin, Vector3.down, out var hit, RAYCAST_DISTANCE))
        {
            platform = hit.collider.GetComponent<Platform>();
            return platform != null;
        }
        
        platform = null;
        return false;
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
