using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NEXA.Hand;
using OpenCvSharp;

namespace NEXA;

public class DwellClickState
{
    public bool IsHovering { get; set; } = false;
    public double HoverProgress { get; set; } = 0.0;
    public double DwellRadiusPx { get; set; } = 28.0; // Radius auf dem Bildschirm (in Pixeln)
    public double RequiredDwellSeconds { get; set; } = 0.85; // 0.85 Sekunden Verweilzeit
    public Point2f AnchorScreenPos { get; set; }
    public Stopwatch DwellTimer { get; } = new();
    public DateTime LastClickTime { get; set; } = DateTime.MinValue;
    public bool InCooldown => (DateTime.Now - LastClickTime).TotalMilliseconds < 500;
}

public class MouseController
{
    #region Win32 API Imports & Structs

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    private const uint INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

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

    public DwellClickState DwellState { get; } = new();
    public bool Enabled { get; set; } = true;
    public int ScreenWidth { get; private set; }
    public int ScreenHeight { get; private set; }

    // Ultra-Smooth Cursor State
    private double _smoothedScreenX = 0;
    private double _smoothedScreenY = 0;
    private bool _hasInitializedPos = false;

    // Visual click effect animation
    public Point2f LastClickPosition { get; private set; }

    public MouseController()
    {
        ScreenWidth = GetSystemMetrics(SM_CXSCREEN);
        ScreenHeight = GetSystemMetrics(SM_CYSCREEN);
        if (ScreenWidth <= 0) ScreenWidth = 1920;
        if (ScreenHeight <= 0) ScreenHeight = 1080;
    }

    public void Update(TrackedHand? hand, int frameWidth, int frameHeight)
    {
        if (!Enabled || hand == null)
        {
            ResetHover();
            return;
        }

        string currentGesture = hand.Gesture;
        var indexTip = hand.SmoothedLandmarks2D[8];

        // Mauszeiger NUR bei Pointing bewegen
        if (currentGesture == "Pointing")
        {
            var (targetX, targetY) = MapToScreen(indexTip.X, indexTip.Y, frameWidth, frameHeight);

            // 1. Ultra-Smooth Filtering mit dynamischer Glättung
            if (!_hasInitializedPos)
            {
                _smoothedScreenX = targetX;
                _smoothedScreenY = targetY;
                _hasInitializedPos = true;
            }
            else
            {
                double dx = targetX - _smoothedScreenX;
                double dy = targetY - _smoothedScreenY;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                // Deadzone: kleine Zitterbewegungen (< 2.5 Pixel) ignorieren
                if (dist > 2.5)
                {
                    // Dynamischer Glättungsfaktor: Bei schnellen Bewegungen direkt folgen, beim Ruhighalten stark stabilisieren
                    double alpha = Math.Clamp(0.18 + (dist / 150.0) * 0.55, 0.18, 0.85);
                    _smoothedScreenX += dx * alpha;
                    _smoothedScreenY += dy * alpha;
                }
            }

            int finalScreenX = (int)Math.Round(_smoothedScreenX);
            int finalScreenY = (int)Math.Round(_smoothedScreenY);

            SetCursorPos(finalScreenX, finalScreenY);

            // 2. Dwell-Click (Verweilklick): Wenn der Zeiger an einer Stelle verweilt
            UpdateDwellClick(finalScreenX, finalScreenY, indexTip);
        }
        else
        {
            ResetHover();
        }
    }

