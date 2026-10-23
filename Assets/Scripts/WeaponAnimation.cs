using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class WeaponAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float shotInterval = 0.3f;

    private float nextShotTime;

    private static readonly int FireState =
        Animator.StringToHash("Base Layer.Fire");

    private void Update()
    {
        bool clicked = false;

#if ENABLE_INPUT_SYSTEM
        clicked = Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        clicked = Input.GetMouseButtonDown(0);
#endif

        if (clicked && animator != null && Time.time >= nextShotTime)
        {
            animator.Play(FireState, 0, 0f);
            nextShotTime = Time.time + shotInterval;
        }
    }
}