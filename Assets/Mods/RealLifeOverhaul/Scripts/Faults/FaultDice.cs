using System;
using System.Security.Cryptography;

namespace RealLifeOverhaul.Faults
{
    /// <summary>
    /// The city's faults' real source of chance: a System.Random started from the operating system's cryptographic
    /// source and the machine's clock when the faults start, never from anything of the save, the day or a district. So
    /// what breaks, where and when is different in every game, even from the same save loaded twice. Only the city's
    /// runner rolls (the player alone, or the host of a Going Public session, who sends the faults to the guests:
    /// FaultShare); a guest never does. Game-free (EconomyCheck checks it is not started from the save). Cost: a number
    /// when asked.
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
