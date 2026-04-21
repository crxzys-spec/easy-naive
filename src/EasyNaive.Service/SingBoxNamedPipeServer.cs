using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using EasyNaive.SingBox.Service;

namespace EasyNaive.Service;

internal sealed class SingBoxNamedPipeServer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SingBoxServiceRuntime _runtime;

    public SingBoxNamedPipeServer(SingBoxServiceRuntime runtime)
    {
        _runtime = runtime;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = CreatePipe();
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
                await HandleConnectionAsync(pipe, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                // Keep the service available even if one client sends a bad request.
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            AutoFlush = true
        };

        var requestLine = await reader.ReadLineAsync(cancellationToken);
        SingBoxServiceResponse response;

        try
        {
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                throw new InvalidOperationException("Empty service request.");
            }

            var request = JsonSerializer.Deserialize<SingBoxServiceRequest>(requestLine, SerializerOptions)
                          ?? throw new InvalidOperationException("Invalid service request.");

            response = await DispatchAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            response = new SingBoxServiceResponse
            {
                Success = false,
                Message = ex.Message
            };
        }

        await writer.WriteLineAsync(JsonSerializer.Serialize(response, SerializerOptions).AsMemory(), cancellationToken);
    }

    private Task<SingBoxServiceResponse> DispatchAsync(SingBoxServiceRequest request, CancellationToken cancellationToken)
    {
        return request.Command.ToLowerInvariant() switch
        {
            SingBoxServiceProtocol.StartCommand => _runtime.StartAsync(request, cancellationToken),
            SingBoxServiceProtocol.StopCommand => _runtime.StopAsync(request.SessionPath, cancellationToken),
            SingBoxServiceProtocol.StatusCommand => Task.FromResult(_runtime.GetStatus()),
            _ => Task.FromResult(new SingBoxServiceResponse
            {
                Success = false,
                Message = $"Unknown service command: {request.Command}"
            })
        };
    }

    private static NamedPipeServerStream CreatePipe()
    {
        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            SingBoxServiceProtocol.PipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 4096,
            outBufferSize: 4096,
            pipeSecurity);
    }
}
