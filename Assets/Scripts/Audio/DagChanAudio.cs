using System;
using UnityEngine;

[DefaultExecutionOrder(-21000)]
public sealed class DagChanAudio : MonoBehaviour
{
    private const string MusicKey="DagChan.MusicVolume", EffectsKey="DagChan.EffectsVolume";
    private static DagChanAudio instance;
    private AudioSource music,voice;
    private float musicDuck=1f;
    /// <summary>
    /// Возвращает единый аудиоконтроллер. При первом обращении создаёт постоянный объект; Awake настроит источники и запустит музыку.
    /// </summary>
    public static DagChanAudio Instance
    {
        get { if(!instance){var go=new GameObject("DagChan Audio");DontDestroyOnLoad(go);go.AddComponent<DagChanAudio>();}return instance; }
    }
    public float MusicVolume {get;private set;}
    public float EffectsVolume {get;private set;}
    public bool VoiceEnabled {get;private set;}
    public AudioSource MusicSource=>music;
    public AudioSource VoiceSource=>voice;
    public event Action EffectsVolumeChanged;
    /// <summary>
    /// Сбрасывает статическую ссылку перед новым запуском игры.
    /// Это не даёт использовать объект прошлого Play Mode при отключённой перезагрузке домена.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState(){instance=null;}
    /// <summary>
    /// Через Instance создаёт общий аудиоконтроллер до загрузки сцены.
    /// Музыка поэтому не зависит от отдельного объекта в сцене.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize(){_ = Instance;}
    /// <summary>
    /// Не допускает второй экземпляр, загружает сохранённую громкость и создаёт оба музыкальных источника.
    /// Голос сначала заглушён; дорожки получают одинаковое время запуска по аудиочасам DSP и продолжают играть в меню.
    /// </summary>
    private void Awake()
    {
        if(instance&&instance!=this){Destroy(gameObject);return;}instance=this;
        MusicVolume=Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey,.5f));EffectsVolume=Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsKey,1));
        VoiceEnabled=false;
        music=CreateSource("Music","AudioMusic/DagChan_Aquapark_Music");voice=CreateSource("Voice","AudioMusic/DagChan_Aquapark_Voice");ApplyMusicVolumes();
        if(music.clip&&voice.clip){double start=AudioSettings.dspTime+.3;music.PlayScheduled(start);voice.PlayScheduled(start);}
    }
    /// <summary>
    /// Создаёт дочерний двумерный AudioSource и загружает клип по пути внутри Resources.
    /// Включает зацикливание и игнорирование паузы слушателя; при отсутствии дорожки пишет ошибку.
    /// </summary>
    private AudioSource CreateSource(string label,string path)
    {
        var go=new GameObject(label);go.transform.SetParent(transform,false);var source=go.AddComponent<AudioSource>();
        source.playOnAwake=false;source.loop=true;source.spatialBlend=0;source.pitch=1;source.priority=32;source.ignoreListenerPause=true;
        source.clip=Resources.Load<AudioClip>(path);if(!source.clip)Debug.LogError("Missing music stem: "+path);return source;
    }
    /// <summary>
    /// Ограничивает громкость диапазоном от 0 до 1 и применяет её к обеим музыкальным дорожкам.
    /// Сохраняет значение в PlayerPrefs для следующего запуска.
    /// </summary>
    public void SetMusicVolume(float value){MusicVolume=Mathf.Clamp01(value);ApplyMusicVolumes();PlayerPrefs.SetFloat(MusicKey,MusicVolume);PlayerPrefs.Save();}
    /// <summary>
    /// Обновляет общую громкость эффектов и уведомляет подписанные источники событием EffectsVolumeChanged.
    /// Сохраняет настройку, которую используют новые и уже созданные эффекты.
    /// </summary>
    public void SetEffectsVolume(float value){EffectsVolume=Mathf.Clamp01(value);EffectsVolumeChanged?.Invoke();PlayerPrefs.SetFloat(EffectsKey,EffectsVolume);PlayerPrefs.Save();}
    // The menu calls this only after the user confirms its warning.
    /// <summary>
    /// Переключает слышимость голосовой дорожки и пересчитывает громкости без перезапуска песни.
    /// Меню вызывает метод после подтверждения предупреждения или для отключения голоса.
    /// </summary>
    public void SetVoiceEnabled(bool value){VoiceEnabled=value;ApplyMusicVolumes();}
    /// <summary>
    /// Выставляет инструменталу MusicVolume, а голосу — такую же громкость либо ноль.
    /// Источники продолжают играть синхронно независимо от слышимости; временное приглушение смерти применяется отдельно от настройки громкости.
    /// </summary>
    private void ApplyMusicVolumes(){if(music)music.volume=MusicVolume*musicDuck;if(voice)voice.volume=VoiceEnabled?MusicVolume*musicDuck:0;}
    /// <summary>
    /// Временно приглушает обе музыкальные дорожки, не меняя сохранённую громкость и положение воспроизведения.
    /// Последовательность смерти плавно уменьшает множитель до нуля, а новый игрок возвращает его к единице.
    /// </summary>
    public void SetMusicDuck(float value){musicDuck=Mathf.Clamp01(value);ApplyMusicVolumes();}
    /// <summary>
    /// Очищает статическую ссылку, если удаляется текущий общий контроллер.
    /// Последующее обращение к Instance сможет создать новый объект.
    /// </summary>
    private void OnDestroy(){if(instance==this)instance=null;}
}
