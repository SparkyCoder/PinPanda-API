using FakeItEasy;
using GpioController.Commands;
using GpioController.Commands.Request;
using GpioController.Commands.Results;
using GpioController.Extensions;
using GpioController.Parsers;
using GpioController.Services;

namespace GpioControllerTests.Commands;

public class GpioSetCommandTests
{
    private IParser<GpioSetResult>? parser;
    private ITerminalService? terminalService;
    
    private GpioSetCommand GetSystemUnderTest()
    {
        parser = A.Fake<IParser<GpioSetResult>>();
        terminalService = A.Fake<ITerminalService>();
        
        return new GpioSetCommand(parser, terminalService);
    }

    [Fact]
    public void Execute_ShouldRunTerminalCommandCorrectly_ForEachRepeatedNumber()
    {
        var sut = GetSystemUnderTest();

        var request = new GpioSetRequest
        {
            Chipset = 1,
            Gpios = [81, 93],
            State = "LoW",
            Options = new OptionalSettings
            {
                Milliseconds = 50,
                RepeatTimes = 3
            }
        };
        
        sut.Execute(request);

        A.CallTo(() => terminalService.RunCommand(A<string>.That.Matches(command => command.Contains("93=0") && command.Contains("81=0"))))
            .MustHaveHappenedANumberOfTimesMatching(times => times == 3);
        
        A.CallTo(() => terminalService.RunCommand(A<string>.That.Matches(command => command.Contains("93=1") && command.Contains("81=1"))))
            .MustHaveHappenedANumberOfTimesMatching(times => times == 3);
    }
    
    [Fact]
    public void Execute_ShouldRunTerminalCommandCorrectly_WhenRunningAnOddNumberOfTimes()
    {
        var sut = GetSystemUnderTest();

        var request = new GpioSetRequest
        {
            Chipset = 1,
            Gpios = [81],
            State = "Low",
            Options = new OptionalSettings
            {
                Milliseconds = 50,
                RepeatTimes = 3
            }
        };
        
        sut.Execute(request);

        A.CallTo(() => terminalService.RunCommand(A<string>.That.Matches(command => command.Contains("81=0"))))
            .MustHaveHappenedANumberOfTimesMatching(times => times == 3);
        
        A.CallTo(() => terminalService.RunCommand(A<string>.That.Matches(command => command.Contains("81=1"))))
            .MustHaveHappenedANumberOfTimesMatching(times => times == 3);
    }
    
    [Fact]
    public void Execute_ShouldRunTerminalCommandCorrectly_WithRequestWithoutAnyOptions()
    {
        var sut = GetSystemUnderTest();

        var request = new GpioSetRequest
        {
            Chipset = 1,
            Gpios = [81],
            State = "Low"
        };
        
        sut.Execute(request);

        A.CallTo(() => terminalService.RunCommand(A<string>._))
            .MustHaveHappenedANumberOfTimesMatching(times => times == 1);
    }
    
    [Fact]
    public void Execute_ShouldExitEarly_WhenTokenIsCancelled()
    {
        var sut = GetSystemUnderTest();
        var tokenSource = new CancellationTokenSource();

        var request = new GpioSetRequest
        {
            Chipset = 1,
            Gpios = [81],
            State = "Low",
            Options = new OptionalSettings
            {
                Milliseconds = 500,
                RepeatTimes = 3
            }
        };

        request.CancellationToken = tokenSource.Token;
        
        new Action(() =>
        {
            sut.Execute(request);
        }).StartOnBackgroundThread();
        
        Thread.Sleep(50);
        
        tokenSource.Cancel();

        A.CallTo(() => terminalService.RunCommand(A<string>._))
            .MustHaveHappenedANumberOfTimesMatching(times => times == 1);
    }
}