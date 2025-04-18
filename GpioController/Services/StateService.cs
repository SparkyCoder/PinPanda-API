using GpioController.Commands.Request;
using GpioController.Commands.Results;
using GpioController.Exceptions;
using GpioController.Extensions;
using GpioController.Factories;
using GpioController.Models;

namespace GpioController.Services;

public class StateService : IStateService
{
    private readonly ICommandFactory commandFactory;
    private readonly IGpioService gpioService;
    private readonly ITokenManagementService tokenManagementService;

    public Action<IEnumerable<GpioSetRequest>> ValidateUpdateMultipleStates;

    public StateService(ICommandFactory commandFactory, IGpioService gpioService, ITokenManagementService tokenManagementService)
    {
        this.commandFactory = commandFactory;
        this.gpioService = gpioService;
        this.tokenManagementService = tokenManagementService;
        
        ValidateUpdateMultipleStates = ValidateUpdateRequests;
    }

    public GpioReadResult GetStateByGpioId(int chipsetId, int gpioId)
    {
        gpioService.GetGpioById(chipsetId, gpioId);
        
        var command = commandFactory.GetCommand<GpioReadRequest, GpioReadResult>();
        var result = command.Execute(new GpioReadRequest
        {
            Chipset = chipsetId,
            Gpio = gpioId
        });
        
        return result;
    }

    public void UpdateSingleState(GpioSetRequest request)
    {
        gpioService.GetGpioById(request.Chipset, request.Gpios.First());

        request.CancellationToken = tokenManagementService.CreateToken(request);
        var command = commandFactory.GetCommand<GpioSetRequest, GpioSetResult>();
        command.Execute(request);
    }

    public void UpdateMultipleStates(IEnumerable<GpioSetRequest> updateRequests)
    {
        ValidateUpdateMultipleStates(updateRequests);

        new Action(() =>
        {
            foreach (var request in updateRequests)
            {
                request.CancellationToken = tokenManagementService.CreateToken(request);
                var command = commandFactory.GetCommand<GpioSetRequest, GpioSetResult>();
                command.Execute(request);
                tokenManagementService.RemoveCompletedTask(request);
            }
        }).StartOnBackgroundThread();
    }

    private void ValidateUpdateRequests(IEnumerable<GpioSetRequest> updateRequests)
    {
        foreach (var request in updateRequests)
        {
            ValidateRequest(request.Gpios, request.Chipset, request.State);
        }
    }

    private void ValidateRequest(IEnumerable<int> gpioIdsToValidate, int chipsetId, string state)
    {
        foreach (var gpioId in gpioIdsToValidate)
        {
            ValidateIndividualGpio(chipsetId, gpioId, state);
        }
    }

    private void ValidateIndividualGpio(int chipsetId, int gpioId, string state)
    {
        var gpios = gpioService.GetGpios();

        if (!gpios.Chipset(chipsetId).HasGpio(gpioId))
            throw new NoGpiosFoundOnChipsetException(gpioId, chipsetId);

        if (!State.CanParse(state))
            throw new InvalidStateException(state);
    }
}
