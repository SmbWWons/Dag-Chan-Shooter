using UnityEngine;
[RequireComponent(typeof(AudioSource))]
public sealed class DagChanEffectVolume : MonoBehaviour
{
    private AudioSource source;private DagChanAudio audioSettings;private float baseVolume=1;
    /// <summary>
    /// Находит AudioSource своего объекта и общий DagChanAudio.
    /// Эти ссылки используются для пересчёта громкости эффекта.
    /// </summary>
    private void Awake(){source=GetComponent<AudioSource>();audioSettings=DagChanAudio.Instance;}
    /// <summary>
    /// Подписывается на изменение общей громкости эффектов и сразу применяет текущее значение.
    /// Источник не ждёт следующего изменения ползунка.
    /// </summary>
    private void OnEnable(){audioSettings.EffectsVolumeChanged+=Apply;Apply();}
    /// <summary>
    /// Отписывается от события аудиоконтроллера.
    /// Выключенный источник больше не получает обновления громкости.
    /// </summary>
    private void OnDisable(){if(audioSettings)audioSettings.EffectsVolumeChanged-=Apply;}
    /// <summary>
    /// Запоминает собственную громкость эффекта от 0 до 1 и пересчитывает результат.
    /// Это сохраняет различия между громкостью выстрела и реплики ульты.
    /// </summary>
    public void SetBaseVolume(float value){baseVolume=Mathf.Clamp01(value);Apply();}
    /// <summary>
    /// Умножает baseVolume на общую EffectsVolume и записывает результат в AudioSource.
    /// Перед изменением проверяет необходимые ссылки.
    /// </summary>
    private void Apply(){if(source&&audioSettings)source.volume=baseVolume*audioSettings.EffectsVolume;}
}
