using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
[InitializeOnLoad]
public static class AudioMenuVerification
{
    const string Marker="Assets/Scripts/Editor/Project/audio-menu-pending.txt",Flag="DagChan.AudioMenuVerify";
    static string Report=>Application.dataPath+"/../Logs/AudioMenuVerification.txt";
    static int step;static double next;static MonitorMenuSettings menu;static DagChanAudio audio;static int samples;static float oldMusic,oldEffects;
    /// <summary>
    /// Регистрирует обработчик запросов и входа в Play Mode.
    /// После запрошенного старта тестов ждёт создания меню и музыкальных источников.
    /// </summary>
    static AudioMenuVerification(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Flag,false)){SessionState.SetBool(Flag,false);step=1;next=EditorApplication.timeSinceStartup+3;}};}
    /// <summary>
    /// Пишет PASS или FAIL с пояснением в отчёт.
    /// При провале выбрасывает исключение, чтобы тест не продолжался с неверными условиями.
    /// </summary>
    static void Check(bool ok,string text){File.AppendAllText(Report,(ok?"PASS: ":"FAIL: ")+text+"\n");if(!ok)throw new Exception(text);}
    /// <summary>
    /// Находит единственную активную кнопку с заданным именем.
    /// Тест затем вызывает её настоящий обработчик, проверяя связь интерфейса с подтверждением или отказом.
    /// </summary>
    static Button WarningButton(string name)=>UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name==name);
    /// <summary>
    /// По запросу настраивает импорт дорожек и проверяет одинаковую длину и частоту, затем запускает игру.
    /// Проверяет начальное отключение голоса, музыку на паузе, независимые громкости, предупреждение, обе кнопки и синхронизацию.
    /// В конце возвращает громкости и останавливает тест; при ошибке также пишет отчёт и выполняет очистку.
    /// </summary>
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try{
            if(File.Exists(Marker)&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(Marker);File.WriteAllText(Report,"");
                foreach(var path in new[]{"Assets/Audio/DagChan/Music/Resources/AudioMusic/DagChan_Aquapark_Music.mp3","Assets/Audio/DagChan/Music/Resources/AudioMusic/DagChan_Aquapark_Voice.mp3"})
                {
                    AssetDatabase.ImportAsset(path);var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.8f;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.preloadAudioData=true;importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.loadInBackground=false;importer.SaveAndReimport();
                }
                var music=Resources.Load<AudioClip>("AudioMusic/DagChan_Aquapark_Music");var voice=Resources.Load<AudioClip>("AudioMusic/DagChan_Aquapark_Voice");
                Check(music&&voice,"both supplied music stems import");Check(music.frequency==voice.frequency&&music.samples==voice.samples,"stems have matching sample rate and loop length: "+music.length+" seconds");
                Check(Resources.Load<Texture2D>("AudioMenu/VoiceWarning"),"supplied warning artwork imports");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
            }
            if(step==0||EditorApplication.timeSinceStartup<next)return;
            if(step==1)
            {
                menu=UnityEngine.Object.FindFirstObjectByType<MonitorMenuSettings>();audio=DagChanAudio.Instance;oldMusic=audio.MusicVolume;oldEffects=audio.EffectsVolume;
                Check(menu.MusicSlider&&menu.EffectsSlider&&menu.VoiceToggle,"right monitor has music/effects sliders and voice toggle");
                Check(!audio.VoiceEnabled&&!menu.VoiceToggle.isOn&&audio.VoiceSource.volume==0,"voice starts muted and unchecked");
                Check(audio.MusicSource.isPlaying&&audio.VoiceSource.isPlaying&&audio.MusicSource.loop&&audio.VoiceSource.loop,"both stems play and loop together");
                samples=audio.MusicSource.timeSamples;Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;step=2;next=EditorApplication.timeSinceStartup+.4;
            }
            else if(step==2)
            {
                Check(audio.MusicSource.timeSamples>samples,"music continues through listener pause in menu");
                menu.MusicSlider.value=.3f;Check(Mathf.Abs(audio.MusicSource.volume-.3f)<.001f&&audio.VoiceSource.volume==0,"music slider adjusts music while vocal remains muted");
                menu.EffectsSlider.value=.4f;var go=new GameObject("Audio gain verification",typeof(AudioSource));var gain=go.AddComponent<DagChanEffectVolume>();gain.SetBaseVolume(.65f);Check(Mathf.Abs(go.GetComponent<AudioSource>().volume-.26f)<.001f,"effects gain multiplies original shot volume");menu.EffectsSlider.value=0;Check(go.GetComponent<AudioSource>().volume==0,"effects slider immediately mutes existing sources");UnityEngine.Object.Destroy(go);
                menu.VoiceToggle.isOn=true;Check(menu.VoiceWarningVisible&&!menu.VoiceToggle.isOn&&!audio.VoiceEnabled&&audio.VoiceSource.volume==0,"request shows warning without enabling voice");WarningButton("Отказаться").onClick.Invoke();Check(!menu.VoiceWarningVisible&&!audio.VoiceEnabled&&!menu.VoiceToggle.isOn,"refusing warning leaves voice off");
                menu.VoiceToggle.isOn=true;WarningButton("Подтвердить").onClick.Invoke();Check(audio.VoiceEnabled&&menu.VoiceToggle.isOn&&!menu.VoiceWarningVisible&&Mathf.Abs(audio.VoiceSource.volume-.3f)<.001f,"confirmation enables checkbox and vocal layer");
                Check(Mathf.Abs((float)audio.MusicSource.timeSamples/audio.MusicSource.clip.frequency-(float)audio.VoiceSource.timeSamples/audio.VoiceSource.clip.frequency)<.04f,"stems remain synchronized after enabling voice");
                menu.MusicSlider.value=0;Check(audio.MusicSource.volume==0&&audio.VoiceSource.volume==0,"music zero silences both layers independently of effects");menu.MusicSlider.value=.3f;menu.VoiceToggle.isOn=false;Check(!audio.VoiceEnabled&&audio.VoiceSource.volume==0,"unchecking voice mutes it without stopping track");
                audio.SetMusicVolume(oldMusic);audio.SetEffectsVolume(oldEffects);File.AppendAllText(Report,"ALL_TESTS_PASSED\n");step=0;EditorApplication.isPlaying=false;
            }
        }catch(Exception e){File.AppendAllText(Report,e+"\n");Debug.LogException(e);if(audio){audio.SetMusicVolume(oldMusic);audio.SetEffectsVolume(oldEffects);audio.SetVoiceEnabled(false);}step=0;if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;}
    }
}

