namespace AI.MIS.UnitTests.Extended.Support;

public static class TestTraits
{
    /// <summary>
    /// Tests in this category describe the CORRECT behaviour for a suspected defect found by reading the source.
    /// They are expected to FAIL until the defect is fixed. Run the rest with:
    ///   dotnet test --filter "Category!=SuspectedDefect"
    /// </summary>
    public const string SuspectedDefect = "SuspectedDefect";
}
