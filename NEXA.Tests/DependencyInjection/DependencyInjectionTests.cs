using Microsoft.Extensions.DependencyInjection;
using NEXA.Abstractions;
using NEXA.Adapters.Output;
using NEXA.Application;
using NEXA.DependencyInjection;
using NEXA.Domain.Click;
using NEXA.Domain.EarsMute;
using NEXA.Domain.Grab;
using NEXA.Domain.Lock;
using NEXA.Domain.MonitorThrow;
using NEXA.Domain.Mute;
using NEXA.Domain.Scroll;
using NEXA.Domain.TwoHand;
using NEXA.Domain.Undo;
using NEXA.Domain.Volume;
using NEXA.Face;
using NEXA.Hand;
using NEXA.Object;
using NEXA.UI;
using Xunit;

namespace NEXA.Tests.DependencyInjection;

/// <summary>
/// Unit tests validating that the Dependency Injection container correctly registers and resolves all components.
/// </summary>
public class DependencyInjectionTests
{
    [Fact]
    public void ServiceCollection_RegistersAndResolvesAllNexaComponents()
    {
        ServiceCollection services = new();
        services.AddNexaServices("dummy_palm.onnx", "dummy_landmark.onnx");

        using ServiceProvider provider = services.BuildServiceProvider();

        // 1. Verify Sinks & Adapters
        IInputSink inputSink = provider.GetRequiredService<IInputSink>();
        Assert.NotNull(inputSink);

        IAudioSink audioSink = provider.GetRequiredService<IAudioSink>();
        Assert.NotNull(audioSink);

        IScreenshotSink screenshotSink = provider.GetRequiredService<IScreenshotSink>();
        Assert.NotNull(screenshotSink);

        // 2. Verify Face Tracker & Renderers
        FaceTracker faceTracker = provider.GetRequiredService<FaceTracker>();
        Assert.NotNull(faceTracker);

        HandMeshRenderer handRenderer = provider.GetRequiredService<HandMeshRenderer>();
        Assert.NotNull(handRenderer);

        FaceMeshRenderer faceRenderer = provider.GetRequiredService<FaceMeshRenderer>();
        Assert.NotNull(faceRenderer);

        HudRenderer hudRenderer = provider.GetRequiredService<HudRenderer>();
        Assert.NotNull(hudRenderer);

        // 3. Verify Domain Controllers
        MouseController mouseController = provider.GetRequiredService<MouseController>();
        Assert.NotNull(mouseController);

        ScrollController scrollController = provider.GetRequiredService<ScrollController>();
        Assert.NotNull(scrollController);

        VolumeController volumeController = provider.GetRequiredService<VolumeController>();
        Assert.NotNull(volumeController);

        WindowGrabController windowGrabController = provider.GetRequiredService<WindowGrabController>();
        Assert.NotNull(windowGrabController);

        TwoHandGestureController twoHandController = provider.GetRequiredService<TwoHandGestureController>();
        Assert.NotNull(twoHandController);

        MonitorThrowController monitorThrowController = provider.GetRequiredService<MonitorThrowController>();
        Assert.NotNull(monitorThrowController);

        LockSequenceController lockController = provider.GetRequiredService<LockSequenceController>();
        Assert.NotNull(lockController);

        CircleUndoController circleUndoController = provider.GetRequiredService<CircleUndoController>();
        Assert.NotNull(circleUndoController);

        ShhhMuteController shhhMuteController = provider.GetRequiredService<ShhhMuteController>();
        Assert.NotNull(shhhMuteController);

        HearNoEvilController hearNoEvilController = provider.GetRequiredService<HearNoEvilController>();
        Assert.NotNull(hearNoEvilController);

        VirtualObjectController virtualObject = provider.GetRequiredService<VirtualObjectController>();
        Assert.NotNull(virtualObject);

        // 4. Verify Command Handler
        KeyboardCommandHandler commandHandler = provider.GetRequiredService<KeyboardCommandHandler>();
        Assert.NotNull(commandHandler);
    }
}
