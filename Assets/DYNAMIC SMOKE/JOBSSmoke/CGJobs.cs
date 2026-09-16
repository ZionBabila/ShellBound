using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public static class CGJobs
{
    [BurstCompile]
    public struct SaxpyJob : IJobParallelFor
    {
        [ReadOnly] public float a;
        [ReadOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> x;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> y;
        public int offset;

        public void Execute(int index)
        {
            y[offset + index] += a * x[offset + index];
        }
    }
}