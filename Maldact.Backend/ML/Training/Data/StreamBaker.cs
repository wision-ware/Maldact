using System.Buffers;
using System.Runtime.InteropServices;
using Maldact.Core.Data;
using Maldact.Core.Preprocessing;

namespace Maldact.Backend.ML.Training.Data;

/// <summary>
/// Handles the offline batch processing of raw dataset streams into model-ready tensor files.
/// Optimized for zero-allocation memory pooling and direct byte-to-tensor marshalling.
/// </summary>
public class StreamBaker
{
    private readonly PipelineFactory _buildPipeline;
    private const int BufferSize = 4096;

    /// <summary>
    /// Factory delegate for instantiating signal processing pipelines per file.
    /// </summary>
    /// <returns>A tuple containing the initialized preprocessor and its base sample rate.</returns>
    public delegate (IDataPreprocessor pipeline, float sampleRate) PipelineFactory();

    /// <summary>
    /// Initializes a new instance of the StreamBaker.
    /// </summary>
    /// <param name="pipelineFactory">The factory used to construct the preprocessing pipeline.</param>
    public StreamBaker(PipelineFactory pipelineFactory)
    {
        ArgumentNullException.ThrowIfNull(pipelineFactory);
        _buildPipeline = pipelineFactory;
    }

    /// <summary>
    /// Reads a raw binary file, applies the zero-allocation preprocessing pipeline in chunks, and writes the output to disk.
    /// Guarantees structural frame alignment across chunk boundaries.
    /// </summary>
    /// <param name="rawFilePath">The absolute path to the raw input stream.</param>
    /// <param name="bakedFilePath">The absolute path where the processed stream will be saved.</param>
    /// <param name="inputFeatureDim">The number of features per temporal frame in the raw data.</param>
    public async Task BakeStreamAsync(string rawFilePath, string bakedFilePath, int inputFeatureDim)
    {
        if (inputFeatureDim <= 0) throw new ArgumentOutOfRangeException(nameof(inputFeatureDim));

        await using var reader = new FileStream(rawFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        await using var writer = new FileStream(bakedFilePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        
        var (pipeline, _) = _buildPipeline();

        int bytesPerFrame = inputFeatureDim * sizeof(float);
        
        // rent a buffer large enough to hold the requested frame count
        byte[] readBuffer = ArrayPool<byte>.Shared.Rent(BufferSize * bytesPerFrame);
        
        try
        {
            int remnantBytes = 0;
            int bytesRead;

            // read into buffer span immediately following any leftover unaligned bytes
            while ((bytesRead = await reader.ReadAsync(readBuffer.AsMemory(remnantBytes))) > 0)
            {
                int totalBytes = remnantBytes + bytesRead;
                int framesInChunk = totalBytes / bytesPerFrame;
                int validBytesToProcess = framesInChunk * bytesPerFrame;
                
                remnantBytes = totalBytes - validBytesToProcess;

                if (framesInChunk == 0) continue;

                // initialize empty chunk
                using (var chunk = PipelineChunk.Rent(framesInChunk, inputFeatureDim))
                {
                    chunk.DecodeRawNetworkBytes(readBuffer, 0, validBytesToProcess);
                    
                    pipeline.Process(chunk);
                    
                    ReadOnlySpan<float> processedFloats = chunk.CurrentData;
                    if (processedFloats.Length > 0)
                    {
                        int bytesToWrite = processedFloats.Length * sizeof(float);
                        byte[] writeBuffer = ArrayPool<byte>.Shared.Rent(bytesToWrite);
                        
                        try
                        {
                            MemoryMarshal.Cast<float, byte>(processedFloats).CopyTo(writeBuffer);
                            await writer.WriteAsync(writeBuffer.AsMemory(0, bytesToWrite));
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(writeBuffer);
                        }
                    }
                }

                if (remnantBytes > 0)
                {
                    readBuffer.AsSpan(validBytesToProcess, remnantBytes).CopyTo(readBuffer.AsSpan(0, remnantBytes));
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
        }
    }
}