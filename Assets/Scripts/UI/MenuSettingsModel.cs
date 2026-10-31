using System;

/// <summary>Чистые данные настроек меню. Не знает о ползунках, игроке, звуке и PlayerPrefs.</summary>
public sealed class MenuSettingsModel
{
    public float Sensitivity { get; private set; }
    public bool RtxMode { get; private set; }
    public float MusicVolume { get; private set; }
    public float EffectsVolume { get; private set; }
    public bool VoiceEnabled { get; private set; }
    public bool VoiceWarningVisible { get; private set; }
    public event Action Changed;
    /// <summary>Задаёт начальные значения из сохранений, подготовленных контроллером.</summary>
    public MenuSettingsModel(float sensitivity, bool rtx, float music, float effects, bool voice)
    { Sensitivity = Clamp(sensitivity,.05f,1.5f); RtxMode = rtx; MusicVolume = Clamp(music,0,1); EffectsVolume = Clamp(effects,0,1); VoiceEnabled = voice; }
    /// <summary>Ограничивает число допустимым диапазоном.</summary>
    private static float Clamp(float value,float min,float max) => float.IsNaN(value) ? min : Math.Max(min, Math.Min(max,value));
    /// <summary>Меняет чувствительность и уведомляет представление.</summary>
    public void SetSensitivity(float value) { Sensitivity=Clamp(value,.05f,1.5f); Changed?.Invoke(); }
    /// <summary>Меняет флаг графического режима.</summary>
    public void SetRtxMode(bool value) { RtxMode=value; Changed?.Invoke(); }
    /// <summary>Меняет громкость музыки.</summary>
    public void SetMusicVolume(float value) { MusicVolume=Clamp(value,0,1); Changed?.Invoke(); }
    /// <summary>Меняет громкость эффектов.</summary>
    public void SetEffectsVolume(float value) { EffectsVolume=Clamp(value,0,1); Changed?.Invoke(); }
    /// <summary>Открывает предупреждение перед включением голоса либо сразу выключает голос.</summary>
    public void RequestVoice(bool enabled) { if(!enabled) { VoiceEnabled=false; VoiceWarningVisible=false; } else if(!VoiceEnabled) VoiceWarningVisible=true; Changed?.Invoke(); }
    /// <summary>Принимает ответ на предупреждение; подтверждение без открытого окна игнорируется.</summary>
    public void AnswerVoice(bool accept) { if(accept && !VoiceWarningVisible)return; VoiceEnabled=accept; VoiceWarningVisible=false; Changed?.Invoke(); }
}
