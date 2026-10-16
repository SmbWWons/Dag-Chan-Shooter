using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ChainsawHealthBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image greenFill;
    [SerializeField] private Image portraitImage;

    [Header("Портреты (от здорового к избитому)")]
    [SerializeField] private Sprite[] portraits;

    [Header("Помехи при смене портрета")]
    [SerializeField] private Sprite[] noiseFrames;
    [SerializeField] private float noiseDuration = 0.5f;

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
    private int currentPortraitIndex = -1;
    private Coroutine noiseRoutine;

    private void Start()
    {
        SetCells(maxCells, true);
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

    public void SetCells(int cells, bool instant = false)
    {
        currentCells = Mathf.Clamp(cells, 0, maxCells);
        float fraction = (float)currentCells / maxCells;

        greenFill.fillAmount = fraction;
        UpdatePortrait(fraction, instant);
    }

    private void UpdatePortrait(float fraction, bool instant)
    {
        if (portraitImage == null || portraits == null || portraits.Length == 0) return;

        // fraction = 1 -> первый спрайт, fraction = 0 -> последний
        int index = Mathf.FloorToInt((1f - fraction) * portraits.Length);
        index = Mathf.Clamp(index, 0, portraits.Length - 1);

        // Портрет не поменялся: помехи не нужны
        if (index == currentPortraitIndex) return;
        currentPortraitIndex = index;

        if (noiseRoutine != null)
        {
            StopCoroutine(noiseRoutine);
            noiseRoutine = null;
        }

        bool noNoise = noiseFrames == null || noiseFrames.Length == 0 || noiseDuration <= 0f;

        if (instant || noNoise || !isActiveAndEnabled)
        {
            portraitImage.sprite = portraits[index];
        }
        else
        {
            noiseRoutine = StartCoroutine(PlayNoise(portraits[index]));
        }
    }

    private IEnumerator PlayNoise(Sprite target)
    {
        float timer = 0f;

        while (timer < noiseDuration)
        {
            int frame = Mathf.FloorToInt(timer / noiseDuration * noiseFrames.Length);
            frame = Mathf.Clamp(frame, 0, noiseFrames.Length - 1);

            portraitImage.sprite = noiseFrames[frame];

            timer += Time.unscaledDeltaTime; // работает и на паузе (timeScale = 0)
            yield return null;
        }

        portraitImage.sprite = target;
        noiseRoutine = null;
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