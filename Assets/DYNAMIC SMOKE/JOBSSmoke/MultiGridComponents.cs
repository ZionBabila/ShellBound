using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

public struct FlatMultigridData
{
    public NativeArray<float> solutions;
    public NativeArray<float> residuals;
    public NativeArray<float> temps;
    public NativeArray<int> levelOffsets;
    public NativeArray<int> levelSizes;
    public int numLevels;

    public FlatMultigridData(int gridSize, int maxLevels, Allocator allocator)
    {
        numLevels = 1;
        int currentSize = gridSize;
        int totalElements = (gridSize + 2) * (gridSize + 2);

        while (currentSize > 4 && numLevels < maxLevels)
        {
            if (currentSize % 2 != 0) break;
            currentSize /= 2;
            numLevels++;
            totalElements += (currentSize + 2) * (currentSize + 2);
        }

        solutions = new NativeArray<float>(totalElements, allocator);
        residuals = new NativeArray<float>(totalElements, allocator);
        temps = new NativeArray<float>(totalElements, allocator);
        levelOffsets = new NativeArray<int>(numLevels, allocator);
        levelSizes = new NativeArray<int>(numLevels, allocator);

        currentSize = gridSize;
        int currentOffset = 0;
        for (int i = 0; i < numLevels; i++)
        {
            levelOffsets[i] = currentOffset;
            levelSizes[i] = currentSize;
            currentOffset += (currentSize + 2) * (currentSize + 2);
            currentSize /= 2;
        }
    }

    public JobHandle Dispose(JobHandle dependency)
    {
        dependency = solutions.Dispose(dependency);
        dependency = residuals.Dispose(dependency);
        dependency = temps.Dispose(dependency);
        dependency = levelOffsets.Dispose(dependency);
        dependency = levelSizes.Dispose(dependency);
        return dependency;
    }
}


[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct ParallelResidualJob : IJobParallelFor
{
    [NativeDisableParallelForRestriction][ReadOnly, NoAlias] public NativeArray<float> solution;
    [NativeDisableParallelForRestriction][ReadOnly, NoAlias] public NativeArray<float> rhs;
    [NativeDisableParallelForRestriction][WriteOnly, NoAlias] public NativeArray<float> residual;
    public int offset;
    public int N;
    public void Execute(int index)
    {
        int i = index % (N + 2);
        int j = index / (N + 2);
        if (i == 0 || i > N || j == 0 || j > N) return;
        float ax = 4f * solution[offset + index] - (solution[offset + index - 1] + solution[offset + index + 1] + solution[offset + index - (N + 2)] + solution[offset + index + (N + 2)]);
        residual[offset + index] = rhs[offset + index] - ax;
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct ParallelRestrictionJob : IJobParallelFor
{
    [NativeDisableParallelForRestriction][ReadOnly, NoAlias] public NativeArray<float> fine;
    [NativeDisableParallelForRestriction][WriteOnly, NoAlias] public NativeArray<float> coarse;
    public int fineOffset;
    public int coarseOffset;
    public int N_c;
    public void Execute(int coarseGridIndex)
    {
        int c_x = coarseGridIndex % N_c + 1;
        int c_y = coarseGridIndex / N_c + 1;
        int N_f = N_c * 2;
        int f_x = c_x * 2;
        int f_y = c_y * 2;
        float center = fine[fineOffset + (f_x) + (f_y) * (N_f + 2)] * 4f;
        float cardinal = (fine[fineOffset + (f_x + 1) + (f_y) * (N_f + 2)] + fine[fineOffset + (f_x - 1) + (f_y) * (N_f + 2)] + fine[fineOffset + (f_x) + (f_y + 1) * (N_f + 2)] + fine[fineOffset + (f_x) + (f_y - 1) * (N_f + 2)]) * 2f;
        float diagonal = fine[fineOffset + (f_x + 1) + (f_y + 1) * (N_f + 2)] + fine[fineOffset + (f_x + 1) + (f_y - 1) * (N_f + 2)] + fine[fineOffset + (f_x - 1) + (f_y + 1) * (N_f + 2)] + fine[fineOffset + (f_x - 1) + (f_y - 1) * (N_f + 2)];
        coarse[coarseOffset + c_x + c_y * (N_c + 2)] = (center + cardinal + diagonal) / 16.0f;
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct ParallelProlongationJob : IJobParallelFor
{
    [NativeDisableParallelForRestriction][ReadOnly, NoAlias] public NativeArray<float> coarse;
    [NativeDisableParallelForRestriction][WriteOnly, NoAlias] public NativeArray<float> fine; 
    public int coarseOffset;
    public int fineOffset;
    public int N_f;
    public void Execute(int fineGridIndex)
    {
        int f_x = fineGridIndex % N_f + 1;
        int f_y = fineGridIndex / N_f + 1;
        int N_c = N_f / 2;
        float c_xf = f_x / 2.0f;
        float c_yf = f_y / 2.0f;
        int c_x0 = (int)math.floor(c_xf);
        int c_y0 = (int)math.floor(c_yf);
        int c_x1 = c_x0 + 1;
        int c_y1 = c_y0 + 1;
        float tx = c_xf - c_x0;
        float ty = c_yf - c_y0;
        float v00 = coarse[coarseOffset + c_x0 + c_y0 * (N_c + 2)];
        float v10 = coarse[coarseOffset + c_x1 + c_y0 * (N_c + 2)];
        float v01 = coarse[coarseOffset + c_x0 + c_y1 * (N_c + 2)];
        float v11 = coarse[coarseOffset + c_x1 + c_y1 * (N_c + 2)];
        fine[fineOffset + f_x + f_y * (N_f + 2)] = math.lerp(math.lerp(v00, v10, tx), math.lerp(v01, v11, tx), ty);
    }
}