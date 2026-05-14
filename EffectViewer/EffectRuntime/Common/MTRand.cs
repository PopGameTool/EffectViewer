using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace EffectViewer.EffectRuntime.Common
{
    public struct MTRand
    {
        [InlineArray(MTRAND_N)]
        private struct InlineArrayMT
        {
            private uint _buf;
        }

        private const int MTRAND_M = 397;
        private const int MTRAND_N = 624;
        private const uint MATRIX_A = 0x9908b0dfU;
        private const uint UPPER_MASK = 0x80000000U;
        private const uint LOWER_MASK = 0x7fffffffU;
        private const uint TEMPERING_MASK_B = 0x9d2c5680U;
        private const uint TEMPERING_MASK_C = 0xefc60000U;

        public const int DATA_LENGTH = MTRAND_N * sizeof(uint);

        private static uint TemperingShiftU(uint y) => y >> 11;
        private static uint TemperingShiftS(uint y) => y << 7;
        private static uint TemperingShiftT(uint y) => y << 15;
        private static uint TemperingShiftL(uint y) => y >> 18;

        private static readonly uint[] mag01 = [0, MATRIX_A];

        private InlineArrayMT mt;
        private int mti;

        public MTRand(ReadOnlySpan<byte> theSerialData)
        {
            SRand(theSerialData);
            mti = MTRAND_N + 1;
        }

        public MTRand(uint seed)
        {
            SRand(seed);
        }

        public MTRand()
        {
            SRand(4357U);
        }

        public void SRand(ReadOnlySpan<byte> theSerialData)
        {
            if (theSerialData.Length >= DATA_LENGTH)
            {
                theSerialData[..DATA_LENGTH].CopyTo(MemoryMarshal.Cast<uint, byte>((Span<uint>)mt));
            }
            else
            {
                SRand(4357U);
            }
        }

        public void SRand(uint seed)
        {
            if (seed == 0)
            {
                seed = 4357U;
            }

            /* setting initial seeds to mt[MTRAND_N] using         */
            /* the generator Line 25 of Table 1 in          */
            /* [KNUTH 1981, The Art of Computer Programming */
            /*    Vol. 2 (2nd Ed.), pp102]                  */
            mt[0] = seed & 0xffffffffU;
            for (mti = 1; mti < MTRAND_N; mti++)
            {
                mt[mti] = 1812433253U * (mt[mti - 1] ^ (mt[mti - 1] >> 30)) + (uint)mti;
                /* See Knuth TAOCP Vol2. 3rd Ed. P.106 for multiplier. */
                /* In the previous versions, MSBs of the seed affect   */
                /* only MSBs of the array mt[].                        */
                /* 2002/01/09 modified by Makoto Matsumoto             */
                mt[mti] &= 0xffffffffU;
                /* for >32 bit machines */
            }
        }

        public uint Next()
        {
            uint y;

            /* mag01[x] = x * MATRIX_A  for x=0,1 */

            if (mti >= MTRAND_N)
            { /* generate MTRAND_N words at one time */
                int kk;

                for (kk = 0; kk < MTRAND_N - MTRAND_M; kk++)
                {
                    y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = mt[kk + MTRAND_M] ^ (y >> 1) ^ mag01[y & 0x1UL];
                }
                for (; kk < MTRAND_N - 1; kk++)
                {
                    y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = mt[kk + (MTRAND_M - MTRAND_N)] ^ (y >> 1) ^ mag01[y & 0x1UL];
                }
                y = (mt[MTRAND_N - 1] & UPPER_MASK) | (mt[0] & LOWER_MASK);
                mt[MTRAND_N - 1] = mt[MTRAND_M - 1] ^ (y >> 1) ^ mag01[y & 0x1UL];

                mti = 0;
            }

            y = mt[mti++];
            y ^= TemperingShiftU(y);
            y ^= TemperingShiftS(y) & TEMPERING_MASK_B;
            y ^= TemperingShiftT(y) & TEMPERING_MASK_C;
            y ^= TemperingShiftL(y);

            y &= 0x7FFFFFFF;
            return y;
        }

        public uint Next(uint range)
        {
            return Next() % range;
        }

        public float Next(float range)
        {
            // must be double
            return (float)(Next() * range / (double)0x7FFFFFFF);
        }

        public void Serialize(Span<byte> theSerialData)
        {
            Debug.Assert(theSerialData.Length >= DATA_LENGTH);
            MemoryMarshal.Cast<uint, byte>((Span<uint>)mt).CopyTo(theSerialData[..DATA_LENGTH]);
        }
    }
}