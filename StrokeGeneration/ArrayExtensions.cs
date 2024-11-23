using PaintDotNet;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BrushStrokes;

/// <summary>
/// Helpers for working with the <see cref="Array"/> type.
/// </summary>
internal static class ArrayExtensions
{
    /// <summary>
    /// Returns a reference to an element at a specified index within a given <typeparamref name="T"/> array.
    /// </summary>
    /// <remarks>This method doesn't have bounds checks. The caller must ensure the <paramref name="index"/> parameter is valid.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T DangerousGetManagedReferenceAt<T>(this T[] array, int index)
    {
#if DEBUG
        if (index < 0 || index >= array.Length) throw new IndexOutOfRangeException();
#endif
        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T DangerousGetReferenceAt<T>(this T[] array, int index) where T : unmanaged
    {
#if DEBUG
        if (index < 0 || index >= array.Length) throw new IndexOutOfRangeException();
#endif
        fixed (T* ptr = array)
        {
            return ptr[index];
        }
    }
}