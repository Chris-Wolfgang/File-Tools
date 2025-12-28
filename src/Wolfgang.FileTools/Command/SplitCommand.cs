using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;

namespace Wolfgang.FileTools.Command;

[Command
(
    Description = "Splits a file into multiple files of specified size.",

    ResponseFileHandling = ResponseFileHandling.ParseArgsAsLineSeparated

)]
internal class SplitCommand
{


    [FileExists]
    [Required]
    [Argument(0, Description = "Filename and path to the file to process")]
    public required string SourcePath { get; [UsedImplicitly] set; }



    [Required]
    [Argument(1, Description = "Maximum size of each split file in bytes. Note you can specify a number followed by K, M or G. i.e. 20M will split the file into multiple files of at most 20 megabytes")]
    public required string? MaxBytes { get; [UsedImplicitly] set; }



    //[Argument(2, Description = "Filename and path to receive the output")]
    //public required string DestinationPath { get; set; }



    

    [UsedImplicitly]
    internal async Task<int> OnExecuteAsync
    (
        IConsole console
    )
    {
        try
        {
            var regex = new Regex(@"^(?<bytes>\d+)(?<units>([KMG])?)$", RegexOptions.IgnoreCase);
            var match = regex.Match(MaxBytes!);
            if (!match.Success)
            {
                Console.WriteLine("MaxBytes must be a number optionally followed by K, M, or G (e.g., 20M for 20 megabytes).");
                return ExitCode.CommandLineError;
            }

            long maxBytes = int.Parse(match.Groups["bytes"].Value);
            var units = match.Groups["units"].Value.ToUpperInvariant();
            switch (units)
            {
                case "K":
                    maxBytes *= 1024;
                    break;
                case "M":
                    maxBytes *= 1024 * 1024;
                    break;
                case "G":
                    maxBytes *= 1024 * 1024 * 1024;
                    break;
            }

            if (maxBytes <= 0)
            {
                console.WriteLine("MaxBytes must be greater than zero.");
                return ExitCode.CommandLineError;
            }
            var pieceCount = 0;
            var bytesReadCount = 0L;

            await using var reader = File.OpenRead(SourcePath);

            var fileSize = reader.Length;

            while (bytesReadCount < fileSize)
            {
                var outPath = GetOutputFilePath(pieceCount);

                var bufferSize = bytesReadCount + maxBytes > reader.Length
                    ? (int)(reader.Length - bytesReadCount)
                    : maxBytes;


                console.Write($"Creating file '{outPath}'");
                await using (var writer = File.Create(outPath))
                {
                    var buffer = new byte[bufferSize];

                    console.WriteLine($"Buffer size {bufferSize}, position {bytesReadCount}, length {buffer.Length}");

                    await reader.ReadExactlyAsync(buffer, 0, buffer.Length);
                    await writer.WriteAsync(buffer);
                    await writer.FlushAsync();
                }

                console.WriteLine($" size {bufferSize} bytes");
                pieceCount++;
                bytesReadCount += bufferSize;
            }

            return ExitCode.Success;
        }
        catch (Exception e)
        {
            console.WriteLine(e);
            return ExitCode.ApplicationError;
        }
    }

    private string GetOutputFilePath(int pieceCount)
    {
        var outPath = SourcePath;
        if (Path.HasExtension(outPath))
        {
            var ext = Path.GetExtension(outPath);
            outPath = Path.ChangeExtension(outPath, pieceCount.ToString("D3"));
            outPath += ext;
        }
        else
        {
            outPath = Path.ChangeExtension(outPath, pieceCount.ToString("D3"));
        }

        return outPath;
    }
}