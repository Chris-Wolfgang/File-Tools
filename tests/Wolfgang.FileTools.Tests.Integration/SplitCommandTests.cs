using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Wolfgang.FileTools.Command;
using Xunit;

namespace Wolfgang.FileTools.Tests.Integration;

public class SplitCommandTests : IDisposable
{
    private readonly string _testDirectory;

    public SplitCommandTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SplitCommandTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region Regex Parsing Tests

    [Theory]
    [InlineData("1024", 1024)]
    [InlineData("100", 100)]
    [InlineData("1", 1)]
    [InlineData("999999", 999999)]
    public async Task MaxBytes_ValidNumericFormat_ParsesCorrectly(string maxBytes, int expectedBytes)
    {
        // Arrange
        var testFile = CreateTestFile("test.txt", 100);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = maxBytes };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(0, result); // Success
    }

    [Theory]
    [InlineData("10K", 10 * 1024)]
    [InlineData("10k", 10 * 1024)]
    [InlineData("5M", 5 * 1024 * 1024)]
    [InlineData("5m", 5 * 1024 * 1024)]
    [InlineData("1G", 1L * 1024 * 1024 * 1024)]
    [InlineData("1g", 1L * 1024 * 1024 * 1024)]
    public async Task MaxBytes_ValidUnitFormat_ParsesCorrectly(string maxBytes, long expectedBytes)
    {
        // Arrange
        var testFile = CreateTestFile("test.txt", 100);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = maxBytes };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(0, result); // Success
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("10X")]
    [InlineData("K10")]
    [InlineData("10.5")]
    [InlineData("10 K")]
    [InlineData("-10")]
    [InlineData("10KM")]
    public async Task MaxBytes_InvalidFormat_ReturnsCommandLineError(string maxBytes)
    {
        // Arrange
        var testFile = CreateTestFile("test.txt", 100);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = maxBytes };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(2, result); // CommandLineError
    }

    #endregion

    #region Unit Conversion Tests

    [Fact]
    public async Task MaxBytes_KilobyteConversion_CalculatesCorrectly()
    {
        // Arrange
        var testFile = CreateTestFile("test.txt", 2048);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = "1K" };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(0, result);
        var outputFiles = Directory.GetFiles(_testDirectory, "test.*.txt");
        Assert.Equal(2, outputFiles.Length); // 2048 bytes / 1024 = 2 files
    }

    [Fact]
    public async Task MaxBytes_MegabyteConversion_CalculatesCorrectly()
    {
        // Arrange
        var testFile = CreateTestFile("test.txt", 3 * 1024 * 1024);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = "1M" };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(0, result);
        var outputFiles = Directory.GetFiles(_testDirectory, "test.*.txt");
        Assert.Equal(3, outputFiles.Length); // 3MB / 1MB = 3 files
    }

    [Fact]
    public async Task MaxBytes_GigabyteConversion_CalculatesCorrectly()
    {
        // Arrange - Create a smaller test for G unit
        var testFile = CreateTestFile("test.txt", 2 * 1024 * 1024);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = "1G" };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(0, result);
        var outputFiles = Directory.GetFiles(_testDirectory, "test.*.txt");
        Assert.Single(outputFiles); // 2MB < 1GB = 1 file
    }

    #endregion

    #region File Splitting Tests

    [Fact]
    public async Task SplitFile_ExactMultiple_CreatesSplitFiles()
    {
        // Arrange
        var testFile = CreateTestFile("test.txt", 1000);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = "500" };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(0, result);
        var outputFiles = Directory.GetFiles(_testDirectory, "test.*.txt").OrderBy(f => f).ToArray();
        Assert.Equal(2, outputFiles.Length);
        Assert.Equal(500, new FileInfo(outputFiles[0]).Length);
        Assert.Equal(500, new FileInfo(outputFiles[1]).Length);
    }

    [Fact]
    public async Task SplitFile_NotExactMultiple_CreatesCorrectNumberOfFiles()
    {
        // Arrange
        var testFile = CreateTestFile("test.txt", 1500);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = "500" };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(0, result);
        var outputFiles = Directory.GetFiles(_testDirectory, "test.*.txt").OrderBy(f => f).ToArray();
        Assert.Equal(3, outputFiles.Length);
        Assert.Equal(500, new FileInfo(outputFiles[0]).Length);
        Assert.Equal(500, new FileInfo(outputFiles[1]).Length);
        Assert.Equal(500, new FileInfo(outputFiles[2]).Length);
    }

    [Fact]
    public async Task SplitFile_PreservesContent()
    {
        // Arrange
        var content = "This is a test file with some content that will be split.";
        var testFile = CreateTestFileWithContent("test.txt", content);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = "20" };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(0, result);
        var outputFiles = Directory.GetFiles(_testDirectory, "test.*.txt").OrderBy(f => f).ToArray();
        
        var rebuiltContent = new StringBuilder();
        foreach (var file in outputFiles)
        {
            rebuiltContent.Append(File.ReadAllText(file));
        }
        
        Assert.Equal(content, rebuiltContent.ToString());
    }

    [Fact]
    public async Task SplitFile_OutputFilesHavePaddedNumbers()
    {
        // Arrange
        var testFile = CreateTestFile("test.txt", 500);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = "100" };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(0, result);
        var outputFiles = Directory.GetFiles(_testDirectory, "test.*.txt").OrderBy(f => f).ToArray();
        Assert.Equal(5, outputFiles.Length);
        Assert.Contains("test.000.txt", outputFiles[0]);
        Assert.Contains("test.001.txt", outputFiles[1]);
        Assert.Contains("test.004.txt", outputFiles[4]);
    }

    [Fact]
    public async Task SplitFile_NoExtension_OutputFilesHaveCorrectNaming()
    {
        // Arrange
        var testFile = CreateTestFile("testfile", 300);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = "100" };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(0, result);
        // Exclude the original file by looking for files with numbered extensions
        var outputFiles = Directory.GetFiles(_testDirectory, "testfile.*")
            .Where(f => !f.EndsWith("testfile"))
            .OrderBy(f => f)
            .ToArray();
        Assert.Equal(3, outputFiles.Length);
        Assert.Contains("testfile.000", outputFiles[0]);
        Assert.Contains("testfile.001", outputFiles[1]);
        Assert.Contains("testfile.002", outputFiles[2]);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task SplitFile_EmptyFile_CreatesNoOutputFiles()
    {
        // Arrange
        var testFile = CreateTestFile("empty.txt", 0);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = "100" };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(0, result);
        var outputFiles = Directory.GetFiles(_testDirectory, "empty.*.txt");
        Assert.Empty(outputFiles);
    }

    [Fact]
    public async Task SplitFile_SmallerThanMaxBytes_CreatesOneFile()
    {
        // Arrange
        var testFile = CreateTestFile("small.txt", 50);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = "100" };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(0, result);
        var outputFiles = Directory.GetFiles(_testDirectory, "small.*.txt");
        Assert.Single(outputFiles);
        Assert.Equal(50, new FileInfo(outputFiles[0]).Length);
    }

    [Fact]
    public async Task SplitFile_ExactlyMaxBytes_CreatesOneFile()
    {
        // Arrange
        var testFile = CreateTestFile("exact.txt", 100);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = "100" };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(0, result);
        var outputFiles = Directory.GetFiles(_testDirectory, "exact.*.txt");
        Assert.Single(outputFiles);
        Assert.Equal(100, new FileInfo(outputFiles[0]).Length);
    }

    [Fact]
    public async Task SplitFile_VeryLargeMaxBytes_CreatesOneFile()
    {
        // Arrange
        var testFile = CreateTestFile("test.txt", 1000);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = "10M" };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(0, result);
        var outputFiles = Directory.GetFiles(_testDirectory, "test.*.txt");
        Assert.Single(outputFiles);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task SplitFile_NonExistentFile_ReturnsError()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.txt");
        var command = new SplitCommand { SourcePath = nonExistentFile, MaxBytes = "100" };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(11, result); // ApplicationError
    }

    [Fact]
    public async Task SplitFile_InvalidMaxBytesFormat_ReturnsCommandLineError()
    {
        // Arrange
        var testFile = CreateTestFile("test.txt", 100);
        var command = new SplitCommand { SourcePath = testFile, MaxBytes = "invalid" };
        var console = new TestConsole();

        // Act
        var result = await InvokeCommand(command, console);

        // Assert
        Assert.Equal(2, result); // CommandLineError
    }

    #endregion

    #region Helper Methods

    private string CreateTestFile(string fileName, int sizeInBytes)
    {
        var filePath = Path.Combine(_testDirectory, fileName);
        var content = new byte[sizeInBytes];
        new Random().NextBytes(content);
        File.WriteAllBytes(filePath, content);
        return filePath;
    }

    private string CreateTestFileWithContent(string fileName, string content)
    {
        var filePath = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private async Task<int> InvokeCommand(SplitCommand command, TestConsole console)
    {
        var method = typeof(SplitCommand).GetMethod("OnExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new InvalidOperationException("OnExecuteAsync method not found");
        }

        var task = method.Invoke(command, new object[] { console }) as Task<int>;
        if (task == null)
        {
            throw new InvalidOperationException("Method invocation failed");
        }

        return await task;
    }

    private class TestConsole : IConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();

        public string Output => _output.ToString();
        public string ErrorOutput => _error.ToString();

        public TextWriter Out => new StringWriter(_output);
        public TextWriter Error => new StringWriter(_error);
        public TextReader In => TextReader.Null;
        public bool IsInputRedirected => false;
        public bool IsOutputRedirected => false;
        public bool IsErrorRedirected => false;
        public ConsoleColor ForegroundColor { get; set; }
        public ConsoleColor BackgroundColor { get; set; }

        public event ConsoleCancelEventHandler? CancelKeyPress;

        public void ResetColor() { }

        public void Write(string value)
        {
            _output.Append(value);
        }

        public void WriteLine(string value)
        {
            _output.AppendLine(value);
        }

        public void WriteLine(object value)
        {
            _output.AppendLine(value?.ToString());
        }
    }

    #endregion
}
