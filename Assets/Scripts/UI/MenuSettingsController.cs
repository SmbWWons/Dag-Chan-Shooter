using UnityEngine;

/// <summary>Принимает команды меню, изменяет модель, применяет настройки к игровым системам и сохраняет их.</summary>
[DisallowMultipleComponent]
public sealed class MenuSettingsController : MonoBehaviour
{
    private const string SensitivityKey="DagChan.MouseSensitivity", RtxKey="DagChan.Rtx10Fps";
    private PlayerMovement player;
    private MonitorGraphicsMode graphics;
    private DagChanAudio audioSettings;
    private int previousTarget,previousVsync;
    private MenuSettingsModel model;
    public MenuSettingsModel Model { get { Initialize(); return model; } }
    /// <summary>Готовит модель и игровые зависимости до построения меню.</summary>
    private void Awake() { Initialize(); }
    /// <summary>Однократно загружает сохранения и применяет стартовые настройки.</summary>
    private void Initialize()
    {
        if(model!=null)return;
        audioSettings=DagChanAudio.Instance;
        foreach(var candidate in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
            if(!candidate.transform.parent || !candidate.transform.parent.GetComponentInParent<PlayerMovement>(true)){player=candidate;break;}
        graphics=GetComponent<MonitorGraphicsMode>(); if(!graphics)graphics=gameObject.AddComponent<MonitorGraphicsMode>();
        graphics.Configure(player?player.GetComponent<Camera>():Camera.main);
        previousTarget=Application.targetFrameRate==10?-1:Application.targetFrameRate;previousVsync=QualitySettings.vSyncCount;
        model=new MenuSettingsModel(PlayerPrefs.GetFloat(SensitivityKey,player?player.mouseSensitivity:.3f),PlayerPrefs.GetInt(RtxKey,1)!=0,audioSettings.MusicVolume,audioSettings.EffectsVolume,audioSettings.VoiceEnabled);
        SetSensitivity(model.Sensitivity,false);SetRtxMode(model.RtxMode,false);
    }
    /// <summary>Меняет модель, применяет чувствительность к игроку и при необходимости сохраняет её.</summary>
    public void SetSensitivity(float value,bool save=true)
    { Model.SetSensitivity(value);if(player)player.mouseSensitivity=model.Sensitivity;if(save){PlayerPrefs.SetFloat(SensitivityKey,model.Sensitivity);PlayerPrefs.Save();} }
    /// <summary>Применяет визуальный режим и шутливый лимит 10 FPS; при выключении снимает ограничение.</summary>
    public void SetRtxMode(bool value,bool save=true)
    { Model.SetRtxMode(value);if(graphics)graphics.SetMode(value);QualitySettings.vSyncCount=0;Application.targetFrameRate=value?10:-1;if(save){PlayerPrefs.SetInt(RtxKey,value?1:0);PlayerPrefs.Save();} }
    /// <summary>Меняет модель и передаёт громкость аудиосистеме, которая сохраняет значение.</summary>
    public void SetMusicVolume(float value) { Model.SetMusicVolume(value);audioSettings.SetMusicVolume(model.MusicVolume); }
    /// <summary>Применяет громкость существующих и будущих звуковых эффектов.</summary>
    public void SetEffectsVolume(float value) { Model.SetEffectsVolume(value);audioSettings.SetEffectsVolume(model.EffectsVolume); }
    /// <summary>Запрашивает голос с подтверждением; выключение применяется сразу.</summary>
    public void RequestVoiceMusic(bool enabled) { Model.RequestVoice(enabled);audioSettings.SetVoiceEnabled(model.VoiceEnabled); }
    /// <summary>Разрешает голос только после подтверждения открытого предупреждения.</summary>
    public void ConfirmVoiceMusic() { Model.AnswerVoice(true);audioSettings.SetVoiceEnabled(model.VoiceEnabled); }
    /// <summary>Отменяет запрос и выключает голос без остановки музыкальных дорожек.</summary>
    public void CancelVoiceMusic() { Model.AnswerVoice(false);audioSettings.SetVoiceEnabled(model.VoiceEnabled); }
    /// <summary>Передаёт команду продолжения контроллеру паузы.</summary>
    public void Resume() { var pause=GetComponent<MonitorPauseController>();if(pause)pause.Resume(); }
    /// <summary>Сохраняет настройки и завершает игру либо Play Mode.</summary>
    public void ExitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
    /// <summary>Возвращает глобальные параметры кадров после удаления контроллера.</summary>
    private void OnDestroy() { if(model==null)return;Application.targetFrameRate=previousTarget;QualitySettings.vSyncCount=previousVsync; }
}
