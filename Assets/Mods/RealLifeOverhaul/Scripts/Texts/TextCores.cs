using System;
using System.Runtime.InteropServices;

namespace RealLifeOverhaul.Texts
{
    /// <summary>The machine's real cores, or the logical processors halved from four where Windows cannot say. Asked once, off the game's thread.</summary>
    public static class TextCores
    {
        private const int RelationProcessorCore = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessorInfo
        {
            public UIntPtr Mask;
            public int Relationship;
            public long Union0;
            public long Union1;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformation(IntPtr buffer, ref uint length);

        public static int Real()
        {
            try
            {
                uint length = 0;
                GetLogicalProcessorInformation(IntPtr.Zero, ref length);
                if (length > 0)
                {
                    IntPtr buffer = Marshal.AllocHGlobal((int)length);
                    try
                    {
                        if (GetLogicalProcessorInformation(buffer, ref length))
                        {
                            int size = Marshal.SizeOf(typeof(ProcessorInfo)), cores = 0;
                            for (int at = 0; at + size <= length; at += size)
                            {
                                var info = (ProcessorInfo)Marshal.PtrToStructure(new IntPtr(buffer.ToInt64() + at), typeof(ProcessorInfo));
                                cores += info.Relationship == RelationProcessorCore ? 1 : 0;
                            }
                            if (cores > 0)
                            {
                                return cores;
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
            catch (Exception)
            {
                // not Windows, or the call is missing: the rule below
            }
            int logical = Environment.ProcessorCount;
            return TextSpeed.RealCores(logical, logical >= 4);
        }
    }
}
