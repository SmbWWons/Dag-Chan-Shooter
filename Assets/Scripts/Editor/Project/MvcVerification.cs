using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Проверка разделения MVC и связей существующей сцены; запускается только явно через Run.</summary>
[InitializeOnLoad]
public static class MvcVerification
{
    private const string Flag="DagChan.MvcVerification";
    private static double deadline;
    private static string Report => Application.dataPath+"/../Logs/MvcVerification.txt";
    /// <summary>После перехода в Play Mode откладывает проверку до создания интерфейса.</summary>
    static MvcVerification()
    {
        EditorApplication.playModeStateChanged+=state=> { if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Flag,false))deadline=EditorApplication.timeSinceStartup+4; };
        EditorApplication.update+=Tick;
    }
    /// <summary>Записывает утверждение и прерывает проверку при ошибке.</summary>
    private static void Check(bool ok,string text) { File.AppendAllText(Report,(ok?"PASS: ":"FAIL: ")+text+"\n");if(!ok)throw new Exception(text); }
    /// <summary>Проверяет чистые модели и запускает игровую сцену для проверки связей MVC.</summary>
    public static void Run()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Report));File.WriteAllText(Report,"");
        try
        {
            var hp=new HealthModel(2);int changes=0,deaths=0;hp.Changed+=(a,b)=>changes++;hp.Died+=()=>deaths++;
            Check(hp.CurrentHealth==2 && hp.TakeDamage(1) && hp.CurrentHealth==1,"health model applies one hit");
            hp.Heal(10);Check(hp.CurrentHealth==2,"healing clamps to maximum");
            Check(hp.TakeDamage(3) && hp.IsDead && hp.CurrentHealth==0 && deaths==1,"lethal damage emits one death");
            hp.Heal(1);Check(!hp.TakeDamage(1) && hp.IsDead && deaths==1 && changes==3,"dead model ignores healing and damage");
            var ult=new UltimateModel();int notifications=0;ult.Changed+=(a,b)=>notifications++;
            ult.SetStatus(.4f,UltimateBarStatus.Active);ult.SetStatus(.4f,UltimateBarStatus.Active);
            Check(notifications==1 && ult.Charge==.4f,"ultimate notifications are not duplicated");
            ult.SetStatus(2,UltimateBarStatus.Ready);Check(ult.Charge==1,"ultimate charge clamps to one");
            var menu=new MenuSettingsModel(.3f,true,.5f,1,false);menu.RequestVoice(true);
            Check(menu.VoiceWarningVisible && !menu.VoiceEnabled,"voice stays muted until confirmation");
            menu.AnswerVoice(true);Check(menu.VoiceEnabled && !menu.VoiceWarningVisible,"voice confirmation updates model");
            menu.RequestVoice(false);menu.AnswerVoice(true);Check(!menu.VoiceEnabled,"cannot confirm closed warning");
            menu.SetSensitivity(100);menu.SetMusicVolume(-5);Check(menu.Sensitivity==1.5f && menu.MusicVolume==0,"settings clamp values");
            EditorSceneManager.OpenScene("Assets/Scenes/DagChan_Level_01.unity");
            SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
        }
        catch(Exception ex) { File.AppendAllText(Report,ex+"\n");EditorApplication.Exit(1); }
    }
    /// <summary>Проверяет UI, контроллеры, порядок событий попадания и восстановление подписок.</summary>
    private static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||deadline==0||EditorApplication.timeSinceStartup<deadline)return;
        SessionState.SetBool(Flag,false);
        try
        {
            var hud=UnityEngine.Object.FindFirstObjectByType<ChainsawHealthBar>();
            var settings=UnityEngine.Object.FindFirstObjectByType<MonitorMenuSettings>();
            Check(hud && settings,"existing scene creates HUD and monitor menu");
            // Batch Mode does not resume WaitForEndOfFrame used by the screenshot menu transition.
            // Build its existing View explicitly so the same controls and listeners can be tested.
            if(!settings.MusicSlider)settings.Build(new GameObject("MVC menu canvas",typeof(Canvas)).GetComponent<Canvas>());
            var hp=(HealthController)typeof(ChainsawHealthBar).GetField("health",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hud);
            var fill=(Image)typeof(ChainsawHealthBar).GetField("greenFill",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hud);
            Check(hp && hp.CurrentHealth==hp.MaxHealth,"player has full model health");
            hp.TakeDamage(1);Check(Math.Abs(fill.fillAmount-(float)hp.CurrentHealth/hp.MaxHealth)<.001f,"HUD observes health model damage");hp.Heal(1);
            Check(fill.fillAmount==1,"HUD observes healing");
            hud.enabled=false;hp.TakeDamage(1);hud.enabled=true;
            Check(Math.Abs(fill.fillAmount-(float)hp.CurrentHealth/hp.MaxHealth)<.001f,"HUD rebinds after disable without stale values");hp.Heal(1);
            var ultimate=hp.GetComponent<UltimateAttackController>();ultimate.Model.SetStatus(.37f,UltimateBarStatus.Active);
            Check(Math.Abs(hud.UltimateFill.fillAmount-.37f)<.001f && hud.UltimateFill.color.r>.9f,"HUD observes active ultimate model in red");
            ultimate.Model.SetStatus(1,UltimateBarStatus.Ready);Check(hud.UltimateFill.fillAmount==1 && hud.UltimateFill.color.g>.9f,"ready ultimate is full and green");
            var control=settings.GetComponent<MenuSettingsController>();float sensitivity=settings.Sensitivity,music=settings.MusicSlider.value,effects=settings.EffectsSlider.value;bool rtx=settings.RtxMode;
            control.SetSensitivity(.75f,false);Check(settings.SensitivitySlider.value==.75f && hp.GetComponent<PlayerMovement>().mouseSensitivity==.75f,"sensitivity controller updates player and view");
            control.SetRtxMode(false,false);Check(Application.targetFrameRate==-1 && !settings.RtxToggle.isOn,"RTX off removes FPS cap and updates view");
            control.SetRtxMode(true,false);Check(Application.targetFrameRate==10 && settings.RtxToggle.isOn,"RTX on retains 10 FPS joke");
            settings.MusicSlider.value=.23f;settings.EffectsSlider.value=.42f;
            Check(Math.Abs(DagChanAudio.Instance.MusicVolume-.23f)<.001f && Math.Abs(DagChanAudio.Instance.EffectsVolume-.42f)<.001f,"UI input routes audio changes through controller");
            settings.RequestVoiceMusic(true);Check(settings.VoiceWarningVisible && !DagChanAudio.Instance.VoiceEnabled,"voice request opens warning without enabling voice");
            settings.CancelVoiceMusic();Check(!settings.VoiceWarningVisible && !settings.VoiceToggle.isOn,"voice refusal clears warning and checkbox");
            settings.RequestVoiceMusic(true);settings.ConfirmVoiceMusic();Check(DagChanAudio.Instance.VoiceEnabled && settings.VoiceToggle.isOn,"voice confirmation enables audio and checkbox");
            settings.RequestVoiceMusic(false);control.SetSensitivity(sensitivity,false);control.SetRtxMode(rtx,false);settings.SetMusicVolume(music);settings.SetEffectsVolume(effects);
            var go=new GameObject("MVC test health");var victim=go.AddComponent<HealthController>();string order="";
            victim.Changed+=(a,b)=>order+="C";victim.Damaged+=(a,b)=>order+="H";victim.Died+=()=>order+="D";victim.TakeDamage(victim.MaxHealth);
            Check(order=="CHD","hit effects are notified before death");UnityEngine.Object.Destroy(go);
            Check(typeof(ChainsawHealthBar).GetMethod("TakeDamage")==null && typeof(ChainsawHealthBar).GetMethod("Heal")==null,"health view cannot mutate gameplay health");
            File.AppendAllText(Report,"SUCCESS\n");EditorApplication.Exit(0);
        }
        catch(Exception ex) { File.AppendAllText(Report,ex+"\n");EditorApplication.Exit(1); }
    }
}
