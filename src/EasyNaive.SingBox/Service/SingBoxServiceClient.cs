using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace EasyNaive.SingBox.Service;

public sealed class SingBoxServiceClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromMilliseconds(700);

    public Task<SingBoxServiceResponse?> StartAsync(
        string executablePath,
        string configPath,
        string workingDirectory,
        string logPath,
        string sessionPath,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(new SingBoxServiceRequest
        {
            Command = SingBoxServiceProtocol.StartCommand,
            ExecutablePath = executablePath,
            ConfigPath = configPath,
            WorkingDirectory = workingDirectory,
            LogPath = logPath,
            SessionPath = sessionPath
        }, cancellationToken);
    }

    public Task<SingBoxServiceResponse?> StopAsync(string sessionPath, CancellationToken cancellationToken = default)
    {
        return SendAsync(new SingBoxServiceRequest
        {
            Command = SingBoxServiceProtocol.StopCommand,
            SessionPath = sessionPath
        }, cancellationToken);
    }

    public Task<SingBoxServiceResponse?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync(new SingBoxServiceRequest
        {
            Command = SingBoxServiceProtocol.StatusCommand
        }, cancellationToken);
    }

    private static async Task<SingBoxServiceResponse?> SendAsync(
        SingBoxServiceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                SingBoxServiceProtocol.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var connectCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCancellationTokenSource.CancelAfter(DefaultConnectTimeout);
            await pipe.ConnectAsync(connectCancellationTokenSource.Token);

            var json = JsonSerializer.Serialize(request, SerializerOptions);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
            {
                AutoFlush = true
            };
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);

            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
            var responseLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(responseLine))
            {
                return new SingBoxServiceResponse
                {
                    Success = false,
                    Message = "EasyNaive service returned an empty response."
                };
            }

            return JsonSerializer.Deserialize<SingBoxServiceResponse>(responseLine, SerializerOptions)
                   ?? new SingBoxServiceResponse
                   {
                       Success = false,
                       Message = "EasyNaive service returned an invalid response."
                   };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
