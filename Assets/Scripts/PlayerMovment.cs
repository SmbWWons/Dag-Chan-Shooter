using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float mouseSensitivity = 0.15f;

    private float yaw;
    private float pitch;

    void Start()
    {
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard == null || mouse == null)
            return;

        // Вращение камеры мышью
        Vector2 mouseDelta = mouse.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Передвижение WASD
        Vector3 forward = Vector3.ProjectOnPlane(
            transform.forward, Vector3.up
        ).normalized;

        Vector3 right = transform.right;

        Vector3 direction = Vector3.zero;

        if (keyboard.wKey.isPressed) direction += forward;
        if (keyboard.sKey.isPressed) direction -= forward;
        if (keyboard.aKey.isPressed) direction -= right;
        if (keyboard.dKey.isPressed) direction += right;

        transform.position += direction.normalized * moveSpeed * Time.deltaTime;
    }
}