namespace Wolfgang.FileTools;

internal static class ExitCode
{

    public static readonly int Success = 0;
    public static readonly int CommandLineError = 2;
    public static readonly int UnhandledException = 10;
    public static readonly int ApplicationError = 11;
}