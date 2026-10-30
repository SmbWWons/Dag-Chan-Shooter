using UnityEngine;

// A small procedural flash and warm light, reused between shots.
public class RevolverMuzzleFlash : MonoBehaviour
{
    private Transform anchor;
    private Camera facingCamera;
    private float roll;
    public Transform Anchor => anchor;
    private MeshRenderer glow;
    private Light flashLight;
    private MaterialPropertyBlock properties;
    private float endTime;
    private float duration;
    private float sizeMultiplier = 2f;
    private static Mesh flashMesh;
    private static readonly int Intensity = Shader.PropertyToID("_Intensity");
    /// <summary>
    /// Создаёт или повторно использует меш звезды, назначает материал и добавляет тёплый свет.
    /// Хранит дуло как привязку, оставляя объект вне иерархии оружия, чтобы масштаб модели не искажал вспышку.
    /// </summary>
    public void Initialize(Material material, int layer, Transform muzzleAnchor, Camera camera)
    {
        anchor=muzzleAnchor;facingCamera=camera;
        transform.SetParent(null,true); // A scaled/rotated gun hierarchy must not shear the billboard.
        gameObject.layer=layer;
        var filter=gameObject.AddComponent<MeshFilter>();
        if(!flashMesh)
        {
            flashMesh=new Mesh { name="Revolver Flash Star" };
            var vertices=new Vector3[13]; var colors=new Color[13]; var triangles=new int[36];
            vertices[0]=Vector3.zero;colors[0]=new Color(8,1.6f,.08f,1);
            for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI/6;float radius=i%3==0 ? .24f : .075f;
                vertices[i+1]=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,.005f);
                colors[i+1]=new Color(4,.55f,.025f,0);
                triangles[i*3]=0; triangles[i*3+1]=i+1;triangles[i*3+2]=(i+1)%12+1;
            }
            flashMesh.vertices=vertices;flashMesh.colors=colors;flashMesh.triangles=triangles;flashMesh.RecalculateBounds();
        }
        filter.sharedMesh=flashMesh;
        glow=gameObject.AddComponent<MeshRenderer>();glow.sharedMaterial=material;
        glow.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;glow.receiveShadows=false;glow.enabled=false;
        properties=new MaterialPropertyBlock();
        flashLight=gameObject.AddComponent<Light>();flashLight.type=LightType.Point;flashLight.color=new Color(1,.45f,.09f);flashLight.range=1.8f;flashLight.shadows=LightShadows.None;flashLight.enabled=false;
    }
    /// <summary>
    /// Задаёт размер и длительность вспышки, выбирает случайный поворот и включает изображение со светом.
    /// Сразу обновляет положение и яркость для текущего выстрела.
    /// </summary>
    public void Show(float lifetime, float size = 2f)
    {
        sizeMultiplier=Mathf.Max(.1f,size);
        duration=Mathf.Max(.01f,lifetime);endTime=Time.time+duration;
        roll=Random.Range(0,360);
        glow.enabled=true;flashLight.enabled=true;UpdateFlash();
    }
    /// <summary>
    /// Выключает изображение и свет, сохраняя объект для следующих выстрелов.
    /// Проверяет ссылки, поэтому безопасен при неполной инициализации.
    /// </summary>
    public void Hide() { if(glow)glow.enabled=false; if(flashLight)flashLight.enabled=false; }
    /// <summary>
    /// При потере дула удаляет эффект, при выключенной привязке скрывает его.
    /// Активную вспышку перемещает и постепенно гасит через UpdateFlash.
    /// </summary>
    private void LateUpdate() { if(!anchor) { Destroy(gameObject);return; } if(!anchor.gameObject.activeInHierarchy) {Hide();return;} if(glow && glow.enabled) UpdateFlash(); }
    /// <summary>
    /// По оставшемуся времени уменьшает яркость материала и света и меняет размер.
    /// Размещает эффект перед дулом, разворачивает к камере и выключает после окончания длительности.
    /// </summary>
    private void UpdateFlash()
    {
        float remaining=Mathf.Clamp01((endTime-Time.time)/duration);
        properties.SetFloat(Intensity,remaining);glow.SetPropertyBlock(properties);

        float size=Mathf.Lerp(.55f,1.15f,remaining)*sizeMultiplier;
        if(anchor) transform.position=anchor.position+anchor.forward*.3f;
        transform.rotation=(facingCamera ? facingCamera.transform.rotation : anchor.rotation)*Quaternion.Euler(0,0,roll);
        transform.localScale=Vector3.one*size;
        flashLight.intensity=4f*remaining;
        if(remaining<=0) { glow.enabled=false;flashLight.enabled=false; }
    }
}





