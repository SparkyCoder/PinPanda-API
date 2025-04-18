using GpioController.Commands.Request;
using GpioController.Commands.Results;
using GpioController.Factories;
using GpioController.Models;

namespace GpioController.Services;

public class TokenManagementService(ICommandFactory commandFactory, ILogger<TokenManagementService> logger) : ITokenManagementService
{
    private readonly List<ActiveTask> activeTokenSources = new();
    private readonly Lock lockingMechanism = new();

    public CancellationToken CreateToken(GpioSetRequest request)
    {
        var cts = new CancellationTokenSource();
        
        lock (lockingMechanism)
        {
            activeTokenSources.Add(new ActiveTask
            {
                TokenSource = cts,
                ActiveRequest = request
                
            });

            PruneCancelled();
        }
        
        return cts.Token;
    }

    public void RemoveCompletedTask(GpioSetRequest request)
    {
        lock (lockingMechanism)
        {
            activeTokenSources.RemoveAll(activeTask => activeTask.ActiveRequest == request );
        }
    }

    public void CancelAll()
    {
        lock (lockingMechanism)
        {
            foreach (var cts in activeTokenSources.Where(cts => !cts.TokenSource.IsCancellationRequested))
            {
                cts.TokenSource.Cancel();
                logger.LogWarning($"Cancelling actions for Chipset {cts.ActiveRequest.Chipset} - GPIO {string.Join(',',cts.ActiveRequest.Gpios)}");
                RunOppositeRequestToTurnOffAnyActiveGpios(cts.ActiveRequest);
                Thread.Sleep(50);
            }

            activeTokenSources.Clear(); 
        }
    }

    private void RunOppositeRequestToTurnOffAnyActiveGpios(GpioSetRequest request)
    {
        request.State = State.ParseOpposite(request.State).ToString();
        request.Options = null;
        var command = commandFactory.GetCommand<GpioSetRequest, GpioSetResult>();
        command.Execute(request);
    }

    private void PruneCancelled()
    {
        activeTokenSources.RemoveAll(cts => cts.TokenSource.IsCancellationRequested);
    }
}