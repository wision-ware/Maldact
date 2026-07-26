using FluentAssertions;
using Maldact.Backend.ML.Training.Data;
using Maldact.Core.Config.ConfigDefinitions;
using Maldact.Core.ML;

namespace Maldact.Tests.BackendTests.MLTests.TrainingTests.DataTests;

public class DatasetManifestTests
{
    // Lightweight wrapper to safely create and destroy physical test directories
    private class TempDirectory : IDisposable
    {
        public string DirectoryPath { get; }

        public TempDirectory()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(DirectoryPath);
        }

        public void CreateFile(string fileName)
        {
            File.Create(Path.Combine(DirectoryPath, fileName)).Dispose();
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
    }

    public class EventManifestTests
    {
        [Fact]
        public void Validate_StartAfterEnd_ThrowsInvalidDataException()
        {
            // arrange
            var ev = new EventManifest { ClassLabel = "Dog", StartMs = 1000, EndMs = 500 };

            // act
            Action act = () => ev.Validate();

            // assert
            act.Should().Throw<InvalidDataException>().WithMessage("*invalid timestamps*");
        }

        [Fact]
        public void ToAbsoluteEvent_MissingClassInLookup_ThrowsInvalidDataException()
        {
            // arrange
            var ev = new EventManifest { ClassLabel = "Dragon", StartMs = 0, EndMs = 100 };
            var lookup = new Dictionary<string, ClassificationClass>(); // Empty lookup

            // act
            Action act = () => ev.ToAbsoluteEvent(lookup);

            // assert
            act.Should().Throw<InvalidDataException>().WithMessage("*not defined in your model specification*");
        }

        [Fact]
        public void ToAbsoluteEvent_ValidEvent_MapsCorrectly()
        {
            // arrange
            var ev = new EventManifest { ClassLabel = "Dog", StartMs = 500, EndMs = 1500 };
            var expectedClass = new ClassificationClass("Dog");
            var lookup = new Dictionary<string, ClassificationClass> { { "Dog", expectedClass } };

            // act
            var result = ev.ToAbsoluteEvent(lookup);

            // assert
            result.Class.Should().BeSameAs(expectedClass);
            result.StartOffset.TotalMilliseconds.Should().Be(500);
            result.Duration.TotalMilliseconds.Should().Be(1000);
        }
    }

    public class StreamManifestTests
    {
        [Fact]
        public void Validate_MissingFile_ThrowsFileNotFoundException()
        {
            using var tempDir = new TempDirectory();
            // arrange
            var stream = new StreamManifest { FileName = "missing_recording.bin", DurationMs = 10000 };

            // act
            Action act = () => stream.Validate(tempDir.DirectoryPath);

            // assert
            act.Should().Throw<FileNotFoundException>().WithMessage("*missing binary file*");
        }

        [Fact]
        public void Validate_EventExceedsStreamDuration_ThrowsInvalidDataException()
        {
            using var tempDir = new TempDirectory();
            tempDir.CreateFile("valid_recording.bin");

            // arrange
            var stream = new StreamManifest
            {
                FileName = "valid_recording.bin",
                DurationMs = 5000, // 5 seconds
                Events = new List<EventManifest>
                {
                    new() { ClassLabel = "Dog", StartMs = 4000, EndMs = 6000 } // Ends after stream is over
                }
            };

            // act
            Action act = () => stream.Validate(tempDir.DirectoryPath);

            // assert
            act.Should().Throw<InvalidDataException>().WithMessage("*ends after the stream duration*");
        }

        [Fact]
        public void Validate_ValidStream_PassesWithoutThrowing()
        {
            using var tempDir = new TempDirectory();
            tempDir.CreateFile("valid_recording.bin");

            // arrange
            var stream = new StreamManifest
            {
                FileName = "valid_recording.bin",
                DurationMs = 5000,
                Events = new List<EventManifest>
                {
                    new() { ClassLabel = "Dog", StartMs = 1000, EndMs = 2000 } 
                }
            };

            // act & assert
            Action act = () => stream.Validate(tempDir.DirectoryPath);
            act.Should().NotThrow();
        }
    }

    public class DatasetRootManifestTests
    {
        [Fact]
        public void Validate_InvalidSampleRate_ThrowsInvalidDataException()
        {
            // arrange
            var manifest = CreateValidManifest();
            var badManifest = manifest with { GlobalSampleRateHz = -10 };

            // act
            Action act = () => badManifest.Validate("C:/fake/path");

            // assert
            act.Should().Throw<InvalidDataException>().WithMessage("*greater than zero*");
        }

        [Fact]
        public void Validate_MissingTrainingStreams_ThrowsInvalidDataException()
        {
            // arrange
            var manifest = CreateValidManifest() with { TrainingStreams = new List<StreamManifest>() };

            // act
            Action act = () => manifest.Validate("C:/fake/path");

            // assert
            act.Should().Throw<InvalidDataException>().WithMessage("*no training streams*");
        }
        
        [Fact]
        public void Validate_MissingCrossValidationStreams_ThrowsInvalidDataException()
        {
            // arrange
            var manifest = CreateValidManifest() with { CrossValidationStreams = new List<StreamManifest>() };

            // act
            Action act = () => manifest.Validate("C:/fake/path");

            // assert
            act.Should().Throw<InvalidDataException>().WithMessage("*no cross-validation streams*");
        }

        [Fact]
        public void Validate_InvalidPreprocessingContract_TriggersDataAnnotationValidation()
        {
            using var tempDir = new TempDirectory();
            tempDir.CreateFile("train.bin");
            tempDir.CreateFile("val.bin");

            // arrange
            var manifest = CreateValidManifest();
            
            // Break the contract: A pipeline must contain at least 1 step based on your [MinLength(1)] attribute
            var badPreprocessing = new PreprocessingContract
            {
                InputDimension = 10,
                InputSampleRate = 100,
                FinalReshaping = null,  // null final preprocessing!
                Pipeline = new List<PreprocessingStep>()
            };

            var badManifest = manifest with { Preprocessing = badPreprocessing };

            // act
            Action act = () => badManifest.Validate(tempDir.DirectoryPath);

            // assert
            act.Should().Throw<InvalidDataException>()
                .WithMessage("*preprocessing contract is invalid*");
        }

        // Helper to quickly spin up a structurally valid root manifest
        private static DatasetManifest CreateValidManifest()
        {
            return new DatasetManifest
            {
                DatasetName = "Test_Dataset",
                GlobalSampleRateHz = 100,
                Classes = new[] { "ClassA" },
                TrainingStreams = new List<StreamManifest>
                {
                    new() { FileName = "train.bin", DurationMs = 1000 }
                },
                CrossValidationStreams = new List<StreamManifest>
                {
                    new() { FileName = "val.bin", DurationMs = 1000 }
                }
            };
        }
    }
}