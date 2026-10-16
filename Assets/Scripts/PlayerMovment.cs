using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Ходьба и мышь")]
    public float moveSpeed = 5f;
    public float mouseSensitivity = 0.15f;

    [Header("Прыжок и гравитация")]
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Дэш (Q / E / Shift)")]
    public float dashSpeed = 22f;
    public float dashDuration = 0.15f;
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

    void Start()
    {
        controller = GetComponent<CharacterController>();
        controller.radius = radius;

        currentHeight = standHeight;
        currentEye = standEyeHeight;
        ApplyCapsule();

        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (kb == null || mouse == null)
            return;

        float dt = Time.deltaTime;

        // ---------- Вращение камеры ----------
        Vector2 mouseDelta = mouse.delta.ReadValue();
        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Направления считаем только по повороту влево-вправо (yaw)
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = yawRot * Vector3.forward;
        Vector3 right = yawRot * Vector3.right;

        // ---------- Ввод WASD ----------
        Vector3 input = Vector3.zero;
        if (kb.wKey.isPressed) input += forward;
        if (kb.sKey.isPressed) input -= forward;
        if (kb.aKey.isPressed) input -= right;
        if (kb.dKey.isPressed) input += right;
        input = input.normalized;

        // ---------- Земля и прыжок ----------
        bool grounded = controller.isGrounded;
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (grounded && kb.spaceKey.wasPressedThisFrame)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * dt;

        // ---------- Подкат (пока зажат Ctrl) ----------
        bool ctrl = kb.leftCtrlKey.isPressed;

        if (ctrl && !isSliding && grounded)
        {
            isSliding = true;
            slideDir = input.sqrMagnitude > 0.01f ? input : forward;
        }

        if (isSliding && !ctrl && CanStandUp())
            isSliding = false;

        // ---------- Дэш ----------
        dashCooldownTimer -= dt;

        if (dashTimer <= 0f && dashCooldownTimer <= 0f)
        {
            Vector3 d = Vector3.zero;

            if (kb.qKey.wasPressedThisFrame) d = -right;
            else if (kb.eKey.wasPressedThisFrame) d = right;
            else if (kb.leftShiftKey.wasPressedThisFrame) d = forward;

            if (d != Vector3.zero)
            {
                dashDir = d;
                dashTimer = dashDuration;
                dashCooldownTimer = dashCooldown;
            }
        }

        // ---------- Горизонтальная скорость ----------
        Vector3 horizontal;

        if (dashTimer > 0f)
        {
            horizontal = dashDir * dashSpeed;
            dashTimer -= dt;
        }
        else if (isSliding)
        {
            horizontal = slideDir * slideSpeed;
        }
        else
        {
            horizontal = input * moveSpeed;
        }

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

    private void ApplyCapsule()
    {
        controller.height = currentHeight;
        controller.center = new Vector3(0f, currentHeight * 0.5f - currentEye, 0f);
    }

    // Проверка, что над головой есть место, чтобы встать после подката
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
