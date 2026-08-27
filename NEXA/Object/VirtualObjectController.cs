using NEXA.Domain.Grab;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA.Object;

/// <summary>
/// Unified application controller orchestrating 2D virtual test object manipulation, relative grab translation, and continuous pinch-to-zoom scaling.
/// <para>
/// <b>What it is:</b> An augmented-reality spatial interaction coordinator managing dragging, zooming, and viewport rendering.
/// </para>
/// </summary>
public class VirtualObjectController
{
    private readonly VirtualObjectGrabEngine _grabEngine;
    private readonly WindowResizeDetector _resizeDetector;
    private readonly VirtualObjectRenderer _renderer;

    /// <summary>
    /// The target virtual object storing current 2D center coordinates and base dimensions.
    /// </summary>
    public TestObject TargetObject { get; } = new();

    /// <summary>
    /// The window resize detector used for stepless continuous scaling.
    /// </summary>
    public WindowResizeDetector ResizeDetector => _resizeDetector;

    /// <summary>
    /// Gets the current scale multiplier of the virtual test object.
    /// </summary>
    public double CurrentScale => _resizeDetector.State.IsActive && _resizeDetector.State.CurrentScale > 0 ? _resizeDetector.State.CurrentScale : 1.0;

    /// <summary>
    /// The grab state machine tracking fist hold timers and spatial offset locks.
    /// </summary>
    public GrabState GrabState => _grabEngine.State;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualObjectController"/> class.
    /// </summary>
    /// <param name="grabEngine">Optional custom grab engine.</param>
    /// <param name="resizeDetector">Optional custom resize detector.</param>
    /// <param name="renderer">Optional custom renderer.</param>
    public VirtualObjectController(
        VirtualObjectGrabEngine? grabEngine = null,
        WindowResizeDetector? resizeDetector = null,
        VirtualObjectRenderer? renderer = null)
    {
        _grabEngine = grabEngine ?? new VirtualObjectGrabEngine();
        _resizeDetector = resizeDetector ?? new WindowResizeDetector();
        _renderer = renderer ?? new VirtualObjectRenderer();
    }

    /// <summary>
    /// Updates object position and zoom scaling based on the active hand gesture.
    /// </summary>
    /// <param name="hand">The tracked hand instance.</param>
    /// <param name="frameWidth">Camera frame width in pixels.</param>
    /// <param name="frameHeight">Camera frame height in pixels.</param>
    public void Update(TrackedHand? hand, int frameWidth, int frameHeight)
    {
        if (hand == null)
        {
            _grabEngine.HandleNoHand();
            _resizeDetector.Reset();
            return;
        }

        Point2f[] lm = hand.SmoothedLandmarks2D;
        Point2f palmCenter = lm[9];
        string currentGesture = hand.Gesture;

        // 1. Evaluate Grab interaction (clenched fist with required hold duration)
        _grabEngine.UpdateGrab(TargetObject, palmCenter, currentGesture, frameWidth, frameHeight);

        // 2. Evaluate Zoom interaction via shared WindowResizeDetector
        if (!_grabEngine.State.Active)
        {
            _resizeDetector.Update(
                hand,
                TargetObject.BaseWidth,
                TargetObject.BaseHeight,
                frameWidth,
                frameHeight);
        }
        else
        {
            _resizeDetector.Reset();
        }
    }

    /// <summary>
    /// Resets the object to its default initial spawn position and 1.0x scale factor.
    /// </summary>
    /// <param name="frameWidth">Camera frame width.</param>
    /// <param name="frameHeight">Camera frame height.</param>
    public void Reset(int frameWidth = 1280, int frameHeight = 720)
    {
        TargetObject.X = frameWidth - 250;
        TargetObject.Y = frameHeight - 200;
        _grabEngine.Reset();
        _resizeDetector.Reset();
    }

    /// <summary>
    /// Renders the virtual test object window with alpha blending, corner accents, and telemetry text onto the frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    public void Render(Mat frame)
    {
        _renderer.Render(frame, TargetObject, GrabState, _resizeDetector.State);
    }
}
