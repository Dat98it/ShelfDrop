using System;
using System.Runtime.InteropServices;
using ShelfDrop.App.Native;
using ShelfDrop.Core;
using Xunit;

namespace ShelfDrop.App.Tests
{
    /// <summary>
    /// Tests that drive the real mouse with injected input, which is the nearest thing to a person shaking a drag. They only run in
    /// the automated build (SHELFDROP_INTERACTIVE_TESTS=1), where nobody is using the machine.
    /// </summary>
    [Collection("wpf")]
    public class InputSimulationTests
    {
        private const uint InputMouse = 0;
        private const uint MoveAbsolute = 0x0001 | 0x8000;
        private const uint LeftDown = 0x0002;
        private const uint LeftUp = 0x0004;

        private readonly WpfFixture _wpf;

        public InputSimulationTests(WpfFixture wpf) => _wpf = wpf;

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint type;
            public MouseInput mouse;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, Input[] inputs, int size);

        private static void Send(uint flags, int x = 0, int y = 0)
        {
            // Absolute coordinates run from 0 to 65535 across the primary screen.
            int width = NativeMethods.GetSystemMetrics(0);
            int height = NativeMethods.GetSystemMetrics(1);
            var input = new Input
            {
                type = InputMouse,
                mouse = new MouseInput
                {
                    dx = (int)((long)x * 65535 / Math.Max(1, width - 1)),
                    dy = (int)((long)y * 65535 / Math.Max(1, height - 1)),
                    dwFlags = flags,
                },
            };
            uint sent = SendInput(1, new[] { input }, Marshal.SizeOf<Input>());
            Assert.Equal(1u, sent);
        }

        [InteractiveFact]
        public void TheHookSeesAnInjectedDragAndTheTrackerFindsTheShake() => _wpf.Run(() =>
        {
            var tracker = new DragTracker(new ShakeDetector(), dragThreshold: 8);
            int downs = 0, ups = 0, moves = 0;
            bool began = false, ended = false;
            PixelPoint? shakenAt = null;
            tracker.DragBegan += () => began = true;
            tracker.DragEnded += () => ended = true;
            tracker.Shaken += p => shakenAt = p;

            using (var hook = new MouseHook())
            {
                hook.LeftDown += p => { downs++; tracker.OnLeftDown(p); };
                hook.Moved += (p, t) => { moves++; tracker.OnMove(p, t); };
                hook.LeftUp += () => { ups++; tracker.OnLeftUp(); };
                hook.Install();

                // Somewhere empty near the middle of the screen: press, shake sideways, let go.
                int cx = NativeMethods.GetSystemMetrics(0) / 2;
                int cy = NativeMethods.GetSystemMetrics(1) / 2;
                Send(MoveAbsolute, cx, cy);
                Pump.For(TimeSpan.FromMilliseconds(100));
                Send(LeftDown);
                Pump.For(TimeSpan.FromMilliseconds(100));
                foreach (int dx in new[] { 120, 0, 120, 0, 120, 0, 120, 0 })
                {
                    Send(MoveAbsolute, cx + dx, cy);
                    Pump.For(TimeSpan.FromMilliseconds(50));
                }
                Send(LeftUp);
                Pump.For(TimeSpan.FromMilliseconds(200));
            }

            Assert.Equal(1, downs);
            Assert.Equal(1, ups);
            Assert.True(moves >= 8, "moves seen: " + moves);
            Assert.True(began, "a drag should have begun");
            Assert.NotNull(shakenAt);
            Assert.True(ended, "and ended when the button came up");
        });
    }
}
