using System.Text;

namespace Gallop.Live.Cutt
{
    //FNV-1a 32 位哈希计算工具
    public static class FNVHash
    {
        private static uint FNV_OFFSET_BASIS_32 = 2166136261u;
        private static uint FNV_PRIME_32 = 16777619u;

        public static int Generate(string seed)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(seed);

            unchecked
            {
                uint hash = FNV_OFFSET_BASIS_32;

                for (int i = 0; i < bytes.Length; i++)
                {
                    hash = (FNV_PRIME_32 * hash) ^ bytes[i];
                }

                return (int)hash;
            }
        }
    }
}