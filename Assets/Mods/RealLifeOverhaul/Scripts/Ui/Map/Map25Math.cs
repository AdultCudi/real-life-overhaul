using System;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// The arithmetic the 2.5D map stands on, game-free so EconomyCheck runs it and the preview draws with it: matrices as the
    /// engine keeps them (sixteen floats, column by column, element (row, column) at row + 4 * column), the view and the
    /// frustum, a place on the screen, half floats for the vertex colours, a seeded generator. Cost a call: a few dozen
    /// multiplications. Nothing here allocates.
    /// </summary>
    public static class Map25Math
    {
        public const float Deg = 0.017453292f;

        /// <summary>result = a * b (b is applied first). The result may not be a or b.</summary>
        public static void Multiply(float[] a, float[] b, float[] result)
        {
            for (int c = 0; c < 4; c++)
            {
                for (int r = 0; r < 4; r++)
                {
                    float sum = 0f;
                    for (int k = 0; k < 4; k++)
                    {
                        sum += a[r + 4 * k] * b[k + 4 * c];
                    }
                    result[r + 4 * c] = sum;
                }
            }
        }

        /// <summary>The camera's matrix as <c>Camera.worldToCameraMatrix</c> has it: the camera looks down its own minus z.</summary>
        public static void View(float[] m, float ex, float ey, float ez, float fx, float fy, float fz)
        {
            // right = up x forward in the engine's left handed world
            float rx = fz, ry = 0f, rz = -fx;
            float rl = (float)Math.Sqrt(rx * rx + rz * rz);
            if (rl < 1e-6f)
            {
                rx = 1f;
                rz = 0f;
            }
            else
            {
                rx /= rl;
                rz /= rl;
            }
            float ux = fy * rz - fz * ry, uy = fz * rx - fx * rz, uz = fx * ry - fy * rx;
            m[0] = rx; m[4] = ry; m[8] = rz; m[12] = -(rx * ex + ry * ey + rz * ez);
            m[1] = ux; m[5] = uy; m[9] = uz; m[13] = -(ux * ex + uy * ey + uz * ez);
            m[2] = -fx; m[6] = -fy; m[10] = -fz; m[14] = fx * ex + fy * ey + fz * ez;
            m[3] = 0f; m[7] = 0f; m[11] = 0f; m[15] = 1f;
        }

        /// <summary>The engine's own <c>Matrix4x4.Perspective</c>: the field of view is the vertical one, in degrees.</summary>
        public static void Perspective(float[] m, float fovDegrees, float aspect, float near, float far)
        {
            float f = 1f / (float)Math.Tan(fovDegrees * Deg * 0.5f);
            for (int i = 0; i < 16; i++)
            {
                m[i] = 0f;
            }
            m[0] = f / aspect;
            m[5] = f;
            m[10] = -(far + near) / (far - near);
            m[11] = -1f;
            m[14] = -2f * far * near / (far - near);
        }

        /// <summary>The six planes of a frustum from its view projection, each (a, b, c, d) with its normal pointing in.</summary>
        public static void Planes(float[] viewProjection, float[] planes)
        {
            for (int p = 0; p < 6; p++)
            {
                int axis = p >> 1;
                float sign = (p & 1) == 0 ? 1f : -1f;
                for (int k = 0; k < 4; k++)
                {
                    planes[p * 4 + k] = viewProjection[3 + 4 * k] + sign * viewProjection[axis + 4 * k];
                }
                float length = (float)Math.Sqrt(planes[p * 4] * planes[p * 4] + planes[p * 4 + 1] * planes[p * 4 + 1] + planes[p * 4 + 2] * planes[p * 4 + 2]);
                if (length > 1e-9f)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        planes[p * 4 + k] /= length;
                    }
                }
            }
        }

        /// <summary>False when the box is wholly outside one of the planes; a box that touches the frustum is in.</summary>
        public static bool BoxIn(float[] planes, float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            for (int p = 0; p < 6; p++)
            {
                float a = planes[p * 4], b = planes[p * 4 + 1], c = planes[p * 4 + 2], d = planes[p * 4 + 3];
                float x = a >= 0f ? maxX : minX, y = b >= 0f ? maxY : minY, z = c >= 0f ? maxZ : minZ;
                if (a * x + b * y + c * z + d < 0f)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// A place in the world as a fraction of the picture (0, 0 at the lower left, as the engine's viewport has it); false for
        /// a place behind the camera.
        /// </summary>
        public static bool Project(float[] viewProjection, float x, float y, float z, out float u, out float v)
        {
            float cx = viewProjection[0] * x + viewProjection[4] * y + viewProjection[8] * z + viewProjection[12];
            float cy = viewProjection[1] * x + viewProjection[5] * y + viewProjection[9] * z + viewProjection[13];
            float cw = viewProjection[3] * x + viewProjection[7] * y + viewProjection[11] * z + viewProjection[15];
            if (cw <= 1e-4f)
            {
                u = 0f;
                v = 0f;
                return false;
            }
            u = cx / cw * 0.5f + 0.5f;
            v = cy / cw * 0.5f + 0.5f;
            return true;
        }

        /// <summary>The nearest half float of a number, as its sixteen bits (the vertex colours are kept in half floats: 8 bit linear bands the darks).</summary>
        public static ushort Half(float value)
        {
            uint bits = (uint)BitConverter.SingleToInt32Bits(value);
            uint sign = (bits >> 16) & 0x8000u;
            int exponent = (int)((bits >> 23) & 0xFFu) - 127 + 15;
            uint mantissa = bits & 0x7FFFFFu;
            if (exponent <= 0)
            {
                if (exponent < -10)
                {
                    return (ushort)sign;
                }
                mantissa |= 0x800000u;
                int shift = 14 - exponent;
                uint small = mantissa >> shift;
                if (((mantissa >> (shift - 1)) & 1u) != 0u)
                {
                    small++;
                }
                return (ushort)(sign | small);
            }
            if (exponent >= 31)
            {
                return (ushort)(sign | 0x7C00u);
            }
            uint half = sign | ((uint)exponent << 10) | (mantissa >> 13);
            if ((mantissa & 0x1000u) != 0u)
            {
                half++;
            }
            return (ushort)half;
        }

        /// <summary>The engine's own conversion of a colour channel from how it is written (sRGB) to light (linear).</summary>
        public static float Linear(float c)
        {
            return c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>The inverse of <see cref="Linear"/>: light as it is written in an sRGB picture, which is what the eye gets of the picture.</summary>
        public static float Encode(float light)
        {
            return light <= 0.0031308f ? light * 12.92f : 1.055f * (float)Math.Pow(light, 1f / 2.4f) - 0.055f;
        }
    }

    /// <summary>A small seeded generator (xorshift on a hashed seed): the same seed, the same numbers, on every machine.</summary>
    public struct Map25Rng
    {
        private uint _s;

        public Map25Rng(uint seed)
        {
            uint z = seed + 0x9E3779B9u;
            z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
            z = (z ^ (z >> 13)) * 0xC2B2AE35u;
            z ^= z >> 16;
            _s = z == 0u ? 0x1234ABCDu : z;
        }

        public uint Next()
        {
            _s ^= _s << 13;
            _s ^= _s >> 17;
            _s ^= _s << 5;
            return _s;
        }

        /// <summary>0 up to but not including 1.</summary>
        public float Float()
        {
            return (Next() & 0xFFFFFFu) / 16777216f;
        }

        public float Range(float low, float high)
        {
            return low + (high - low) * Float();
        }

        /// <summary>0 up to but not including <paramref name="count"/>.</summary>
        public int Int(int count)
        {
            return (int)(Next() % (uint)count);
        }
    }
}
