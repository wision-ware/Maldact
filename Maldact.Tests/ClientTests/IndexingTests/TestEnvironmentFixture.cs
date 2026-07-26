using Maldact.Client.Indexing;

namespace Maldact.Tests.ClientTests.IndexingTests;

/// <summary>
/// Provides a self-cleaning, isolated file system sandbox for configuration indexing tests.
/// </summary>
public sealed class TestEnvironmentFixture : IDisposable
{
    /// <summary>
    /// Gets the absolute path to the isolated test directory.
    /// </summary>
    public string TestDirectory { get; }

    /// <summary>
    /// Initializes a new isolated test environment.
    /// </summary>
    public TestEnvironmentFixture()
    {
        TestDirectory = Path.Combine(Path.GetTempPath(), "Maldact_Test_Sandbox", Guid.NewGuid().ToString());
        Directory.CreateDirectory(TestDirectory);
        
        IndexingInvariantManager.OverrideSandboxPath(TestDirectory);
    }

    /// <summary>
    /// Cleans up the test environment variables and forcefully deletes the sandbox directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(TestDirectory))
        {
            try
            {
                Directory.Delete(TestDirectory, recursive: true);
            }
            catch
            {
                // swallow aggressive file locks during fast concurrent test teardowns
            }
        }
    }
}