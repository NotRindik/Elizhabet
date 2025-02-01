public unsafe struct Standard
{
    public static unsafe void Swap<T>(T* a, T* b) where T : unmanaged
    {
        T temp = *b; 
        *b = *a;
        *a = temp;
    }
}