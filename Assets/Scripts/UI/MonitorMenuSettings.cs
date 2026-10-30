using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MonitorMenuSettings : MonoBehaviour
{
    private const string SensitivityKey="DagChan.MouseSensitivity";
    private const string RtxKey="DagChan.Rtx10Fps";
    private PlayerMovement player;
    private MonitorPauseController pause;
    private MonitorGraphicsMode graphics;
    private int previousTarget,previousVsync;
    private RectTransform leftPanel,rightPanel;
    private CanvasGroup group;
    private Text sensitivityLabel;
    private Slider slider;
    private Toggle rtxToggle;
    private Font font;
    private DagChanAudio audioSettings;
    private Slider musicSlider,effectsSlider;
    private Toggle voiceToggle;
    private Text musicLabel,effectsLabel;
    private GameObject voiceWarning;
    private RectTransform warningPanel;
    public Slider MusicSlider=>musicSlider;
    public Slider EffectsSlider=>effectsSlider;
    public Toggle VoiceToggle=>voiceToggle;
    public bool VoiceWarningVisible=>voiceWarning && voiceWarning.activeSelf;
    public float Sensitivity {get;private set;}
    public bool RtxMode {get;private set;}
    public Slider SensitivitySlider=>slider;
    public Toggle RtxToggle=>rtxToggle;
    /// <summary>
    /// Находит игрока, аудиосистему, контроллер паузы и графический режим.
    /// Сохраняет исходные ограничения кадров и VSync, затем загружает чувствительность и выбранный режим из PlayerPrefs.
    /// </summary>
    private void Awake()
    {
        audioSettings=DagChanAudio.Instance;
        pause=GetComponent<MonitorPauseController>();
        foreach(var candidate in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
            if(!candidate.transform.parent || !candidate.transform.parent.GetComponentInParent<PlayerMovement>(true)){player=candidate;break;}
        graphics=GetComponent<MonitorGraphicsMode>();if(!graphics)graphics=gameObject.AddComponent<MonitorGraphicsMode>();
        graphics.Configure(player?player.GetComponent<Camera>():Camera.main);
        previousTarget=Application.targetFrameRate==10?-1:Application.targetFrameRate;previousVsync=QualitySettings.vSyncCount;
        SetSensitivity(PlayerPrefs.GetFloat(SensitivityKey,player?player.mouseSensitivity:.3f),false);
        SetRtxMode(PlayerPrefs.GetInt(RtxKey,1)!=0,false);
    }
    /// <summary>
    /// Публичный обработчик изменения чувствительности.
    /// Передаёт значение полной перегрузке с разрешением сохранить настройку.
    /// </summary>
    public void SetSensitivity(float value){SetSensitivity(value,true);}
    /// <summary>
    /// Ограничивает чувствительность, применяет её к PlayerMovement и обновляет подпись и ползунок без повторного события.
    /// При save записывает значение в PlayerPrefs.
    /// </summary>
    private void SetSensitivity(float value,bool save)
    {
        Sensitivity=Mathf.Clamp(value,.05f,1.5f);
        if(player)player.mouseSensitivity=Sensitivity;
        if(slider)slider.SetValueWithoutNotify(Sensitivity);
        if(sensitivityLabel)sensitivityLabel.text="Чувствительность: "+Sensitivity.ToString("0.00",CultureInfo.InvariantCulture);
        if(save){PlayerPrefs.SetFloat(SensitivityKey,Sensitivity);PlayerPrefs.Save();}
    }
    /// <summary>
    /// Публичный обработчик галочки графического режима.
    /// Вызывает полную перегрузку с сохранением пользовательского выбора.
    /// </summary>
    public void SetRtxMode(bool value){SetRtxMode(value,true);}
    /// <summary>
    /// Передаёт режим в MonitorGraphicsMode и выключает VSync.
    /// Для включённого режима задаёт 10 FPS, для выключенного снимает лимит значением минус один; синхронизирует галочку и при необходимости сохраняет выбор.
    /// </summary>
    private void SetRtxMode(bool value,bool save)
    {
        RtxMode=value;
        if(graphics)graphics.SetMode(value);
        QualitySettings.vSyncCount=0;
        Application.targetFrameRate=value?10:-1;
        if(rtxToggle)rtxToggle.SetIsOnWithoutNotify(value);
        if(save){PlayerPrefs.SetInt(RtxKey,value?1:0);PlayerPrefs.Save();}
    }
    /// <summary>
    /// Однократно создаёт левое меню, правые настройки и окно предупреждения поверх изображения мониторов.
    /// Подключает EventSystem к Input Actions, назначает события кнопок и выставляет текущие чувствительность, громкости и галочки.
    /// </summary>
    public void Build(Canvas canvas)
    {
        if(group)return;
        if(!canvas.GetComponent<GraphicRaycaster>())canvas.gameObject.AddComponent<GraphicRaycaster>();
        if(!EventSystem.current)
        {
            var es=new GameObject("Menu Event System",typeof(EventSystem),typeof(InputSystemUIInputModule));
            es.transform.SetParent(transform,false);
        }
        var inputModule=EventSystem.current.GetComponent<InputSystemUIInputModule>();
        if(!inputModule)inputModule=EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
        DagChanInput.ConfigureUI(inputModule);
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root=new GameObject("Monitor Controls",typeof(RectTransform),typeof(CanvasGroup));root.transform.SetParent(canvas.transform,false);
        var full=root.GetComponent<RectTransform>();full.anchorMin=Vector2.zero;full.anchorMax=Vector2.one;full.offsetMin=full.offsetMax=Vector2.zero;
        group=root.GetComponent<CanvasGroup>();
        leftPanel=Rect("Left Monitor",full,new Vector2(440,270));
        rightPanel=Rect("Right Monitor",full,new Vector2(440,270));
        leftPanel.gameObject.AddComponent<CanvasGroup>();rightPanel.gameObject.AddComponent<CanvasGroup>();
        Label("МЕНЮ",leftPanel,new Vector2(0,105),new Vector2(400,45),27,TextAnchor.MiddleCenter);
        Button("Продолжить",leftPanel,new Vector2(0,27),()=>pause.Resume());
        Button("Выход",leftPanel,new Vector2(0,-57),ExitGame);
        Label("НАСТРОЙКИ",rightPanel,new Vector2(0,127),new Vector2(420,30),23,TextAnchor.MiddleLeft);
        sensitivityLabel=Label("",rightPanel,new Vector2(0,94),new Vector2(420,26),19,TextAnchor.MiddleLeft);
        slider=MakeSlider("Mouse Sensitivity",rightPanel,68,.05f,1.5f,SetSensitivity);
        musicLabel=Label("",rightPanel,new Vector2(0,37),new Vector2(420,26),19,TextAnchor.MiddleLeft);
        musicSlider=MakeSlider("Music Volume",rightPanel,12,0,1,SetMusicVolume);
        effectsLabel=Label("",rightPanel,new Vector2(0,-19),new Vector2(420,26),19,TextAnchor.MiddleLeft);
        effectsSlider=MakeSlider("Effects Volume",rightPanel,-44,0,1,SetEffectsVolume);
        voiceToggle=MakeToggle("Музыка с голосом",rightPanel,-80,RequestVoiceMusic,19);
        rtxToggle=MakeToggle("RTX RT ТРАСИРОВКА ПУТИ\nDLSS5 DLAA MODE",rightPanel,-123,SetRtxMode,15);
        SetSensitivity(Sensitivity,false);rtxToggle.SetIsOnWithoutNotify(RtxMode);
        musicSlider.SetValueWithoutNotify(audioSettings.MusicVolume);effectsSlider.SetValueWithoutNotify(audioSettings.EffectsVolume);
        voiceToggle.SetIsOnWithoutNotify(audioSettings.VoiceEnabled);UpdateAudioLabels();
        BuildVoiceWarning(full);
        UpdateLayout();
    }
    /// <summary>
    /// Создаёт Slider с дорожкой, заливкой и ручкой в указанном контейнере.
    /// Задаёт диапазон, высоту расположения и обработчик изменения, возвращая готовый элемент.
    /// </summary>
    private Slider MakeSlider(string name,Transform parent,float y,float min,float max,UnityEngine.Events.UnityAction<float> changed)
    {
        var rect=Rect(name,parent,new Vector2(420,26));rect.anchoredPosition=new Vector2(0,y);
        var control=rect.gameObject.AddComponent<Slider>();control.minValue=min;control.maxValue=max;
        var track=Rect("Track",rect,new Vector2(420,6));track.gameObject.AddComponent<Image>().color=new Color(.25f,.32f,.36f);
        var area=Rect("Fill Area",rect,new Vector2(400,6));var fill=Rect("Fill",area,Vector2.zero);fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;
        fill.gameObject.AddComponent<Image>().color=new Color(.12f,.83f,.91f);control.fillRect=fill;
        var handleArea=Rect("Handle Area",rect,new Vector2(400,24));var handle=Rect("Handle",handleArea,new Vector2(16,0));var image=handle.gameObject.AddComponent<Image>();image.color=Color.white;
        control.handleRect=handle;control.targetGraphic=image;control.direction=Slider.Direction.LeftToRight;control.onValueChanged.AddListener(changed);return control;
    }
    /// <summary>
    /// Создаёт переключатель с прозрачной областью нажатия, квадратом, отметкой и подписью.
    /// Размещает его на заданной высоте и подписывает переданное действие.
    /// </summary>
    private Toggle MakeToggle(string title,Transform parent,float y,UnityEngine.Events.UnityAction<bool> changed,int size)
    {
        var rect=Rect(title,parent,new Vector2(420,40));rect.anchoredPosition=new Vector2(0,y);rect.gameObject.AddComponent<Image>().color=Color.clear;
        var toggle=rect.gameObject.AddComponent<Toggle>();var box=Rect("Checkbox",rect,new Vector2(26,26));box.anchoredPosition=new Vector2(-197,0);var image=box.gameObject.AddComponent<Image>();image.color=new Color(.26f,.34f,.39f);
        var check=Rect("Checkmark",box,new Vector2(17,17));var checkImage=check.gameObject.AddComponent<Image>();checkImage.color=new Color(.12f,.9f,.9f);
        toggle.targetGraphic=image;toggle.graphic=checkImage;Label(title,rect,new Vector2(21,0),new Vector2(368,40),size,TextAnchor.MiddleLeft);toggle.onValueChanged.AddListener(changed);return toggle;
    }
    /// <summary>
    /// Передаёт громкость в DagChanAudio и обновляет проценты возле ползунков.
    /// Сама аудиосистема сразу применяет и сохраняет значение.
    /// </summary>
    public void SetMusicVolume(float value){audioSettings.SetMusicVolume(value);UpdateAudioLabels();}
    /// <summary>
    /// Меняет общую громкость эффектов через DagChanAudio и обновляет подписи.
    /// Подписанные звуковые источники получают изменение сразу.
    /// </summary>
    public void SetEffectsVolume(float value){audioSettings.SetEffectsVolume(value);UpdateAudioLabels();}
    /// <summary>
    /// Показывает текущую громкость музыки и эффектов в целых процентах.
    /// Проверяет ссылки на текст, поскольку вызывается и во время построения меню.
    /// </summary>
    private void UpdateAudioLabels()
    {
        if(musicLabel)musicLabel.text="Музыка: "+Mathf.RoundToInt(audioSettings.MusicVolume*100)+"%";
        if(effectsLabel)effectsLabel.text="Звуковые эффекты: "+Mathf.RoundToInt(audioSettings.EffectsVolume*100)+"%";
    }
    /// <summary>
    /// При выключении заглушает голос и закрывает окно.
    /// При попытке включения оставляет галочку снятой и показывает предупреждение с выбранной кнопкой отказа; голос до подтверждения не становится слышимым.
    /// </summary>
    public void RequestVoiceMusic(bool enabled)
    {
        if(!enabled){audioSettings.SetVoiceEnabled(false);voiceToggle.SetIsOnWithoutNotify(false);if(voiceWarning)voiceWarning.SetActive(false);return;}
        if(audioSettings.VoiceEnabled)return;
        voiceToggle.SetIsOnWithoutNotify(false);voiceWarning.SetActive(true);
        var button=voiceWarning.GetComponentsInChildren<Button>(true)[1];UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
    /// <summary>
    /// При открытом предупреждении включает голос и галочку без повторного события переключателя.
    /// Закрывает окно и возвращает выделение на переключатель.
    /// </summary>
    public void ConfirmVoiceMusic()
    {
        if(!VoiceWarningVisible)return;
        audioSettings.SetVoiceEnabled(true);voiceToggle.SetIsOnWithoutNotify(true);voiceWarning.SetActive(false);
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(voiceToggle.gameObject);
    }
    /// <summary>
    /// Выключает голос, снимает галочку, закрывает предупреждение и возвращает выделение.
    /// Дорожки продолжают играть без перезапуска и потери синхронизации.
    /// </summary>
    public void CancelVoiceMusic()
    {
        audioSettings.SetVoiceEnabled(false);voiceToggle.SetIsOnWithoutNotify(false);if(voiceWarning)voiceWarning.SetActive(false);
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(voiceToggle.gameObject);
    }
    /// <summary>
    /// Создаёт затемнение поверх меню и панель с вопросом и предоставленным изображением.
    /// Добавляет кнопки подтверждения и отказа и первоначально скрывает окно.
    /// </summary>
    private void BuildVoiceWarning(RectTransform parent)
    {
        var shade=Rect("Voice Music Confirmation",parent,Vector2.zero);shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.offsetMin=shade.offsetMax=Vector2.zero;
        shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.87f);voiceWarning=shade.gameObject;
        warningPanel=Rect("Warning Panel",shade,new Vector2(520,490));warningPanel.gameObject.AddComponent<Image>().color=new Color(.045f,.055f,.07f,1);
        Label("Включить музыку с голосом?",warningPanel,new Vector2(0,218),new Vector2(500,40),25,TextAnchor.MiddleCenter);
        var art=Rect("Warning Artwork",warningPanel,new Vector2(360,322));art.anchoredPosition=new Vector2(0,27);var raw=art.gameObject.AddComponent<RawImage>();raw.texture=Resources.Load<Texture2D>("AudioMenu/VoiceWarning");raw.raycastTarget=false;
        WarningButton("Подтвердить",-125,ConfirmVoiceMusic);WarningButton("Отказаться",125,CancelVoiceMusic);voiceWarning.SetActive(false);
    }
    /// <summary>
    /// Создаёт кнопку в окне предупреждения с заданным горизонтальным смещением.
    /// Назначает фон, подпись и действие нажатия.
    /// </summary>
    private void WarningButton(string title,float x,UnityEngine.Events.UnityAction action)
    {
        var rect=Rect(title,warningPanel,new Vector2(225,55));rect.anchoredPosition=new Vector2(x,-197);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.1f,.23f,.28f,1);
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);Label(title,rect,Vector2.zero,new Vector2(215,50),22,TextAnchor.MiddleCenter);
    }
    /// <summary>
    /// Создаёт дочерний UI-объект с центральными якорями, pivot и заданным размером.
    /// Остальные элементы меню используют его как общий способ настройки RectTransform.
    /// </summary>
    private RectTransform Rect(string name,Transform parent,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.pivot=new Vector2(.5f,.5f);r.sizeDelta=size;return r;
    }
    /// <summary>
    /// Создаёт текст с заданными позицией, размером, шрифтом и выравниванием.
    /// Отключает перехват указателя текстом, чтобы он не мешал нажатиям родительских кнопок.
    /// </summary>
    private Text Label(string value,Transform parent,Vector2 position,Vector2 size,int fontSize,TextAnchor alignment)
    {
        var r=Rect(value==""?"Sensitivity Value":value,parent,size);r.anchoredPosition=position;
        var text=r.gameObject.AddComponent<Text>();text.font=font;text.text=value;text.fontSize=fontSize;text.alignment=alignment;text.color=new Color(.90f,.95f,.98f);text.raycastTarget=false;return text;
    }
    /// <summary>
    /// Создаёт основную кнопку меню и задаёт цвета обычного, выделенного и нажатого состояний.
    /// Подключает действие и добавляет текстовую подпись.
    /// </summary>
    private void Button(string value,Transform parent,Vector2 position,UnityEngine.Events.UnityAction action)
    {
        var r=Rect(value,parent,new Vector2(370,62));r.anchoredPosition=position;
        var image=r.gameObject.AddComponent<Image>();image.color=new Color(.10f,.17f,.21f,.96f);
        var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;colors.highlightedColor=new Color(.48f,.8f,.9f);colors.pressedColor=new Color(.22f,.6f,.7f);colors.selectedColor=Color.white;button.colors=colors;
        button.onClick.AddListener(action);Label(value,r,Vector2.zero,new Vector2(350,58),25,TextAnchor.MiddleCenter);
    }
    /// <summary>
    /// После обновления перехода мониторов вызывает UpdateLayout.
    /// Элементы следуют актуальному Progress контроллера паузы.
    /// </summary>
    private void LateUpdate(){UpdateLayout();}
    /// <summary>
    /// Закрывает неподтверждённое предупреждение при выходе из меню и масштабирует его под экран.
    /// На время предупреждения блокирует боковые панели; также меняет прозрачность, доступность и положение кнопок по фазе перехода.
    /// </summary>
    private void UpdateLayout()
    {
        if(!group || !pause)return;
        if(VoiceWarningVisible && (!pause.IsPaused || pause.Progress<.999f))CancelVoiceMusic();
        if(leftPanel){leftPanel.GetComponent<CanvasGroup>().interactable=!VoiceWarningVisible;leftPanel.GetComponent<CanvasGroup>().blocksRaycasts=!VoiceWarningVisible;}
        if(rightPanel){rightPanel.GetComponent<CanvasGroup>().interactable=!VoiceWarningVisible;rightPanel.GetComponent<CanvasGroup>().blocksRaycasts=!VoiceWarningVisible;}
        if(warningPanel)warningPanel.localScale=Vector3.one*Mathf.Max(.45f,Screen.height/900f);
        float p=pause.Progress;float eased=p*p*(3-2*p);
        group.alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.75f,1,p));
        group.interactable=pause.IsPaused && !pause.IsTransitioning && p>=.999f;
        group.blocksRaycasts=group.interactable;
        Place(leftPanel,new Vector2(375,291),eased);Place(rightPanel,new Vector2(1547,291),eased);
    }
    /// <summary>
    /// Переводит позицию из макета 1920 на 1080 в экранные координаты текущего разрешения.
    /// Учитывает соотношение сторон, приближение и масштаб, чтобы элементы следовали изображению монитора.
    /// </summary>
    private static void Place(RectTransform panel,Vector2 designCenter,float progress)
    {
        float aspect=(float)Screen.width/Screen.height;
        var fit=new Vector2(Mathf.Max(aspect/(16f/9),1),Mathf.Max((16f/9)/aspect,1));
        var center=Vector2.Lerp(new Vector2(.50234f,.73611f),new Vector2(.5f,.5f),progress);
        float zoom=Mathf.Lerp(3.55f,1,progress);
        var uv=new Vector2(designCenter.x/1920,1-designCenter.y/1080);
        var offset=Vector2.Scale((uv-center)*zoom,new Vector2(Screen.width/fit.x,Screen.height/fit.y));
        panel.anchoredPosition=offset;
        float scale=Screen.width/(1920*fit.x)*zoom;panel.localScale=Vector3.one*scale;
    }
    /// <summary>
    /// Сохраняет настройки, затем останавливает Play Mode в редакторе или вызывает Application.Quit в сборке.
    /// Способ выхода выбирается директивой UNITY_EDITOR при компиляции.
    /// </summary>
    public void ExitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
    /// <summary>
    /// Возвращает исходный лимит кадров и VSync, сохранённые при создании компонента.
    /// Настройки запуска поэтому не остаются глобально активными после его завершения.
    /// </summary>
    private void OnDestroy(){Application.targetFrameRate=previousTarget;QualitySettings.vSyncCount=previousVsync;}
}



