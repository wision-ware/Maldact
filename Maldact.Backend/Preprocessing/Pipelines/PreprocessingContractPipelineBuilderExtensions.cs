using Maldact.Backend.Preprocessing.DSPs;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.Preprocessing;
using Maldact.Core.Streaming;

namespace Maldact.Backend.Preprocessing.Pipelines;

public static class PreprocessingContractPipelineBuilderExtensions
{
    /// <summary>
    /// Constructs a fully instantiated and sequential DSP pipeline from the provided contract.
    /// </summary>
    public static IDataPreprocessor BuildPipeline(this PreprocessingContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var preprocessors = new List<IDataPreprocessor>();
        var currentSampleRate = contract.InputSampleRate ?? throw new ArgumentException("InputSampleRate missing.");
        var currentDimension = contract.InputDimension ?? throw new ArgumentException("InputDimension missing.");

        // wire up the primary pipeline steps
        foreach (var prepStep in contract.Pipeline)
        {
            (IDataPreprocessor preprocessor, double resampleRate) 
                = prepStep.CreateFilter(currentDimension, currentSampleRate);
            
            preprocessors.Add(preprocessor);

            // shift the rolling state to the output of the newly created filter
            currentDimension = preprocessor.OutputDimension;
            currentSampleRate = resampleRate;
        }
        
        // wire up the final enforcement reshaper
        var finalStep = new PreprocessingStep
        {
            Type = PreprocessingStep.PreprocessingStepType.Reshaping,
            Reshaping = contract.FinalReshaping,
        };
        
        (IDataPreprocessor finalReshaper, _) = finalStep.CreateFilter(currentDimension, currentSampleRate);
        preprocessors.Add(finalReshaper);
        
        return new LinearPipelineCompositePreprocessor(preprocessors);
    }

    /// <summary>
    /// Instantiates a specific DSP filter based on the DTO configuration.
    /// </summary>
    public static (IDataPreprocessor Dsp, double OutputSampleRate) CreateFilter(
        this PreprocessingStep step, int inputDimension, double inputSampleRate)
    {
        return step.Type switch
        {
            PreprocessingStep.PreprocessingStepType.Imputation => BuildImputation(step.Imputation, inputDimension, inputSampleRate),
            PreprocessingStep.PreprocessingStepType.Smoothing => BuildSmoothing(step.Smoothing, inputDimension, inputSampleRate),
            PreprocessingStep.PreprocessingStepType.Resampling => BuildResampling(step.Resampling, inputDimension, inputSampleRate),
            PreprocessingStep.PreprocessingStepType.Reshaping => BuildReshaping(step.Reshaping, inputDimension, inputSampleRate),
            PreprocessingStep.PreprocessingStepType.Rescaling => BuildRescaling(step.Rescaling, inputDimension, inputSampleRate),
            PreprocessingStep.PreprocessingStepType.Normalization => BuildNormalization(step.Normalization, inputDimension, inputSampleRate),
            PreprocessingStep.PreprocessingStepType.FourierTransform => BuildFft(step.Fft, inputDimension, inputSampleRate),
            
            _ => throw new InvalidOperationException($"Unknown or missing filter type: {step.Type}")
        };
    }

    private static (IDataPreprocessor, double) BuildImputation(ImputationOptions? opts, int dim, double hz)
    {
        ArgumentNullException.ThrowIfNull(opts);
        
        var method = opts.Method switch
        {
            ImputationOptions.ImputationMethod.ForwardFill => ImputationFilter.ImputationMethod.ForwardFill,
            ImputationOptions.ImputationMethod.ZeroFill => ImputationFilter.ImputationMethod.ZeroFill,
            _ => throw new ArgumentOutOfRangeException(nameof(opts.Method))
        };
        
        return (new ImputationFilter(method, dim), hz);
    }

    private static (IDataPreprocessor, double) BuildSmoothing(SmoothingOptions? opts, int dim, double hz)
    {
        ArgumentNullException.ThrowIfNull(opts);
        int window = opts.WindowSize ?? throw new ArgumentException("WindowSize missing");

        IDataPreprocessor filter = opts.Method switch
        {
            SmoothingOptions.SmoothingMethod.MovingAverage => new MovingAverageSmoother(dim, window),
            SmoothingOptions.SmoothingMethod.ExponentialSmoothing => new ExponentialSmoother(dim, window, opts.Gamma),
            _ => throw new ArgumentOutOfRangeException(nameof(opts.Method))
        };
        
        return (filter, hz);
    }

