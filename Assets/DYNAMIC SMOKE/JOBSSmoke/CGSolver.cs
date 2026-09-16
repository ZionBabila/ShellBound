using Unity.Collections;

public class CGSolver
{
    public NativeArray<float> r, p, z, q;
    public NativeArray<float> tempDot;

    public CGSolver(int gridSize)
    {
        int arraySize = (gridSize + 2) * (gridSize + 2);

        r = new NativeArray<float>(arraySize, Allocator.Persistent);
        p = new NativeArray<float>(arraySize, Allocator.Persistent);
        z = new NativeArray<float>(arraySize, Allocator.Persistent);
        q = new NativeArray<float>(arraySize, Allocator.Persistent);
        tempDot = new NativeArray<float>(arraySize, Allocator.Persistent);
    }

    public void Dispose()
    {
        if (r.IsCreated) r.Dispose();
        if (p.IsCreated) p.Dispose();
        if (z.IsCreated) z.Dispose();
        if (q.IsCreated) q.Dispose();
        if (tempDot.IsCreated) tempDot.Dispose();
    }
}