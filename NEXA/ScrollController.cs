using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA;

public class SwipeState
{
    public Queue<(double Y, DateTime Time)> History { get; } = new();
    public DateTime LastSwipeTime { get; set; } = DateTime.MinValue;
    public bool WaitingForRest { get; set; } = false;
    public double LastSpeed { get; set; } = 0.0;
    public double LastDeltaY { get; set; } = 0.0;
    public double LastSlope { get; set; } = 0.0; // Berechnete Steigung der linearen Regression (px/ms)

    // Physikalisches Momentum (Trägheit / Ausrollen)
    public double MomentumVelocity { get; set; } = 0.0;
    public double AccumulatedDelta { get; set; } = 0.0;
    public DateTime LastMomentumUpdate { get; set; } = DateTime.MinValue;

    public static readonly TimeSpan WindowSize = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan Cooldown = TimeSpan.FromMilliseconds(350);
    public const double MinDistance = 25.0;        // Mindeststrecke in Pixeln
    public const double MinSpeed = 0.16;           // Mindest-Steigung (px/ms)
    public const double RestSpeedThreshold = 0.04;  // Ruhephasen-Schwellenwert (px/ms)

    public const double MomentumDecay = 0.91;       // Reibung / Geschwindigkeitsabbau pro 16ms
    public const double MinMomentumVelocity = 3.0;  // Stoppgrenze
}

public class ScrollController
{
    #region Win32 API Imports

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private const uint INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    public const int WHEEL_DELTA = 120;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    #endregion

    public SwipeState State { get; } = new();
    public bool Enabled { get; set; } = true;
    public DateTime LastPointerActiveTime { get; set; } = DateTime.MinValue;
    public static readonly TimeSpan ScrollWindowDuration = TimeSpan.FromSeconds(3.0); // 3 Sekunden Aktivierungsfenster

    // Gesten-Debounce Toleranz gegen Motion-Blur Flackern
    private int _invalidGestureFrameCount = 0;
    private const int InvalidGestureTolerance = 3;

    // Visual feedback & Debug
    public string LastSwipeDirection { get; private set; } = "";
    public DateTime LastFeedbackTime { get; private set; } = DateTime.MinValue;
    public Point2f LastSwipePoint { get; private set; }
    public double LastInitialVelocity { get; private set; } = 0.0;

    public bool IsWindowActive
    {
        get
        {
            double secSincePointer = (DateTime.Now - LastPointerActiveTime).TotalSeconds;
            double secSinceSwipe = (DateTime.Now - State.LastSwipeTime).TotalSeconds;
            return secSincePointer <= ScrollWindowDuration.TotalSeconds || secSinceSwipe <= ScrollWindowDuration.TotalSeconds;
        }
    }

    public double RemainingWindowSeconds
    {
        get
        {
            double secSincePointer = (DateTime.Now - LastPointerActiveTime).TotalSeconds;
            double secSinceSwipe = (DateTime.Now - State.LastSwipeTime).TotalSeconds;
            double minElapsed = Math.Min(secSincePointer, secSinceSwipe);
            return Math.Max(0.0, ScrollWindowDuration.TotalSeconds - minElapsed);
        }
    }

    /// <summary>
    /// Berechnet die Steigung (Slope in px/ms) über alle Punkte im Zeitfenster per linearer Regression (Least Squares).
    /// </summary>
    private static double CalculateTrendSlope(Queue<(double Y, DateTime Time)> history, DateTime referenceTime)
    {
        int n = history.Count;
        if (n < 2) return 0.0;

        double sumT = 0.0;
        double sumY = 0.0;
        double sumTY = 0.0;
        double sumTT = 0.0;

        foreach (var item in history)
        {
            double t = (item.Time - referenceTime).TotalMilliseconds;
            double y = item.Y;

            sumT += t;
            sumY += y;
            sumTY += t * y;
            sumTT += t * t;
        }

        double denominator = (n * sumTT) - (sumT * sumT);
        if (Math.Abs(denominator) < 1e-6) return 0.0;

        double slope = ((n * sumTY) - (sumT * sumY)) / denominator;
        return slope;
    }

    /// <summary>
    /// Wird jeden Frame aufgerufen, um das Momentum-Ausrollen kontinuierlich zu verarbeiten.
    /// </summary>
    public void UpdateMomentum()
    {
        if (!Enabled || Math.Abs(State.MomentumVelocity) < SwipeState.MinMomentumVelocity)
        {
            State.MomentumVelocity = 0.0;
            State.AccumulatedDelta = 0.0;
            return;
        }

        var now = DateTime.Now;
        double dt = State.LastMomentumUpdate == DateTime.MinValue
            ? 16.0
            : (now - State.LastMomentumUpdate).TotalMilliseconds;
        State.LastMomentumUpdate = now;

        // Zeitkorrigiertes Delta akkumulieren
        double normalizedTicks = Math.Clamp(dt / 16.0, 0.1, 4.0);
        State.AccumulatedDelta += State.MomentumVelocity * normalizedTicks;

        // Ganze WHEEL_DELTA-Einheiten senden
        int wholeUnits = (int)(State.AccumulatedDelta / WHEEL_DELTA) * WHEEL_DELTA;
        if (wholeUnits != 0)
        {
            ScrollWheel(wholeUnits);
            State.AccumulatedDelta -= wholeUnits;
        }

        // Geschwindigkeitsabbau (Trägheit / Reibung)
        State.MomentumVelocity *= Math.Pow(SwipeState.MomentumDecay, normalizedTicks);
    }

