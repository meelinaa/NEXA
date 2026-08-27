using NEXA.Domain.Undo;
using NEXA.Hand;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="CircleUndoDetector"/> evaluating Peace-sign wrist twist Undo and Redo kinematics.
/// </summary>
public class CircleUndoDetectorTests
{
    [Fact]
    public void PeaceSign_WristTwistLeftAndRight_TriggersUndoAndRedo()
    {
        CircleUndoDetector testUndoDetector = new();

        TrackedHand peaceHand = new() { Gesture = "Peace" };
        peaceHand.SmoothedLandmarks2D[0] = new Point2f(500, 500);
        peaceHand.SmoothedLandmarks2D[4] = new Point2f(470, 430);
        peaceHand.SmoothedLandmarks2D[5] = new Point2f(480, 420);
        peaceHand.SmoothedLandmarks2D[9] = new Point2f(500, 400);
        peaceHand.SmoothedLandmarks2D[13] = new Point2f(520, 420);
        peaceHand.SmoothedLandmarks2D[16] = new Point2f(520, 450);
        peaceHand.SmoothedLandmarks2D[17] = new Point2f(540, 430);
        peaceHand.SmoothedLandmarks2D[20] = new Point2f(540, 460);

        peaceHand.SmoothedLandmarks2D[8] = new Point2f(490, 380);
        peaceHand.SmoothedLandmarks2D[12] = new Point2f(510, 380);
        testUndoDetector.Update(peaceHand);

        // Twist Left (Undo)
        peaceHand.SmoothedLandmarks2D[8] = new Point2f(370, 380);
        peaceHand.SmoothedLandmarks2D[12] = new Point2f(390, 380);
        CircleUndoAction undoAction = testUndoDetector.Update(peaceHand);
        Assert.Equal(CircleUndoAction.Undo, undoAction);

        // Twist Right (Redo)
        testUndoDetector.State.CooldownTimer.Reset();
        testUndoDetector.Update(peaceHand);
        peaceHand.SmoothedLandmarks2D[8] = new Point2f(610, 380);
        peaceHand.SmoothedLandmarks2D[12] = new Point2f(630, 380);
        CircleUndoAction redoAction = testUndoDetector.Update(peaceHand);
        Assert.Equal(CircleUndoAction.Redo, redoAction);
    }
}
