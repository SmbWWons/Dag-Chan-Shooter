using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(HealthController))]
public sealed class GummyBearHitEffects : MonoBehaviour
{
    [SerializeField] private Material chunkMaterial;
    private HealthController health;
    private GummyBearTint tint;
    private SkinnedMeshRenderer skin;
    private static Mesh chunkMesh;
    private static Material fallbackMaterial;
    public int HitBursts {get;private set;}
    public int DeathBursts {get;private set;}

    /// <summary>
    /// Находит здоровье, индивидуальный цвет и SkinnedMeshRenderer медведя.
    /// Меш и границы тела нужны для распределения кусочков при смерти.
    /// </summary>
    private void Awake()
    {
        health=GetComponent<HealthController>();tint=GetComponent<GummyBearTint>();
        skin=GetComponentInChildren<SkinnedMeshRenderer>(true);
    }
    /// <summary>
    /// Подписывается на событие урона HealthController, при необходимости восстанавливая ссылку.
    /// Эффект запускается уведомлением здоровья, а не отдельной проверкой каждого выстрела.
    /// </summary>
    private void OnEnable(){if(!health)health=GetComponent<HealthController>();health.Damaged+=OnDamaged;}
    /// <summary>
    /// Отписывается от события урона.
    /// Отключённый медведь больше не получает вызовы эффекта.
    /// </summary>
    private void OnDisable(){if(health)health.Damaged-=OnDamaged;}
    /// <summary>
    /// Определяет, был ли удар смертельным, и создаёт независимую систему частиц цвета медведя.
    /// Выпускает 9 кусочков из попадания или 56 по телу при смерти, задавая разлёт, гравитацию, отскоки, вращение и желейное сжатие.
    /// Эффект живёт отдельно от удалённого врага и затем уничтожается по таймеру.
    /// </summary>
    private void OnDamaged(Vector3 point,Vector3 direction)
    {
        bool death=health.IsDead;
        if(death)DeathBursts++;else HitBursts++;
        var bounds=skin?skin.bounds:new Bounds(transform.position+Vector3.up,Vector3.one*2);
        if(direction.sqrMagnitude<.001f)point=bounds.center;
        Color color=tint?tint.InstanceColor:new Color(1,.5f,.02f);
        var root=new GameObject(death?"GummyJelly_Death":"GummyJelly_Hit");
        root.transform.position=death?bounds.center:point;
        var ps=root.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.loop=false;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.maxParticles=64;main.startLifetime=new ParticleSystem.MinMaxCurve(1.4f,2.3f);
        main.startSpeed=0;main.startColor=color;main.startSize3D=false;
        main.gravityModifier=1.35f;main.startRotation3D=true;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
        var emission=ps.emission;emission.enabled=false;
        var shape=ps.shape;shape.enabled=false;
        var collision=ps.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.World;
        collision.mode=ParticleSystemCollisionMode.Collision3D;collision.quality=ParticleSystemCollisionQuality.Medium;
        collision.collidesWith=1;collision.enableDynamicColliders=false;collision.bounce=.52f;collision.dampen=.30f;collision.radiusScale=.8f;
        var rotation=ps.rotationOverLifetime;rotation.enabled=true;rotation.separateAxes=true;
        rotation.x=new ParticleSystem.MinMaxCurve(-7,7);rotation.y=new ParticleSystem.MinMaxCurve(-9,9);rotation.z=new ParticleSystem.MinMaxCurve(-7,7);
        var size=ps.sizeOverLifetime;size.enabled=true;size.separateAxes=true;
        size.x=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,1.25f),new Keyframe(.08f,.82f),new Keyframe(.18f,1.12f),new Keyframe(.30f,.96f),new Keyframe(.6f,1),new Keyframe(.83f,.9f),new Keyframe(1,0)));
        size.z=size.x;
        size.y=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.7f),new Keyframe(.08f,1.2f),new Keyframe(.18f,.88f),new Keyframe(.30f,1.04f),new Keyframe(.6f,1),new Keyframe(.83f,.9f),new Keyframe(1,0)));
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=ChunkMesh();
        renderer.sharedMaterial=chunkMaterial?chunkMaterial:FallbackMaterial();renderer.enableGPUInstancing=true;
        renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
        Vector3[] surface=null;Mesh baked=null;Bounds surfaceBounds=default;
        if(death && skin)
        {
            baked=new Mesh();skin.BakeMesh(baked);surface=baked.vertices;
            if(surface.Length>0)
            {
                for(int i=0;i<surface.Length;i++)surface[i]=skin.transform.TransformPoint(surface[i]);
                surfaceBounds=new Bounds(surface[0],Vector3.zero);
                foreach(var vertex in surface)surfaceBounds.Encapsulate(vertex);
            }
        }
        int count=death?56:9;
        float unit=Mathf.Clamp(bounds.size.y/2,.5f,2);
        for(int i=0;i<count;i++)
        {
            Vector3 origin=point;
            if(death)
            {
                if(surface!=null && surface.Length>0)
                {
                    var local=surface[Random.Range(0,surface.Length)]-surfaceBounds.center;
                    var bakedSize=surfaceBounds.size;
                    origin=bounds.center+Vector3.Scale(local,new Vector3(bounds.size.x/Mathf.Max(bakedSize.x,.001f),bounds.size.y/Mathf.Max(bakedSize.y,.001f),bounds.size.z/Mathf.Max(bakedSize.z,.001f)));
                }
                else origin=bounds.center+Vector3.Scale(Random.insideUnitSphere,bounds.extents);
                origin=Vector3.Lerp(bounds.center,origin,Random.Range(.7f,1f));
            }
            else origin+=Random.insideUnitSphere*.035f;
            Vector3 outward=death?(origin-bounds.center).normalized:Random.onUnitSphere;
            var velocity=outward*Random.Range(death?2.5f:1.3f,death?5.5f:3.2f)+Vector3.up*Random.Range(1.6f,3.6f)+direction*(death?1.2f:1.6f);
            var particle=new ParticleSystem.EmitParams{position=origin,velocity=velocity,startColor=color,
                startSize=Random.Range(death?.16f:.075f,death?.31f:.15f)*unit,
                rotation3D=Random.insideUnitSphere*180,startLifetime=Random.Range(1.4f,2.3f)};
            ps.Emit(particle,1);
        }
        if(baked)Destroy(baked);
        ps.Play();Destroy(root,2.6f);
    }
    /// <summary>
    /// При отсутствии назначенного материала один раз создаёт общий запасной материал частиц URP.
    /// Включает гладкость и GPU instancing для повторного использования эффектами.
    /// </summary>
    private static Material FallbackMaterial()
    {
        if(!fallbackMaterial)
        {
            fallbackMaterial=new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
            fallbackMaterial.SetColor("_BaseColor",Color.white);fallbackMaterial.SetFloat("_Smoothness",.75f);
            fallbackMaterial.enableInstancing=true;
        }
        return fallbackMaterial;
    }
    /// <summary>
    /// Один раз копирует сферу и искажает вершины, получая неровный желейный кусочек.
    /// Пересчитывает нормали и границы, удаляет временный примитив и сохраняет меш для следующих частиц.
    /// </summary>
    private static Mesh ChunkMesh()
    {
        if(chunkMesh)return chunkMesh;
        var temp=GameObject.CreatePrimitive(PrimitiveType.Sphere);
        chunkMesh=Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);chunkMesh.name="Gummy jelly blob";
        var vertices=chunkMesh.vertices;
        for(int i=0;i<vertices.Length;i++)
        {
            var v=vertices[i];float ripple=1+.10f*Mathf.Sin(v.x*17+v.z*13)*Mathf.Cos(v.y*15);
            vertices[i]=Vector3.Scale(v,new Vector3(1.1f,.85f,.95f))*ripple;
        }
        chunkMesh.vertices=vertices;chunkMesh.RecalculateNormals();chunkMesh.RecalculateBounds();
        temp.SetActive(false);Destroy(temp);return chunkMesh;
    }
}