    private void UpdateDwellClick(int screenX, int screenY, Point2f indexTip)
    {
        if (DwellState.InCooldown)
        {
            ResetHover();
            return;
        }

        var currentScreenPt = new Point2f(screenX, screenY);

        if (!DwellState.IsHovering)
        {
            // Hover-Messung an aktuellem Punkt starten
            DwellState.IsHovering = true;
            DwellState.AnchorScreenPos = currentScreenPt;
            DwellState.DwellTimer.Restart();
            DwellState.HoverProgress = 0.0;
        }
        else
        {
            double distFromAnchor = Math.Sqrt(
                Math.Pow(currentScreenPt.X - DwellState.AnchorScreenPos.X, 2) +
                Math.Pow(currentScreenPt.Y - DwellState.AnchorScreenPos.Y, 2)
            );

            // Wenn sich die Maus innerhalb des Verweil-Radius bewegt -> Ladezeit hochzählen
            if (distFromAnchor <= DwellState.DwellRadiusPx)
            {
                double elapsed = DwellState.DwellTimer.Elapsed.TotalSeconds;
                DwellState.HoverProgress = Math.Clamp(elapsed / DwellState.RequiredDwellSeconds, 0.0, 1.0);

                if (DwellState.HoverProgress >= 1.0)
                {
                    // Klick auslösen!
                    PerformClick();
                    LastClickPosition = indexTip;
                    DwellState.LastClickTime = DateTime.Now;
                    ResetHover();
                }
            }
            else
            {
                // Hand hat sich zu weit bewegt -> Neuer Ankerpunkt
                DwellState.AnchorScreenPos = currentScreenPt;
                DwellState.DwellTimer.Restart();
                DwellState.HoverProgress = 0.0;
            }
        }
    }

    private void ResetHover()
    {
        DwellState.IsHovering = false;
        DwellState.HoverProgress = 0.0;
        DwellState.DwellTimer.Reset();
    }

    private static void PerformClick()
    {
        var inputs = new INPUT[]
        {
            new() { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } },
            new() { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP } }
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    private (double screenX, double screenY) MapToScreen(float x, float y, int frameWidth, int frameHeight)
    {
        // 15% Randbereich als Puffer für leicht erreichbare Ecken
        float marginX = frameWidth * 0.15f;
        float marginY = frameHeight * 0.15f;

        float normX = Math.Clamp((x - marginX) / (frameWidth - 2 * marginX), 0.0f, 1.0f);
        float normY = Math.Clamp((y - marginY) / (frameHeight - 2 * marginY), 0.0f, 1.0f);

        double screenX = normX * ScreenWidth;
        double screenY = normY * ScreenHeight;

        return (screenX, screenY);
    }

    public void RenderFeedback(Mat frame, TrackedHand? hand)
    {
        if (hand == null) return;

        var indexTip = hand.SmoothedLandmarks2D[8];
        var pt = new Point((int)Math.Round(indexTip.X), (int)Math.Round(indexTip.Y));

        // 1. Dwell-Click Radial Charge Ring
        if (DwellState.IsHovering && DwellState.HoverProgress > 0.05)
        {
            int radius = 22;
            int angle = (int)(DwellState.HoverProgress * 360);

            // Hintergrund-Ring
            Cv2.Circle(frame, pt, radius, new Scalar(60, 60, 80), 2, LineTypes.AntiAlias);

            // Auflade-Bogen (von Türkis zu leuchtendem Gelb/Grün)
            Scalar arcColor = DwellState.HoverProgress > 0.7
                ? new Scalar(0, 255, 120) // Grün kurz vor Klick
                : new Scalar(0, 220, 255); // Cyan beim Laden

            Cv2.Ellipse(frame, pt, new Size(radius, radius), -90, 0, angle, arcColor, 3, LineTypes.AntiAlias);
            Cv2.Circle(frame, pt, 4, arcColor, -1, LineTypes.AntiAlias);

            string pct = $"{(int)(DwellState.HoverProgress * 100)}%";
            Cv2.PutText(frame, pct, new Point(pt.X + 26, pt.Y + 5),
                HersheyFonts.HersheySimplex, 0.40, arcColor, 1, LineTypes.AntiAlias);
        }

        // 2. Klick-Animation (Ripple Flash bei Auslösung)
        double elapsedSinceClick = (DateTime.Now - DwellState.LastClickTime).TotalMilliseconds;
        if (elapsedSinceClick < 400)
        {
            float rippleProgress = (float)(elapsedSinceClick / 400.0);
            int rippleRadius = (int)(12 + rippleProgress * 40);
            var clickPt = new Point((int)Math.Round(LastClickPosition.X), (int)Math.Round(LastClickPosition.Y));

            Cv2.Circle(frame, clickPt, rippleRadius, new Scalar(0, 255, 255), 2, LineTypes.AntiAlias);
            Cv2.PutText(frame, "* CLICK *", new Point(clickPt.X - 28, clickPt.Y - rippleRadius - 6),
                HersheyFonts.HersheySimplex, 0.52, new Scalar(0, 255, 255), 1, LineTypes.AntiAlias);
        }
    }
}
