// Можно запустить через Job System:

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
struct ColorSearchJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Color32> pixels;
    [ReadOnly] public NativeArray<Color32> targetColors;
    [ReadOnly] public NativeArray<int> indices;       // явные глобальные индексы
    [NativeDisableParallelForRestriction] public NativeArray<int2> results;
    public int width;
    public int rectX, rectY, rectW, rectH;

    public void Execute(int i)
    {
        int globalIndex = indices[i];  // берём реальный индекс
        Color32 target = targetColors[globalIndex];

        for (int y = rectY; y < rectY + rectH; y++)
        {
            for (int x = rectX; x < rectX + rectW; x++)
            {
                var p = pixels[y * width + x];
                if (p.a == 0) continue;
                if (p.r == target.r && p.g == target.g && p.b == target.b)
                {
                    results[globalIndex] = new int2(x, y);
                    return;
                }
            }
        }
        results[globalIndex] = new int2(-1, -1);
    }
}