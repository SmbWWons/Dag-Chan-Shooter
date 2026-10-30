using System;
using UnityEngine;

// Each renderer overrides only the existing gelatin shader's color.
// The shared material, normal maps and gelatin lighting stay unchanged.
[DisallowMultipleComponent]
public sealed class GummyBearTint : MonoBehaviour
{
    private static System.Random random;
    private static int colorOffset;
    private static readonly float[] BrightHues = { 0f, .075f, .155f, .3333333f };
    private static int nextColor;
    private static readonly int GelColor = Shader.PropertyToID("_GelColor");
    private MaterialPropertyBlock properties;
    private Renderer[] renderers;
    private bool assigned;
    public Color InstanceColor { get; private set; }

    /// <summary>
    /// Создаёт генератор случайных чисел, выбирает начальное смещение палитры и обнуляет очередь.
    /// Статические данные прошлого Play Mode поэтому не переходят в новый запуск.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSequence()
    {
        random = new System.Random();
        colorOffset = random.Next(BrightHues.Length);
        nextColor = 0;
    }

    /// <summary>
    /// Выбирает следующий яркий цвет с небольшим случайным отклонением.
    /// Находит дочерние Renderer, создаёт MaterialPropertyBlock и применяет InstanceColor без копирования общего материала.
    /// </summary>
    private void Awake()
    {
        if (random == null) ResetSequence();
        float hue = Mathf.Repeat(BrightHues[(colorOffset + nextColor++) % BrightHues.Length]
            + ((float)random.NextDouble() - .5f) * .012f, 1f);
        float saturation = .9f + (float)random.NextDouble() * .1f;
        float value = 1f;
        InstanceColor = Color.HSVToRGB(hue, saturation, value);
        assigned = true;
        renderers = GetComponentsInChildren<Renderer>(true);
        properties = new MaterialPropertyBlock();
        ApplyColor();
    }

    /// <summary>
    /// Повторно применяет уже выбранный цвет после включения объекта.
    /// Новый случайный оттенок при этом не выбирается.
    /// </summary>
    private void OnEnable() { if (assigned) ApplyColor(); }

    /// <summary>
    /// Меняет цвет только у Renderer с материалами, имеющими параметр _GelColor.
    /// Записывает его через MaterialPropertyBlock, сохраняя общий материал и другие индивидуальные параметры.
    /// </summary>
    private void ApplyColor()
    {
        foreach (var renderer in renderers)
        {
            bool gelatin = false;
            foreach (var material in renderer.sharedMaterials)
                if (material && material.HasProperty(GelColor)) { gelatin = true; break; }
            if (!gelatin) continue;
            renderer.GetPropertyBlock(properties);
            properties.SetColor(GelColor, InstanceColor);
            renderer.SetPropertyBlock(properties);
        }
    }
}