    private static (IDataPreprocessor, double) BuildResampling(ResamplingOptions? opts, int dim, double sourceHz)
    {
        ArgumentNullException.ThrowIfNull(opts);
        double targetHz = opts.TargetHz ?? throw new ArgumentException("TargetHz missing");

        var aggFunc = opts.AggregationFunctionUsed switch
        {
            ResamplingOptions.AggregationFunction.First => Resampler.AggregationFunction.First,
            ResamplingOptions.AggregationFunction.Last => Resampler.AggregationFunction.Last,
            ResamplingOptions.AggregationFunction.Max => Resampler.AggregationFunction.Max,
            ResamplingOptions.AggregationFunction.Min => Resampler.AggregationFunction.Min,
            ResamplingOptions.AggregationFunction.Mean => Resampler.AggregationFunction.Mean,
            _ => throw new ArgumentOutOfRangeException(nameof(opts.AggregationFunctionUsed))
        };

        return (new Resampler(dim, sourceHz, targetHz, aggFunc), targetHz);
    }

    private static (IDataPreprocessor, double) BuildReshaping(ReshapingOptions? opts, int dim, double hz)
    {
        ArgumentNullException.ThrowIfNull(opts);
        int targetDim = opts.TargetDimension ?? throw new ArgumentException("TargetDimension missing");

        var method = opts.Method switch
        {
            ReshapingOptions.ReshapeMethod.Interpolation => Reshaper.ReshapeMethod.Interpolation,
            ReshapingOptions.ReshapeMethod.Strict => Reshaper.ReshapeMethod.Strict,
            ReshapingOptions.ReshapeMethod.TruncateOrZeroFill => Reshaper.ReshapeMethod.TruncateOrZeroFill,
            _ => throw new ArgumentOutOfRangeException(nameof(opts.Method))
        };

        return (new Reshaper(dim, targetDim, method), hz);
    }

    private static (IDataPreprocessor, double) BuildRescaling(RescalingOptions? opts, int dim, double hz)
    {
        ArgumentNullException.ThrowIfNull(opts);

        IDataPreprocessor filter = opts.Method switch
        {
            RescalingOptions.RescalingMethod.Linear => new Rescaler(dim, sample =>
            {
                float m = opts.Multiplier;
                float o = opts.Offset;
                for (int i = 0; i < sample.Length; i++) sample[i] = (sample[i] * m) + o;
            }),
            RescalingOptions.RescalingMethod.Exponential => new Rescaler(dim, sample =>
            {
                for (int i = 0; i < sample.Length; i++) sample[i] = MathF.Exp(sample[i]);
            }),
            RescalingOptions.RescalingMethod.Logarithmic => new Rescaler(dim, sample =>
            {
                for (int i = 0; i < sample.Length; i++)
                    sample[i] = MathF.Sign(sample[i]) * MathF.Log(1f + MathF.Abs(sample[i]));
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(opts.Method))
        };

        return (filter, hz);
    }

    private static (IDataPreprocessor, double) BuildNormalization(NormalizationOptions? opts, int dim, double hz)
    {
        ArgumentNullException.ThrowIfNull(opts);

        IDataPreprocessor filter = opts.Method switch
        {
            NormalizationOptions.NormalizationMethod.MinMax => new MinMaxNormalizer(
                dim,
                globalMins: CreateUniformArray(dim, opts.MinBound),
                globalMaxes: CreateUniformArray(dim, opts.MaxBound)
            ),
            NormalizationOptions.NormalizationMethod.ZScore => new ZScoreNormalizer(
                dim,
                globalMeans: CreateUniformArray(dim, opts.Mean),
                globalStdDevs: CreateUniformArray(dim, opts.StdDev)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(opts.Method))
        };

        return (filter, hz);
    }

    private static (IDataPreprocessor, double) BuildFft(FftOptions? opts, int dim, double hz)
    {
        ArgumentNullException.ThrowIfNull(opts);
        int window = opts.WindowSize ?? throw new ArgumentException("WindowSize missing");
        int freqs = opts.NumberOfFrequenciesToKeep ?? throw new ArgumentException("NumberOfFrequenciesToKeep missing");

        return (new FftTransformer(dim, window, freqs, opts.IncludePhase), hz);
    }

    /// <summary>
    /// Helper to cleanly generate pre-filled global bounds arrays for normalizers.
    /// </summary>
    private static float[]? CreateUniformArray(int length, float? value)
    {
        if (value is null) return null;
        
        float[] arr = new float[length];
        Array.Fill(arr, value.Value);
        return arr;
    }
}