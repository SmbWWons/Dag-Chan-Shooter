using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// A stylized reflection/post-processing preset, deliberately independent of hardware ray tracing.
[DisallowMultipleComponent]
public sealed class MonitorGraphicsMode : MonoBehaviour
{
    private struct Swap {public Renderer renderer;public Material[] originals;}
    private readonly List<Swap> swaps=new List<Swap>();
    private readonly Dictionary<Material,Material> clones=new Dictionary<Material,Material>();
    private Camera world;
    private bool previousRealtime,qualityCaptured;
    private Volume volume;
    private VolumeProfile profile;
    private ReflectionProbe probe;
    private float nextProbe;
    private int probeJob=-1;
    private Vector3 probePosition;
    public bool IsEnabled {get;private set;}
    public ReflectionProbe Reflection=>probe;
    public Volume EffectsVolume=>volume;
    public int EnhancedRendererCount=>swaps.Count;
    private static readonly int Mode=Shader.PropertyToID("_DagChanRtxMode");
    /// <summary>
    /// Перед новым запуском сбрасывает глобальный параметр шейдеров _DagChanRtxMode.
    /// Режим прошлого Play Mode не остаётся включённым в новом сеансе.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMode(){Shader.SetGlobalFloat(Mode,0);}
    /// <summary>
    /// Запоминает основную камеру и один раз сохраняет исходную настройку живых Reflection Probes.
    /// Она восстанавливается при выключении или удалении режима.
    /// </summary>
    public void Configure(Camera camera){world=camera;if(!qualityCaptured){previousRealtime=QualitySettings.realtimeReflectionProbes;qualityCaptured=true;}}
    /// <summary>
    /// Переключает параметр шейдеров и доступность живых отражений.
    /// При включении создаёт эффекты, усиливает материалы и запускает ReflectionProbe; при выключении возвращает материалы и убирает влияние Volume.
    /// Аппаратная трассировка лучей этим методом не включается.
    /// </summary>
    public void SetMode(bool enabled)
    {
        if(IsEnabled==enabled && volume)return;
        if(!qualityCaptured){previousRealtime=QualitySettings.realtimeReflectionProbes;qualityCaptured=true;}
        QualitySettings.realtimeReflectionProbes=enabled?true:previousRealtime;
        IsEnabled=enabled;Shader.SetGlobalFloat(Mode,enabled?1:0);
        if(enabled)
        {
            if(!world)world=Camera.main;
            CreateEffects();volume.weight=1;probe.enabled=true;EnhanceMaterials();RefreshProbe();
        }
        else
        {
            if(volume)volume.weight=0;
            if(probe)probe.enabled=false;
            RestoreMaterials();
        }
    }
    /// <summary>
    /// Один раз создаёт Volume с Bloom и коррекцией цвета и ReflectionProbe возле игрока.
    /// Настраивает область, разрешение и маску отражений, исключая интерфейс и слой модели оружия.
    /// </summary>
    private void CreateEffects()
    {
        if(volume)return;
        var go=new GameObject("RTX Joke - Cinematic Lighting");go.transform.SetParent(transform,false);
        volume=go.AddComponent<Volume>();volume.isGlobal=true;volume.priority=100;volume.weight=0;
        profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Runtime RTX joke effects";volume.sharedProfile=profile;
        var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.38f);bloom.threshold.Override(.85f);bloom.scatter.Override(.62f);
        var grade=profile.Add<ColorAdjustments>(true);grade.postExposure.Override(.12f);grade.contrast.Override(8);grade.saturation.Override(4);
        var probeObject=new GameObject("RTX Joke - Local Reflections");probeObject.transform.SetParent(transform,false);
        probe=probeObject.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;probe.resolution=128;probe.hdr=true;
        probe.boxProjection=true;probe.size=new Vector3(120,28,100);probe.intensity=1.15f;probe.importance=100;
        probe.nearClipPlane=.1f;probe.farClipPlane=65;probe.clearFlags=ReflectionProbeClearFlags.Skybox;
        probe.cullingMask=(world?world.cullingMask:~0)&~(1<<7)&~(1<<5);
    }
    /// <summary>
    /// Выбирает подходящие Renderer пола, укрытий, маршрута и оружия с URP Lit.
    /// Подменяет материалы временными копиями с усиленной гладкостью, отражениями или свечением и запоминает оригиналы для возврата.
    /// </summary>
    private void EnhanceMaterials()
    {
        if(swaps.Count>0)return;
        foreach(var renderer in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
        {
            bool weapon=renderer.name=="Cube.005" || renderer.name=="РЕвольвер 2";
            bool floor=renderer.name=="Floor" || renderer.name=="Exit platform";
            bool cover=renderer.name.StartsWith("Cover");
            bool route=renderer.name=="Route dash" || renderer.name=="Route arrow";
            if(!weapon && !floor && !cover && !route)continue;
            var originals=renderer.sharedMaterials;var changed=(Material[])originals.Clone();bool applied=false;
            for(int i=0;i<originals.Length;i++)
            {
                var source=originals[i];if(!source || source.shader.name!="Universal Render Pipeline/Lit")continue;
                if(!clones.TryGetValue(source,out var material))
                {
                    material=new Material(source){name=source.name+" [RTX joke]"};clones.Add(source,material);
                    if(weapon)material.SetFloat("_Smoothness",1.15f);
                    else if(route){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",source.GetColor("_BaseColor")*1.8f);}
                    else {material.SetFloat("_Smoothness",floor?.74f:.58f);material.SetFloat("_Metallic",floor?.23f:.18f);}
                }
                changed[i]=material;applied=true;
            }
            if(applied){swaps.Add(new Swap{renderer=renderer,originals=originals});renderer.sharedMaterials=changed;}
        }
    }
    /// <summary>
    /// Возвращает сохранённые материалы существующим Renderer.
    /// Очищает список замен и удаляет временные копии, освобождая ресурсы.
    /// </summary>
    private void RestoreMaterials()
    {
        foreach(var swap in swaps)if(swap.renderer)swap.renderer.sharedMaterials=swap.originals;
        swaps.Clear();foreach(var material in clones.Values)if(material)Destroy(material);clones.Clear();
    }
    /// <summary>
    /// Перемещает ReflectionProbe к камере и запускает захват отражений.
    /// Запоминает номер задания, текущую позицию и ближайшее время следующего обновления.
    /// </summary>
    private void RefreshProbe()
    {
        if(!probe || !world)return;
        probe.transform.position=world.transform.position;probePosition=world.transform.position;
        probeJob=probe.RenderProbe();nextProbe=Time.unscaledTime+2.5f;
    }
    /// <summary>
    /// Ждёт нужного интервала и окончания прошлого захвата.
    /// При заметном перемещении камеры или по периодическому таймеру обновляет отражения, используя время, которое продолжает идти на паузе.
    /// </summary>
    private void LateUpdate()
    {
        if(!IsEnabled || !probe || !world || Time.unscaledTime<nextProbe)return;
        if(probeJob>=0 && !probe.IsFinishedRendering(probeJob))return;
        // Updating periodically also picks up moving enemies, while avoiding a six-camera capture every frame.
        if((world.transform.position-probePosition).sqrMagnitude>4 || Time.unscaledTime>nextProbe+2.5f)RefreshProbe();
    }
    /// <summary>
    /// Сбрасывает параметр шейдеров, восстанавливает настройку отражений и исходные материалы.
    /// Удаляет созданные VolumeProfile и его компоненты, не затрагивая ресурсы проекта.
    /// </summary>
    private void OnDestroy()
    {
        Shader.SetGlobalFloat(Mode,0);if(qualityCaptured)QualitySettings.realtimeReflectionProbes=previousRealtime;RestoreMaterials();
        if(profile){foreach(var component in profile.components)if(component)Destroy(component);Destroy(profile);}
    }
}
