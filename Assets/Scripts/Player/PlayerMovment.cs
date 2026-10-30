using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Ходьба и мышь")]
    public float moveSpeed = 5f;
    public float mouseSensitivity = 0.15f;

    [Header("Прыжок и гравитация")]
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Рывок (Shift)")]
    public float dashSpeed = 65f;
    public float dashDuration = 0.16f;
    public float dashCooldown = 0.6f;

    [Header("Подкат (Ctrl)")]
    public float slideSpeed = 12f;

    [Header("Капсула игрока")]
    public float radius = 0.35f;
    public float standHeight = 1.8f;
    public float slideHeight = 0.9f;
    [Tooltip("Расстояние от камеры до пола, когда игрок стоит")]
    public float standEyeHeight = 1.6f;
    [Tooltip("Расстояние от камеры до пола во время подката")]
    public float slideEyeHeight = 0.7f;
    public float heightChangeSpeed = 12f;

    private CharacterController controller;

    private float yaw;
    private float pitch;

    private float verticalVelocity;

    private bool isSliding;
    private Vector3 slideDir;

    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDir;

    private float currentHeight;
    private float currentEye;

    /// <summary>
    /// Проверяет, не находится ли компонент на дочерней камере уже существующего игрока.
    /// В таком случае отключает повторное управление и CharacterController, чтобы они не конкурировали с основным игроком.
    /// </summary>
    private void Awake()
    {
        // A cloned child camera is a renderer, not a second player.
        if(transform.parent && transform.parent.GetComponentInParent<PlayerMovement>(true))
        {
            enabled=false;
            var duplicate=GetComponent<CharacterController>();if(duplicate)duplicate.enabled=false;
        }
    }

    /// <summary>
    /// Получает контроллер столкновений, задаёт начальную капсулу и высоту глаз.
    /// Читает исходный поворот в yaw и pitch и захватывает скрытый курсор.
    /// </summary>
    void Start()
    {
        controller = GetComponent<CharacterController>();
        controller.radius = radius;
        controller.minMoveDistance = 0;

        currentHeight = standHeight;
        currentEye = standEyeHeight;
        ApplyCapsule();

        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// При разрешённом вводе последовательно рассчитывает обзор, ходьбу, прыжок с гравитацией, подкат и рывок.
    /// Плавно меняет высоту глаз и капсулы, сохраняя положение ног, и передаёт итог в CharacterController.Move.
    /// В меню выполнение прекращается через InputBlocked.
    /// </summary>
    void Update()
    {
        if (MonitorPauseController.InputBlocked) return;
        float dt = Time.deltaTime;
        // Контакт прошлого Move сохраняем до пересоздания формы при смене локального центра.
        bool grounded = controller.isGrounded;

        // ---------- Вращение камеры ----------
        Vector2 mouseDelta = DagChanInput.LookDelta;
        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        // Центр CharacterController задан локально и иначе уезжает вперёд/назад вместе с наклоном камеры.
        // Исправляем его до grounded и Move, чтобы столкновения уже использовали капсулу строго под глазами.
        ApplyCapsule();

        // Направления считаем только по повороту влево-вправо (yaw)
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = yawRot * Vector3.forward;
        Vector3 right = yawRot * Vector3.right;

        // ---------- Ввод WASD ----------
        Vector2 moveInput=DagChanInput.Move;
        Vector3 input=Vector3.ClampMagnitude(forward*moveInput.y+right*moveInput.x,1f);

        // ---------- Земля и прыжок ----------
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (grounded && DagChanInput.JumpPressed)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * dt;

        // ---------- Подкат (пока зажат Ctrl) ----------
        bool ctrl = DagChanInput.SlideHeld;

        if (ctrl && !isSliding && grounded)
        {
            isSliding = true;
            slideDir = input.sqrMagnitude > 0.01f ? input : forward;
        }

        if (isSliding && !ctrl && CanStandUp())
            isSliding = false;

        // ---------- Дэш ----------
        dashCooldownTimer -= dt;

        bool directionalDash = Mathf.Abs(moveInput.x)>.01f || moveInput.y<-.01f;
        if (DagChanInput.DashPressed)
            TryDash(ResolveDashDirection(transform.forward, input, directionalDash));

        // Integrate the burst exactly over this frame, including its short braking tail.
        Vector3 horizontal;
        if (dashTimer > 0f)
        {
            float activeTime = Mathf.Min(dt, dashTimer);
            float elapsed = dashDuration - dashTimer;
            float distance = dashSpeed * dashDuration *
                (DashDistanceFraction((elapsed + activeTime) / dashDuration) -
                 DashDistanceFraction(elapsed / dashDuration));
            horizontal = dashDir * (distance / Mathf.Max(dt, .000001f));
            dashTimer = Mathf.Max(0f, dashTimer - dt);
        }
        else if (isSliding) horizontal = slideDir * slideSpeed;
        else horizontal = input * moveSpeed;

        // ---------- Высота капсулы и камеры ----------
        float targetHeight = isSliding ? slideHeight : standHeight;
        float targetEye = isSliding ? slideEyeHeight : standEyeHeight;
        float t = 1f - Mathf.Exp(-heightChangeSpeed * dt);

        float newHeight = Mathf.Lerp(currentHeight, targetHeight, t);
        float newEye = Mathf.Lerp(currentEye, targetEye, t);
        float eyeDelta = newEye - currentEye;

        Vector3 move = (horizontal + Vector3.up * verticalVelocity) * dt;
        move += Vector3.up * eyeDelta; // ноги остаются на месте, камера опускается/поднимается

        currentHeight = newHeight;
        currentEye = newEye;

        if (eyeDelta < 0f)
        {
            // Приседаем: сначала уменьшаем капсулу, потом двигаем
            ApplyCapsule();
            controller.Move(move);
        }
        else
        {
            // Встаём: сначала двигаем, потом увеличиваем капсулу
            controller.Move(move);
            ApplyCapsule();
        }
    }

    /// <summary>
    /// При боковом или обратном вводе возвращает нормализованное направление движения по горизонтали.
    /// Иначе использует полный вектор взгляда камеры, включая наклон вверх или вниз.
    /// </summary>
    public static Vector3 ResolveDashDirection(Vector3 cameraForward, Vector3 movement, bool directional)
    {
        if (directional && movement.sqrMagnitude > .0001f) return movement.normalized;
        return cameraForward.normalized;
    }

    // Constant peak speed for 75% of the burst, then a quick stop; no lingering drift.
    /// <summary>
    /// Переводит долю времени рывка в долю пройденной дистанции.
    /// Первые 75 процентов времени скорость постоянна, затем резко снижается; разница значений позволяет интегрировать движение независимо от частоты кадров.
    /// </summary>
    public static float DashDistanceFraction(float progress)
    {
        float t = Mathf.Clamp01(progress);
        if (t <= .75f) return t;
        float tail = t - .75f;
        return .75f + tail - 2f * tail * tail;
    }

    /// <summary>
    /// Проверяет паузу, активность, текущий рывок, задержку повтора и ненулевое направление.
    /// При успехе запоминает направление, запускает два таймера и возвращает true.
    /// </summary>
    public bool TryDash(Vector3 direction)
    {
        if (MonitorPauseController.InputBlocked || !isActiveAndEnabled || dashTimer > 0f || dashCooldownTimer > 0f || direction.sqrMagnitude < .0001f)
            return false;
        dashDir = direction.normalized;
        dashTimer = Mathf.Max(.01f, dashDuration);
        dashCooldownTimer = dashCooldown;
        return true;
    }

    /// <summary>
    /// Вычисляет высоту и центр вертикальной капсулы относительно глаз в мировых координатах.
    /// Обратное преобразование компенсирует наклон общей с камерой Transform: камера остаётся внутри защиты от стен.
    /// Изменяет свойства CharacterController только при заметной разнице, избегая постоянного пересоздания формы столкновений.
    /// </summary>
    private void ApplyCapsule()
    {
        // Changing these native properties recreates the controller. Do so only when crouching changes its shape.
        if(Mathf.Abs(controller.height-currentHeight)>.0001f)controller.height=currentHeight;
        // PhysX держит капсулу CharacterController вертикально, но её локальный center вращается с Transform.
        // Нужный мировой сдвиг вниз переводим обратно в локальные координаты текущего поворота камеры.
        var center=transform.InverseTransformVector(Vector3.up*(currentHeight*.5f-currentEye));
        if((controller.center-center).sqrMagnitude>.00000001f)controller.center=center;
    }

    // Проверка, что над головой есть место, чтобы встать после подката
    /// <summary>
    /// Проверяет лучом вверх свободное место для перехода от текущей высоты к стоячей.
    /// Игнорирует триггеры и возвращает true, если вставанию не мешает препятствие.
    /// </summary>
    private bool CanStandUp()
    {
        Vector3 top = transform.position + Vector3.up * (currentHeight - currentEye);
        float distance = standHeight - currentHeight + 0.05f;

        return !Physics.Raycast(
            top + Vector3.up * 0.05f,
            Vector3.up,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );
    }
}

