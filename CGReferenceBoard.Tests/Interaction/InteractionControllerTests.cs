using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using CGReferenceBoard.Interaction;
using CGReferenceBoard.Interaction.States;
using CGReferenceBoard.Services;
using CGReferenceBoard.Services.Abstractions;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

public class InteractionControllerTests
{
    [Fact]
    public void Controller_StartsInIdleState()
    {
        var ctx = new FakeInteractionContext();
        var controller = new InteractionController(ctx, new IdleState());
        Assert.IsType<IdleState>(controller.CurrentState);
    }

    [Fact]
    public void Controller_TransitionsOnGoTo()
    {
        var ctx = new FakeInteractionContext();
        var next = new FakeState();
        var idle = new IdleStateWithTransition(StateTransition.GoTo(next));
        var controller = new InteractionController(ctx, idle);

        controller.OnPointerPressed(null!);
        Assert.IsType<FakeState>(controller.CurrentState);
    }

    [Fact]
    public void Controller_StaysOnStay()
    {
        var ctx = new FakeInteractionContext();
        var idle = new IdleStateWithTransition(StateTransition.Stay);
        var controller = new InteractionController(ctx, idle);

        controller.OnPointerPressed(null!);
        Assert.IsType<IdleStateWithTransition>(controller.CurrentState);
    }
}

internal sealed class FakeInteractionContext : IInteractionContext
{
    public IViewportService ViewportOverride { get; set; } = new ViewportService();
    public MainWindowViewModel Vm { get; } = MainWindowViewModel.CreateWithDI(false);
    public SelectionService Selection => Vm.SelectionService;
    public IViewportService Viewport => ViewportOverride;
    public IHistoryService History => null!;
    public Point ScreenToCanvas(Point p) => p;
    public CellViewModel? HitTestCell(Point p) => null;
    public IReadOnlyList<CellViewModel> HitTestCellsInRect(Rect r) => Array.Empty<CellViewModel>();
    public IReadOnlyList<AnnotationViewModel> HitTestAnnotationsInRect(Rect r) => Array.Empty<AnnotationViewModel>();
    public void SetAnnotationMarqueeRect(Rect? r) { }
    public void SetCellMarqueeRect(Rect? r) { }
    public void SetPointerCapture(IPointer? p, bool c) { }
    public void RequestViewportUpdate() { }
    public void NotifyZoomChanged() { }
}

internal sealed class FakeState : IInteractionState
{
    public void Enter(IInteractionContext ctx) { }
    public void Exit(IInteractionContext ctx) { }
    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
}

internal sealed class IdleStateWithTransition : IInteractionState
{
    private readonly StateTransition _t;
    public IdleStateWithTransition(StateTransition t) { _t = t; }
    public void Enter(IInteractionContext ctx) { }
    public void Exit(IInteractionContext ctx) { }
    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx) => _t;
    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
}
