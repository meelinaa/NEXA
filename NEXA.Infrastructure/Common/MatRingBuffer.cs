using System;
using System.Collections.Concurrent;
using System.Threading;
using OpenCvSharp;

namespace NEXA.Common;

/// <summary>
/// High-performance, zero-allocation ring buffer and object pool for native OpenCV <see cref="Mat"/> instances.
/// <para>
/// <b>What it is:</b> A lock-free/thread-safe pool of reusable native image matrices preventing continuous Gen-0 GC allocations 
/// and unmanaged memory churn during 30/60 FPS camera ingestion and asynchronous multi-task vision processing.
/// </para>
/// </summary>
public sealed class MatRingBuffer : IDisposable
{
    private readonly Mat[] _buffer;
    private readonly ConcurrentQueue<Mat> _availableQueue;
    private int _isDisposed;

    /// <summary>
    /// Gets the total capacity of pre-allocated matrices in the ring buffer.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MatRingBuffer"/> class with pre-allocated native matrices.
    /// </summary>
    /// <param name="capacity">Number of native <see cref="Mat"/> instances to pre-allocate in the pool (default: 4).</param>
    public MatRingBuffer(int capacity = 4)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        Capacity = capacity;
        _buffer = new Mat[capacity];
        _availableQueue = new ConcurrentQueue<Mat>();

        for (int i = 0; i < capacity; i++)
        {
            Mat mat = new();
            _buffer[i] = mat;
            _availableQueue.Enqueue(mat);
        }
    }

    /// <summary>
    /// Rents a reusable native <see cref="Mat"/> instance from the pool.
    /// If all pre-allocated instances are currently rented out, a transient instance is created.
    /// </summary>
    /// <returns>A native <see cref="Mat"/> ready for writing or image ingestion.</returns>
    public Mat Rent()
    {
        ObjectDisposedException.ThrowIf(_isDisposed != 0, this);

        if (_availableQueue.TryDequeue(out Mat? mat))
        {
            return mat;
        }

        // Fallback in case of temporary buffer saturation
        return new Mat();
    }

    /// <summary>
    /// Returns a rented <see cref="Mat"/> back into the pool for subsequent frame recycling.
    /// If the returned matrix was a temporary overflow instance, it is disposed.
    /// </summary>
    /// <param name="mat">The native matrix to recycle.</param>
    public void Return(Mat? mat)
    {
        if (mat == null)
        {
            return;
        }

        if (_isDisposed != 0)
        {
            mat.Dispose();
            return;
        }

        // Check if the returned matrix belongs to our pre-allocated ring buffer
        bool isPooled = false;
        for (int i = 0; i < _buffer.Length; i++)
        {
            if (ReferenceEquals(_buffer[i], mat))
            {
                isPooled = true;
                break;
            }
        }

        if (isPooled)
        {
            _availableQueue.Enqueue(mat);
        }
        else
        {
            // Overflow matrix: dispose immediately
            mat.Dispose();
        }
    }

    /// <summary>
    /// Rents a <see cref="Mat"/> wrapped in an auto-recycling <see cref="MatLease"/> disposable struct.
    /// </summary>
    /// <returns>A disposable lease that returns the matrix to this pool upon disposal.</returns>
    public MatLease RentLease()
    {
        return new MatLease(this, Rent());
    }

    /// <summary>
    /// Disposes all pre-allocated native OpenCV matrices and releases unmanaged memory.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        while (_availableQueue.TryDequeue(out _))
        {
            // Clear queue references
        }

        for (int i = 0; i < _buffer.Length; i++)
        {
            _buffer[i]?.Dispose();
        }
    }
}

/// <summary>
/// Lightweight disposable lease wrapping a rented native <see cref="Mat"/> to ensure automatic return to the <see cref="MatRingBuffer"/>.
/// </summary>
public readonly struct MatLease : IDisposable
{
    private readonly MatRingBuffer _pool;

    /// <summary>
    /// Gets the rented native <see cref="Mat"/> instance.
    /// </summary>
    public Mat Mat { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MatLease"/> struct.
    /// </summary>
    public MatLease(MatRingBuffer pool, Mat mat)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        Mat = mat ?? throw new ArgumentNullException(nameof(mat));
    }

    /// <summary>
    /// Returns the rented matrix back to its originating pool.
    /// </summary>
    public void Dispose()
    {
        _pool.Return(Mat);
    }
}
