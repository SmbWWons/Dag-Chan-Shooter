using UnityEngine;

// Shared by both revolvers, including simultaneous ultimate shots.
[DisallowMultipleComponent]
public sealed class RevolverShotPlaylist : MonoBehaviour
{
    [SerializeField] private AudioClip[] clips;
    private AudioClip[] bag;
    private int cursor;
    private AudioClip previous;
    /// <summary>
    /// Возвращает очередной клип перемешанного набора и запоминает его как предыдущий.
    /// Если набор закончился, перемешивает заново; при отсутствии звуков возвращает null.
    /// </summary>
    public AudioClip NextClip()
    {
        if(bag==null || cursor>=bag.Length)Shuffle();
        if(bag==null || bag.Length==0)return null;
        previous=bag[cursor++];return previous;
    }
    /// <summary>
    /// Собирает непустые уникальные клипы и перемешивает случайными перестановками.
    /// Если есть выбор, первый звук нового набора не совпадает с последним звуком старого.
    /// </summary>
    private void Shuffle()
    {
        var unique=new System.Collections.Generic.List<AudioClip>();
        if(clips!=null)foreach(var clip in clips)if(clip && !unique.Contains(clip))unique.Add(clip);
        bag=unique.ToArray();cursor=0;
        for(int i=bag.Length-1;i>0;i--){int j=Random.Range(0,i+1);var temp=bag[i];bag[i]=bag[j];bag[j]=temp;}
        // Also prevent a repeat where one shuffled round ends and the next starts.
        if(bag.Length>1 && bag[0]==previous){int j=Random.Range(1,bag.Length);var temp=bag[0];bag[0]=bag[j];bag[j]=temp;}
    }
}
