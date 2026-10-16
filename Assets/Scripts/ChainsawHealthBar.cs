using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ChainsawHealthBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image greenFill;

    [Header("Настройки")]
    [SerializeField] private int maxCells = 15;

    [Header("Клавиши для теста")]
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private Key damageKey = Key.Minus;   // нажать: -1 ячейка
    [SerializeField] private Key healKey = Key.Equals;    // нажать: +1 ячейка
#else
    [SerializeField] private KeyCode damageKey = KeyCode.Minus;
    [SerializeField] private KeyCode healKey = KeyCode.Equals;
#endif

    private int currentCells;

    private void Start()
    {
        SetCells(maxCells);
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb[damageKey].wasPressedThisFrame) TakeDamage();
        if (kb[healKey].wasPressedThisFrame) Heal();
#else
        if (Input.GetKeyDown(damageKey)) TakeDamage();
        if (Input.GetKeyDown(healKey)) Heal();
#endif
    }

    public void SetCells(int cells)
    {
        currentCells = Mathf.Clamp(cells, 0, maxCells);
        greenFill.fillAmount = (float)currentCells / maxCells;
    }

    // Можно вызывать из других скриптов или из UI-кнопок
    public void TakeDamage(int cellsLost = 1)
    {
        SetCells(currentCells - cellsLost);
    }

    public void Heal(int cellsGained = 1)
    {
        SetCells(currentCells + cellsGained);
    }
}