using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(BoxCollider))]
public sealed class ArenaWaveController : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private GameObject[] gates;
    [SerializeField] private int[] waveSizes={3,5,7};
    [SerializeField] private float waveDelay=2.5f;
    [SerializeField] private string arenaName="Арена";
    private readonly List<HealthController> living=new List<HealthController>();
    private Transform player;
    private float nextWaveAt;
    public bool Started {get;private set;}
    public bool Completed {get;private set;}
    public int CurrentWave {get;private set;}
    public int TotalWaves=>waveSizes.Length;
    public int LivingCount=>living.Count;
    public string ArenaName=>arenaName;
    /// <summary>
    /// Назначает префаб врага, точки появления, ворота, размеры волн и имя арены.
    /// Генератор уровня вызывает метод для настройки созданного триггера.
    /// </summary>
    public void Configure(GameObject prefab,Transform[] spawns,GameObject[] barriers,int[] waves,string label)
    {enemyPrefab=prefab;spawnPoints=spawns;gates=barriers;waveSizes=waves;arenaName=label;}
    /// <summary>
    /// Находит PlayerMovement у вошедшего коллайдера или его родителей.
    /// Если вошёл игрок, передаёт его Transform в Begin.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        var movement=other.GetComponentInParent<PlayerMovement>();if(movement)Begin(movement.transform);
    }
    /// <summary>
    /// Отказывается от повторного старта и запуска без нужных ссылок.
    /// Запоминает игрока, закрывает ворота и назначает первую волну через 0,75 секунды; возвращает результат попытки.
    /// </summary>
    public bool Begin(Transform target)
    {
        if(Started || Completed || !target || !enemyPrefab || spawnPoints==null || spawnPoints.Length==0)return false;
        player=target;Started=true;nextWaveAt=Time.time+.75f;SetGates(true);return true;
    }
    /// <summary>
    /// Удаляет умерших из списка живых и не создаёт новые волны после смерти игрока.
    /// Когда список пуст, ждёт задержку и создаёт очередную волну с проверкой точек на NavMesh, назначением цели и навигации.
    /// После последней зачищенной волны устанавливает Completed и открывает ворота.
    /// </summary>
    private void Update()
    {
        if(!Started || Completed)return;
        living.RemoveAll(h=>!h || h.IsDead || !h.gameObject.activeInHierarchy);
        var hp=player?player.GetComponentInParent<HealthController>():null;
        if(!player || (hp && hp.IsDead))return;
        if(living.Count>0){nextWaveAt=Time.time+waveDelay;return;}
        if(CurrentWave>=waveSizes.Length){Completed=true;SetGates(false);return;}
        if(Time.time<nextWaveAt)return;
        int count=waveSizes[CurrentWave++];
        for(int i=0;i<count;i++)
        {
            Vector3 wanted=spawnPoints[i%spawnPoints.Length].position;
            int offset=i/spawnPoints.Length;if(offset>0)wanted+=spawnPoints[i%spawnPoints.Length].right*(offset*.9f);
            if(Vector3.Distance(wanted,player.position)<4)wanted=spawnPoints[(i+spawnPoints.Length/2)%spawnPoints.Length].position;
            if(NavMesh.SamplePosition(wanted,out var spot,2f,NavMesh.AllAreas))wanted=spot.position;
            var go=Instantiate(enemyPrefab,wanted+Vector3.up*.06f,Quaternion.identity,transform);
            go.name=arenaName+"_Wave"+CurrentWave+"_Bear"+(i+1);
            var enemy=go.GetComponent<GummyBearEnemy>();enemy.SetTarget(player);go.AddComponent<ArenaBearNavigation>();
            living.Add(go.GetComponent<HealthController>());
        }
    }
    /// <summary>
    /// Включает объекты ворот при закрытии и выключает при открытии.
    /// Пропускает пустые ссылки, если массив настроен частично.
    /// </summary>
    private void SetGates(bool closed){if(gates!=null)foreach(var gate in gates)if(gate)gate.SetActive(closed);}
}
