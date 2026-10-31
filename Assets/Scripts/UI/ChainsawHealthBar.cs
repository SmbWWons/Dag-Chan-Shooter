using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ChainsawHealthBar : MonoBehaviour
{
    [SerializeField] private HealthController health;
    public int MaxCells => maxCells;
    [Header("UI")]
    [SerializeField] private Image greenFill;
    [SerializeField] private Image ultimateFill;
    [SerializeField] private Color ultimateChargingColor = new Color(1f, .05f, .09f, 1f);
    [SerializeField] private Color ultimateReadyColor = new Color(0f, 1f, .35f, 1f);
    public Image UltimateFill => ultimateFill;
    [SerializeField] private Image portraitImage;

    [Header("Портреты (от здорового к избитому)")]
    [SerializeField] private Sprite[] portraits;

    [Header("Помехи при смене портрета")]
    [SerializeField] private Sprite[] noiseFrames;
    [SerializeField] private float noiseDuration = 0.5f;

    [Header("Настройки")]
    [SerializeField] private int maxCells = 15;


    private int currentCells;
    private int currentPortraitIndex = -1;
    private Coroutine noiseRoutine;

    private HealthModel healthModel;
    private UltimateModel ultimateModel;
    /// <summary>Подключает модели игрока; интерфейс не изменяет игровые данные.</summary>
    private void Start()
    {
        if (!health) { var player = FindFirstObjectByType<PlayerMovement>(); if (player) health = player.GetComponent<HealthController>(); }
        if (health) BindHealth(health); else SetCells(maxCells, true);
    }
    /// <summary>Сохраняет назначение игрока и связывает представление с моделями здоровья и ульты.</summary>
    public void BindHealth(HealthController value)
    {
        Unsubscribe(); health = value;
        healthModel = health ? health.Model : null;
        var ultimate = health ? health.GetComponent<UltimateAttackController>() : null;
        ultimateModel = ultimate ? ultimate.Model : null;
        if (isActiveAndEnabled) Subscribe();
    }
    /// <summary>Подписывает представление на данные и сразу рисует их актуальные значения.</summary>
    private void Subscribe()
    {
        Unsubscribe();
        if (healthModel != null) { healthModel.Changed += OnHealthChanged; maxCells = healthModel.MaxHealth; SetCells(healthModel.CurrentHealth, true); }
        if (ultimateModel != null) { ultimateModel.Changed += OnUltimateChanged; OnUltimateChanged(ultimateModel.Charge, ultimateModel.Status); }
    }
    /// <summary>Снимает подписки без изменения модели или её состояния.</summary>
    private void Unsubscribe()
    {
        if (healthModel != null) healthModel.Changed -= OnHealthChanged;
        if (ultimateModel != null) ultimateModel.Changed -= OnUltimateChanged;
    }
    /// <summary>Восстанавливает отображение после повторного включения Canvas.</summary>
    private void OnEnable() { if (health) BindHealth(health); }
    /// <summary>Останавливает получение событий выключенным представлением.</summary>
    private void OnDisable() { Unsubscribe(); }
    /// <summary>Рисует заряд ульты красным, а полную готовность зелёным.</summary>
    private void OnUltimateChanged(float charge, UltimateBarStatus status)
    {
        if (!ultimateFill) return;
        ultimateFill.fillAmount = charge;
        ultimateFill.color = status == UltimateBarStatus.Ready ? ultimateReadyColor : ultimateChargingColor;
    }
    /// <summary>Обновляет представление ХП по событию модели.</summary>
    private void OnHealthChanged(int current, int maximum) { maxCells = maximum; SetCells(current); }
    /// <summary>
    /// Ограничивает число ячеек и вычисляет долю заполнения от максимального здоровья.
    /// Обновляет зелёную заливку и портрет с учётом запроса мгновенного отображения.
    /// </summary>
    public void SetCells(int cells, bool instant = false)
    {
        currentCells = Mathf.Clamp(cells, 0, maxCells);
        float fraction = (float)currentCells / maxCells;

        if (greenFill) greenFill.fillAmount = fraction;
        UpdatePortrait(fraction, instant);
    }

    /// <summary>
    /// Выбирает портрет по доле потерянных ХП и не переключает уже показанный вариант.
    /// Останавливает прежние помехи, затем меняет спрайт сразу либо запускает PlayNoise.
    /// </summary>
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

    /// <summary>
    /// Показывает последовательность кадров помех в течение noiseDuration и затем целевой портрет.
    /// Использует независящее от паузы время, поэтому переход может завершиться при остановленной игре.
    /// </summary>
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

}