    public void Update(TrackedHand? hand)
    {
        if (!Enabled || hand == null)
        {
            _invalidGestureFrameCount++;
            if (_invalidGestureFrameCount >= InvalidGestureTolerance)
            {
                State.History.Clear();
                State.WaitingForRest = false;
            }
            return;
        }

        // 3-Sekunden-Aktivierungsfenster prüfen
        if (!IsWindowActive)
        {
            State.History.Clear();
            State.WaitingForRest = false;
            _invalidGestureFrameCount = 0;
            return;
        }

        string currentGesture = hand.Gesture;
        bool isValidGesture = currentGesture == "Hand Up" || currentGesture == "Hand Down" || currentGesture == "Open Palm" || currentGesture == "Tracking";

        // 3. Debounce für Gesten-Aussetzer (Motion Blur bei schnellen Wischen)
        if (!isValidGesture)
        {
            _invalidGestureFrameCount++;
            if (_invalidGestureFrameCount >= InvalidGestureTolerance)
            {
                State.History.Clear();
                State.WaitingForRest = false;
            }
            return; // History für bis zu 2 Frames beibehalten, aber keinen neuen Punkt hinzufügen
        }

        _invalidGestureFrameCount = 0;

        var palmCenter = hand.SmoothedLandmarks2D[9];
        var now = DateTime.Now;

        State.History.Enqueue((palmCenter.Y, now));

        // Alte Einträge außerhalb des 250ms-Fensters entfernen
        while (State.History.Count > 0 && now - State.History.Peek().Time > SwipeState.WindowSize)
        {
            State.History.Dequeue();
        }

        // 2. Mindest-Sample-Anzahl (mindestens 4 Samples für stabile Regression)
        if (State.History.Count < 4) return;

        // 1. Lineare Regression über alle Punkte im Zeitfenster
        double slope = CalculateTrendSlope(State.History, now);
        double speed = Math.Abs(slope);
        State.LastSlope = slope;
        State.LastSpeed = speed;

        // Gesamtverschiebung im Fenster für die Distanz-Bedingung
        var oldest = State.History.Peek();
        double totalDisplacement = palmCenter.Y - oldest.Y;
        State.LastDeltaY = totalDisplacement;

        // 1. Nach einem Wisch: Prüfen, ob Hand sich beruhigt hat (Ruhephase via Speed/Slope)
        if (State.WaitingForRest)
        {
            bool cooldownPassed = now - State.LastSwipeTime >= SwipeState.Cooldown;
            if (cooldownPassed && speed < SwipeState.RestSpeedThreshold)
            {
                State.WaitingForRest = false;
            }
            return;
        }

        // 2. Wisch-Erkennung: Mindestdistanz + Mindestgeschwindigkeit (Slope)
        if (Math.Abs(totalDisplacement) > SwipeState.MinDistance && speed > SwipeState.MinSpeed)
        {
            // Konsistenzprüfung: Regression-Slope und Gesamtverschiebung müssen in dieselbe Richtung zeigen
            bool slopeSaysUp = slope < 0;
            bool displacementSaysUp = totalDisplacement < 0;
            if (slopeSaysUp != displacementSaysUp)
            {
                return;
            }
            bool isMovingUp = slopeSaysUp; // true = Hand nach oben -> Scroll DOWN

            double speedNormalized = Math.Clamp((speed - SwipeState.MinSpeed) / 0.50, 0.0, 1.0);
            double initialVelocity = 45.0 + speedNormalized * 115.0;
            LastInitialVelocity = initialVelocity;

            double newVelocity = isMovingUp ? -initialVelocity : initialVelocity;

            if (Math.Sign(newVelocity) == Math.Sign(State.MomentumVelocity))
            {
                State.MomentumVelocity = Math.Clamp(State.MomentumVelocity + newVelocity * 0.75, -240.0, 240.0);
            }
            else
            {
                State.MomentumVelocity = newVelocity;
            }

            LastSwipeDirection = isMovingUp ? "DOWN" : "UP";
            LastSwipePoint = palmCenter;
            LastFeedbackTime = now;
            State.LastSwipeTime = now;
            State.LastMomentumUpdate = now;
            State.History.Clear();
            State.WaitingForRest = true;
        }
    }

    private static void ScrollWheel(int scrollAmount)
    {
        var inputs = new INPUT[]
        {
            new()
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dwFlags = MOUSEEVENTF_WHEEL,
                    mouseData = unchecked((uint)scrollAmount)
                }
            }
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    public void RenderFeedback(Mat frame)
    {
        // 1. Wisch-Pfeil Animation mit Geschwindigkeits- und Slope-Anzeige
        double elapsed = (DateTime.Now - LastFeedbackTime).TotalMilliseconds;
        if (elapsed < 550)
        {
            float progress = (float)(elapsed / 550.0);

            int x = (int)LastSwipePoint.X;
            int y = (int)LastSwipePoint.Y;

            bool isUp = LastSwipeDirection == "UP";
            var color = LastInitialVelocity >= 100
                ? new Scalar(0, 100, 255)  // Starker Schwung: Orange/Rot
                : new Scalar(0, 240, 255); // Normaler Schwung: Cyan/Gelb

            int offset = (int)(progress * 40);
            int drawY = isUp ? y - offset : y + offset;

            string arrowText = isUp
                ? $"^ SCROLL UP (Slope: {State.LastSlope:+0.00;-0.00;0.00})"
                : $"v SCROLL DOWN (Slope: {State.LastSlope:+0.00;-0.00;0.00})";

            Cv2.PutText(frame, arrowText, new Point(Math.Max(10, x - 85), Math.Max(30, drawY)),
                HersheyFonts.HersheySimplex, 0.52, color, 2, LineTypes.AntiAlias);
        }
    }
}
