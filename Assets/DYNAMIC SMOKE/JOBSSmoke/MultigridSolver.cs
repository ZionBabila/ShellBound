using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

public static class MultigridSolver
{
    public static JobHandle VCycle_Jobs(JobHandle dependency, ref FlatMultigridData data, int preSmooth, int postSmooth, int bottomSolve, JosStamSmokeEmitterJobs.SmootherType smootherType)
    {
        JobHandle handle = dependency;

        // --- GO DOWN THE V ---
        for (int level = 0; level < data.numLevels - 1; level++)
        {
            handle = ScheduleSmoother(handle, level, preSmooth, ref data, smootherType);
            handle = ScheduleComputeAndRestrictResidual(handle, level, ref data);
            handle = ScheduleClear(handle, level + 1, ref data);
        }

        // --- BOTTOM SOLVE ---
        int bottomLevel = data.numLevels - 1;
        handle = ScheduleSmoother(handle, bottomLevel, bottomSolve, ref data, smootherType);

        // --- GO UP THE V ---
        for (int level = data.numLevels - 2; level >= 0; level--)
        {
            handle = ScheduleProlongateAndCorrect(handle, level, ref data);
            handle = ScheduleSmoother(handle, level, postSmooth, ref data, smootherType);
        }

        return handle;
    }

    private static JobHandle ScheduleSmoother(JobHandle dependency, int level, int iterations, ref FlatMultigridData data, JosStamSmokeEmitterJobs.SmootherType smootherType)
    {
        JobHandle handle = dependency;
        int N = data.levelSizes[level];
        int offset = data.levelOffsets[level];

        switch (smootherType)
        {
            case JosStamSmokeEmitterJobs.SmootherType.Jacobi:
                {
                    int innerCellCount = N * N;
                    if (innerCellCount <= 0) return handle;

                    NativeArray<float> readBuffer = data.solutions;
                    NativeArray<float> writeBuffer = data.temps;

                    for (int i = 0; i < iterations; i++)
                    {
                        var jacobiJob = new JacobiSmootherJob { x_read = readBuffer, x0_rhs = data.residuals, x_write = writeBuffer, offset = offset, N = N };
                        handle = jacobiJob.Schedule(innerCellCount, 64, handle);
                        handle = new BoundaryJob { field = writeBuffer, N = N, boundaryType = 0, offset = offset }.Schedule(handle);

                        var temp = readBuffer;
                        readBuffer = writeBuffer;
                        writeBuffer = temp;
                    }

                    if (iterations % 2 == 1)
                    {
                        int arraySize = (N + 2) * (N + 2);
                        var copyJob = new CopyArrayJob
                        {
                            source = data.temps.GetSubArray(offset, arraySize),
                            destination = data.solutions.GetSubArray(offset, arraySize)
                        };
                        handle = copyJob.Schedule(arraySize, 128, handle);
                    }
                    break;
                }

            case JosStamSmokeEmitterJobs.SmootherType.RedBlackGaussSeidel:
                {
                    int redBlackCount = (N * N) / 2;
                    if (redBlackCount <= 0) return handle;

                    for (int i = 0; i < iterations; i++)
                    {
                        var redJob = new ParallelRedBlackSmootherJob { x = data.solutions, x0 = data.residuals, offset = offset, N = N, isRedPass = true };
                        handle = redJob.Schedule(redBlackCount, 64, handle);
                        handle = new BoundaryJob { field = data.solutions, N = N, boundaryType = 0, offset = offset }.Schedule(handle);

                        var blackJob = new ParallelRedBlackSmootherJob { x = data.solutions, x0 = data.residuals, offset = offset, N = N, isRedPass = false };
                        handle = blackJob.Schedule(redBlackCount, 64, handle);
                        handle = new BoundaryJob { field = data.solutions, N = N, boundaryType = 0, offset = offset }.Schedule(handle);
                    }
                    break;
                }
        }

        return handle;
    }

    private static JobHandle ScheduleComputeAndRestrictResidual(JobHandle dependency, int level, ref FlatMultigridData data)
    {
        int N_f = data.levelSizes[level];
        int N_c = data.levelSizes[level + 1];
        int offset_f = data.levelOffsets[level];
        int offset_c = data.levelOffsets[level + 1];
        int arraySize_f = (N_f + 2) * (N_f + 2);

        var resJob = new ParallelResidualJob { solution = data.solutions, rhs = data.residuals, residual = data.temps, offset = offset_f, N = N_f };
        JobHandle handle = resJob.Schedule(arraySize_f, 128, dependency);

        var restrictJob = new ParallelRestrictionJob { fine = data.temps, coarse = data.residuals, fineOffset = offset_f, coarseOffset = offset_c, N_c = N_c };
        handle = restrictJob.Schedule(N_c * N_c, 64, handle);
        return handle;
    }

    private static JobHandle ScheduleProlongateAndCorrect(JobHandle dependency, int level, ref FlatMultigridData data)
    {
        int N_f = data.levelSizes[level];
        int offset_f = data.levelOffsets[level];
        int offset_c = data.levelOffsets[level + 1];

        var prolongJob = new ParallelProlongationJob { coarse = data.solutions, fine = data.temps, coarseOffset = offset_c, fineOffset = offset_f, N_f = N_f };
        JobHandle handle = prolongJob.Schedule(N_f * N_f, 64, dependency);

        int arrayLength = (N_f + 2) * (N_f + 2);
        var saxpyJob = new CGJobs.SaxpyJob { a = 1.0f, x = data.temps, y = data.solutions, offset = offset_f };
        handle = saxpyJob.Schedule(arrayLength, 128, handle);
        return handle;
    }

    private static JobHandle ScheduleClear(JobHandle dependency, int level, ref FlatMultigridData data)
    {
        int N = data.levelSizes[level];
        int offset = data.levelOffsets[level];
        int arraySize = (N + 2) * (N + 2);
        return new ClearArrayJob { array = data.solutions.GetSubArray(offset, arraySize) }.Schedule(arraySize, 128, dependency);
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct JacobiSmootherJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<float> x_read; 
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<float> x0_rhs; 
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> x_write; 

        public int offset;
        public int N;

        public void Execute(int index)
        {
            int i = (index % N) + 1;
            int j = (index / N) + 1;
            int gridIndex = offset + i + j * (N + 2);

            float neighborSum =
                x_read[gridIndex - 1] +
                x_read[gridIndex + 1] +
                x_read[gridIndex - (N + 2)] +
                x_read[gridIndex + (N + 2)];

            x_write[gridIndex] = (x0_rhs[gridIndex] + neighborSum) / 4.0f;
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct ParallelRedBlackSmootherJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction][NoAlias] public NativeArray<float> x;
        [ReadOnly, NoAlias] public NativeArray<float> x0;
        public int offset;
        public int N;
        public bool isRedPass;

        public void Execute(int index)
        {
            int x_coord = 0;
            int y_coord = 0;
            int halfN = N / 2;

            if (isRedPass)
            {
                y_coord = index / halfN;
                x_coord = (index % halfN) * 2;
                x_coord += (y_coord % 2);
            }
            else
            {
                y_coord = index / halfN;
                x_coord = (index % halfN) * 2;
                x_coord += ((y_coord + 1) % 2);
            }

            y_coord += 1;
            x_coord += 1;

            if (x_coord > N) return;

            int gridIndex = offset + x_coord + y_coord * (N + 2);
            x[gridIndex] = (x0[gridIndex] + (x[gridIndex - 1] + x[gridIndex + 1] + x[gridIndex - (N + 2)] + x[gridIndex + (N + 2)])) / 4.0f;
        }
    }
}