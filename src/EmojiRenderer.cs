using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace PowerPlanSwitcher
{
    // A small Direct2D / DirectWrite bridge, hosted inside the existing WinForms paint path.
    // GDI TextRenderer cannot render the color layers of Segoe UI Emoji.
    internal sealed class EmojiRenderer : IDisposable
    {
        private IntPtr factory, writeFactory, target, format, brush;
        private float fontSize;
        private bool unavailable;

        internal bool TryDraw(Graphics graphics, Rectangle bounds, string text, Color foreground, bool color)
        {
            if (unavailable || bounds.Width <= 0 || bounds.Height <= 0) return false;
            IntPtr dc = IntPtr.Zero;
            try
            {
                EnsureResources(Math.Min(bounds.Width, bounds.Height) * .68f);
                ColorF ink = new ColorF(foreground);
                Method<SetBrushColor>(brush, 8)(brush, ref ink);
                dc = graphics.GetHdc();
                var rectangle = new NativeRect { Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom };
                Check(Method<BindDc>(target, 57)(target, dc, ref rectangle));
                Method<BeginDraw>(target, 48)(target);
                var layout = new RectF { Left = 0, Top = 0, Right = bounds.Width, Bottom = bounds.Height };
                // CLIP | ENABLE_COLOR_FONT. High contrast uses a monochrome glyph instead.
                Method<DrawText>(target, 27)(target, text, (uint)text.Length, format, ref layout, brush, color ? 6u : 2u, 0);
                Check(Method<EndDraw>(target, 49)(target, IntPtr.Zero, IntPtr.Zero));
                return true;
            }
            catch (ExternalException) { Dispose(); unavailable = true; return false; }
            catch (DllNotFoundException) { Dispose(); unavailable = true; return false; }
            catch (EntryPointNotFoundException) { Dispose(); unavailable = true; return false; }
            finally { if (dc != IntPtr.Zero) graphics.ReleaseHdc(dc); }
        }

        private void EnsureResources(float size)
        {
            if (factory == IntPtr.Zero)
            {
                Guid factoryId = new Guid("06152247-6f50-465a-9245-118bfd3b6007");
                Check(D2D1CreateFactory(0, ref factoryId, IntPtr.Zero, out factory));
                Guid writeId = new Guid("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");
                Check(DWriteCreateFactory(0, ref writeId, out writeFactory));
                // BGRA8, opaque DC. Software rendering also works over Remote Desktop.
                var properties = new TargetProperties { Type = 1, Format = 87, AlphaMode = 3, DpiX = 96, DpiY = 96 };
                Check(Method<CreateDcTarget>(factory, 16)(factory, ref properties, out target));
                var ink = new ColorF(Color.White);
                Check(Method<CreateBrush>(target, 8)(target, ref ink, IntPtr.Zero, out brush));
            }
            if (format == IntPtr.Zero || Math.Abs(fontSize - size) > .1f)
            {
                Release(ref format); fontSize = size;
                Check(Method<CreateFormat>(writeFactory, 15)(writeFactory, "Segoe UI Emoji", IntPtr.Zero, 400, 0, 5, size, "en-us", out format));
                Check(Method<SetAlignment>(format, 3)(format, 2));
                Check(Method<SetAlignment>(format, 4)(format, 2));
                Check(Method<SetAlignment>(format, 5)(format, 1));
            }
        }
        private static T Method<T>(IntPtr instance, int slot) where T : class
        {
            IntPtr table = Marshal.ReadIntPtr(instance);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(table, slot * IntPtr.Size), typeof(T));
        }
        private static void Check(int result) { if (result < 0) Marshal.ThrowExceptionForHR(result); }
        private static void Release(ref IntPtr resource)
        { if (resource != IntPtr.Zero) { Marshal.Release(resource); resource = IntPtr.Zero; } }
        public void Dispose()
        { Release(ref brush); Release(ref format); Release(ref target); Release(ref writeFactory); Release(ref factory); }

        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct RectF { internal float Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct TargetProperties
        { internal int Type, Format, AlphaMode; internal float DpiX, DpiY; internal int Usage, MinimumLevel; }
        [StructLayout(LayoutKind.Sequential)] private struct ColorF
        {
            internal float R, G, B, A;
            internal ColorF(Color c) { R = c.R / 255f; G = c.G / 255f; B = c.B / 255f; A = 1; }
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateDcTarget(IntPtr self, ref TargetProperties properties, out IntPtr target);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateBrush(IntPtr self, ref ColorF color, IntPtr properties, out IntPtr brush);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void SetBrushColor(IntPtr self, ref ColorF color);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate int CreateFormat(IntPtr self, string family, IntPtr collection, uint weight, uint style, uint stretch, float size, string locale, out IntPtr format);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetAlignment(IntPtr self, uint value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int BindDc(IntPtr self, IntPtr dc, ref NativeRect rectangle);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void BeginDraw(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EndDraw(IntPtr self, IntPtr tag1, IntPtr tag2);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate void DrawText(IntPtr self, string text, uint length, IntPtr format, ref RectF bounds, IntPtr brush, uint options, uint measuringMode);
        [DllImport("d2d1.dll", ExactSpelling = true)]
        private static extern int D2D1CreateFactory(uint type, ref Guid id, IntPtr options, out IntPtr factory);
        [DllImport("dwrite.dll", ExactSpelling = true)]
        private static extern int DWriteCreateFactory(uint type, ref Guid id, out IntPtr factory);
    }
}
