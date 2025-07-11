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
    
    [Header("Timing Challenge Settings")]
    public float timingWindowDuration = 2f; // タイミング入力の全体時間
    
    // Platformクラスからアクセスできるようにパブリックプロパティを追加
    public float TimingWindowDuration => timingWindowDuration;
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
    
    // Integrated Timing System
    private bool _isInTimingChallenge;
    private float _timingStartTime;
    private GameManager.DifficultyConfig _currentConfig;
    private System.Action<GameManager.TimingResult> _onTimingComplete;
    private Coroutine _timingCoroutine;
    
    // 初期位置を記録
    private Vector3 _initialPosition;
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
        CheckGameCompletion(); // ゲーム完了チェックを追加
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
        
        // 初期位置を記録
        _initialPosition = transform.position;
        
        // RepairTimingSystemコンポーネントは不要になったため削除
        // タイミングシステムは統合されました
    }
    #endregion

    #region Integrated Timing System
    public void StartTimingChallenge(GameManager.DifficultyConfig config, System.Action<GameManager.TimingResult> onComplete)
    {
        if (_isInTimingChallenge) return;

        _currentConfig = config;
        _onTimingComplete = onComplete;
        
        _timingCoroutine = StartCoroutine(TimingSequence());
    }
    
    private IEnumerator TimingSequence()
    {
        _isInTimingChallenge = true;
        
        // UIManagerにタイミングUI表示を依頼（即座に表示）
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowTimingUI(timingWindowDuration);
        }
        
        // 修繕フェーズを0.3秒遅らせてスタート
        yield return new WaitForSeconds(0.3f);
        
        // 実際のタイミング開始時刻を設定
        _timingStartTime = Time.time;
        
        // 足場の落下を開始（0.3秒遅れて開始）
        if (_targetPlatform != null)
        {
            _targetPlatform.StartFalling();
        }
        
        // タイミング窓の終了を待つ
        yield return new WaitForSeconds(timingWindowDuration);
        
        // 時間切れの場合はMiss
        if (_isInTimingChallenge)
        {
            CompleteTimingChallenge(GameManager.TimingResult.Miss);
        }
    }
    
    private void ProcessTimingInput()
    {
        if (!_isInTimingChallenge) return;

        // タイミングをチェック
        float elapsedTime = Time.time - _timingStartTime;
        float normalizedTime = elapsedTime / timingWindowDuration;

        // 現在修理中の足場の落下を停止し、同時にキャッチ処理を実行
        if (_targetPlatform != null)
        {
            // 足場の落下を停止
            _targetPlatform.StopFalling();
            
            // 直接キャッチ処理を実行（カウント増加を含む）
            bool catchSuccess = _targetPlatform.TryCatchFallingPlatform();
            
            if (catchSuccess)
            {
                // キャッチ成功時は修理状態をリセット
                ResetRepairState();
            }
        }
        else
        {
            Debug.LogWarning("ProcessTimingInput: _targetPlatform is null!");
        }

        GameManager.TimingResult result = EvaluateTiming(normalizedTime);
        CompleteTimingChallenge(result);
    }
    
    private GameManager.TimingResult EvaluateTiming(float normalizedTime)
    {
        // カーソル位置に依存した正確な百分率計算
        float perfectCenter = 0.5f; // 中央位置（50%）
        float distanceFromCenter = Mathf.Abs(normalizedTime - perfectCenter);
        float accuracyPercentage = (1f - (distanceFromCenter / 0.5f)) * 100f; // 中央からの距離を百分率に変換
        
        // 現在の難易度に応じた判定閾値を設定
        float perfectThreshold, goodThreshold, badThreshold;
        
        switch (GameManager.Instance?.GetCurrentDifficulty() ?? GameManager.Difficulty.Normal)
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
        
        // 百分率に基づいた判定
        if (accuracyPercentage >= perfectThreshold)
        {
            return GameManager.TimingResult.Perfect;
        }
        else if (accuracyPercentage >= goodThreshold)
        {
            return GameManager.TimingResult.Good;
        }
        else if (accuracyPercentage >= badThreshold)
        {
            return GameManager.TimingResult.Bad;
        }
        else
        {
            return GameManager.TimingResult.Miss;
        }
    }
    
    private void CompleteTimingChallenge(GameManager.TimingResult result)
    {
        _isInTimingChallenge = false;
        
        // タイミングコルーチンを停止
        if (_timingCoroutine != null)
        {
            StopCoroutine(_timingCoroutine);
            _timingCoroutine = null;
        }
        
        // UIManagerにタイミングUI非表示を依頼
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideTimingUI();
        }
        
        // 結果をコールバック
        _onTimingComplete?.Invoke(result);
    }
    #endregion

    #region Input Handling
    private void HandleInput()
    {
        // ゲーム状態をチェックして、Tutorial または Playing状態の場合のみ入力を受け付ける
        // TutorialCountdown中は操作を禁止
        if (GameManager.Instance == null || 
            (GameManager.Instance.GetCurrentState() != GameManager.GameState.Playing && 
             GameManager.Instance.GetCurrentState() != GameManager.GameState.Tutorial))
        {
            return;
        }

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
            // チュートリアル中の通常足場の場合、1回叩いただけで進行カウントを増やす
            if (GameManager.Instance != null && GameManager.Instance.IsTutorialMode() && 
                _targetPlatform != null && _targetPlatform.type == Platform.PlatformType.Normal)
            {
                // 通常足場のチュートリアル処理
                ProcessNormalPlatformTutorial();
                return;
            }
            
            // タイミングチャレンジ中の場合
            if (_isInTimingChallenge)
            {
                ProcessTimingInput();
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

    // チュートリアル中の通常足場を叩いた時の処理
    private void ProcessNormalPlatformTutorial()
    {
        // ハンマーを振る演出
        PerformHammerSwing();
        
        // 修繕処理は呼ばず、直接チュートリアル進行カウントを増やす
        if (GameManager.Instance != null && GameManager.Instance.IsTutorialMode())
        {
            GameManager.Instance.OnTutorialPlatformCompleted();
        }
        
        // 修繕状態をリセット
        ResetRepairState();
    }

    private void HandlePlatformDetection()
    {
        if (TryDetectPlatform(out Platform platform))
        {
            // チュートリアル中の通常足場の場合、修理状態にしない
            if (GameManager.Instance != null && GameManager.Instance.IsTutorialMode() && platform.type == Platform.PlatformType.Normal)
            {
                // 通常足場では修理状態にせず、移動処理で叩いた時に処理する
                return;
            }
            else if (platform.CanRepair())
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

    // チュートリアル中の通常足場用の処理
    private void StartNormalPlatformTutorial(Platform platform)
    {
        _targetPlatform = platform;
        _isRepairing = true;
        _repairCount = 0;
    }

    private void HandleMovementInput()
    {
        if (_canMove && Keyboard.current.spaceKey.wasPressedThisFrame && !_isMoving)
        {
            // チュートリアル中の通常足場の場合、移動前に進行カウントを増やす
            if (GameManager.Instance != null && GameManager.Instance.IsTutorialMode())
            {
                if (TryDetectPlatform(out Platform platform) && platform.type == Platform.PlatformType.Normal)
                {
                    // 通常足場のチュートリアル進行カウントを増やす
                    GameManager.Instance.OnTutorialPlatformCompleted();
                }
            }
            
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
        
        // 初回叩き時から残り回数を表示
        UpdateCountLeftDisplay();
        
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
        if (GameManager.Instance != null)
        {
            var config = GameManager.Instance.GetCurrentDifficultyConfig();
            
            // 足場を落下開始させる（元の位置に戻す）
            if (_targetPlatform != null)
            {
                _targetPlatform.StartFalling();
            }
            
            StartTimingChallenge(config, OnTimingChallengeComplete);
        }
        else
        {
            // GameManagerがない場合は従来の処理
            _targetPlatform.Repair();
            ResetRepairState();
        }
    }
    
    private void OnTimingChallengeComplete(GameManager.TimingResult result)
    {
        _isInTimingChallenge = false;
        
        // チュートリアル時はGameManagerのスコア処理をスキップ
        if (GameManager.Instance != null && !GameManager.Instance.IsTutorialMode())
        {
            // 通常ゲーム中のみGameManagerに結果を通知
            GameManager.Instance.OnRepairAttempt(result);
        }
        
        // 結果に応じて処理を分岐
        if (result != GameManager.TimingResult.Miss)
        {
            // Miss以外の場合は修繕を完了
            _targetPlatform.Repair();
            
            // チュートリアル中の場合、ここではカウントを追加しない
            // 足場のキャッチ完了時に追加する
        }
        else
        {
            // Miss時の処理
            if (GameManager.Instance != null && GameManager.Instance.IsTutorialMode())
            {
                // チュートリアル時：単純にリセットするだけ
                ResetRepairState();
            }
            else
            {
                // 通常ゲーム時：ゲーム終了は既にGameManager.OnRepairAttemptで処理済み
                ResetRepairState();
            }
        }
        
        // 修繕状態をリセット（Miss以外の場合は落下キャッチに移行）
        if (result == GameManager.TimingResult.Miss)
        {
            // Miss時は上記で既に処理済み
            return;
        }
    }

    private void StartPlatformRepair(Platform platform)
    {
        _isRepairing = true;
        _repairCount = 0;
        _targetPlatform = platform;
        platform.StartRepair();
        
        // 修繕開始時は残り回数を表示しない（初回叩き時に表示）
    }

    private void StartFallingPlatformCatch(Platform platform)
    {
        _targetPlatform = platform;
        _isRepairing = true;
        _repairCount = REPAIR_NEEDED;
        
        // キャッチ状態では残り回数を0に
        UpdateCountLeftDisplay();
    }

    private void UpdateCountLeftDisplay()
    {
        if (UIManager.Instance != null)
        {
            int countLeft = Mathf.Max(0, REPAIR_NEEDED - _repairCount);
            UIManager.Instance.UpdateCountLeft(countLeft);
        }
    }

    private void ResetRepairState()
    {
        _isRepairing = false;
        _repairCount = 0;
        _targetPlatform = null;
        
        // 修繕状態リセット時に残り回数表示をクリア
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateCountLeft(0);
        }
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
    
    public void OnPlatformCollapse()
    {
        StartCoroutine(CameraShake(COLLAPSE_SHAKE_DURATION, COLLAPSE_SHAKE_MAGNITUDE));
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

    #region Game Completion Check
    private void CheckGameCompletion()
    {
        // ゲームが進行中でない場合はチェックしない
        if (GameManager.Instance == null || 
            GameManager.Instance.GetCurrentState() != GameManager.GameState.Playing)
        {
            return;
        }
        
        // プレイヤーの現在位置から次の足場の位置を取得
        float playerZ = transform.position.z;
        int nextPlatformIndex = GetNextPlatformIndexFromPosition(playerZ);
        Vector3 nextPlatformPosition = new Vector3(0, 0, nextPlatformIndex);
        
        // 次の足場が存在するかチェック
        Platform nextPlatform = FindPlatformAtPosition(nextPlatformPosition);
        
        // 次の足場が存在しない場合、ゲーム完了
        if (nextPlatform == null)
        {
            CompleteGame();
        }
    }
    
    private void CompleteGame()
    {
        // 移動を停止
        _canMove = false;
        
        // GameManagerにゲーム完了を通知
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnBridgeComplete();
        }
    }
    #endregion

    #region Player Reset
    public void ResetToInitialPosition()
    {
        // プレイヤーを初期位置に戻す
        transform.position = _initialPosition;
        
        // プレイヤーの状態をリセット
        _canMove = true;
        _isMoving = false;
        _isRepairing = false;
        _repairCount = 0;
        _targetPlatform = null;
        _isInTimingChallenge = false;
        
        // タイミングコルーチンを停止
        if (_timingCoroutine != null)
        {
            StopCoroutine(_timingCoroutine);
            _timingCoroutine = null;
        }
        
        // ハンマーアニメーションを停止
        if (_swingCoroutine != null)
        {
            StopCoroutine(_swingCoroutine);
            _swingCoroutine = null;
        }
        
        // ハンマーの角度をリセット
        if (hammer != null)
        {
            hammer.localRotation = Quaternion.Euler(45, hammer.localEulerAngles.y, hammer.localEulerAngles.z);
        }
    }
    #endregion
}
