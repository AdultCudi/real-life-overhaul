using System;
using System.Security.Cryptography;

namespace RealLifeOverhaul.Faults
{
    /// <summary>
    /// The faults' real source of chance, seeded from the system's cryptographic source and the clock, never the save:
    /// what breaks differs in every game. Only the city's runner rolls; a Going Public guest takes the host's faults.
    /// </summary>
    public sealed class FaultDice : IFaultDice
    {
        private readonly Random _random;

        public FaultDice()
        {
            var bytes = new byte[4];
            using (RandomNumberGenerator source = RandomNumberGenerator.Create())
            {
                source.GetBytes(bytes);
            }
            _random = new Random(BitConverter.ToInt32(bytes, 0) ^ Environment.TickCount);
        }

        public int Next(int most)
        {
            return most <= 1 ? 0 : _random.Next(most);
        }
    }
}
