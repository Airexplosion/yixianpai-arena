namespace YxArena
{
	public static class ParamStream
	{
		public static int[] Generate(uint seed, int count)
		{
			if (count <= 0)
			{
				return new int[0];
			}
			uint num = ((seed == 0) ? 1u : seed);
			int[] array = new int[count];
			for (int i = 0; i < count; i++)
			{
				num ^= num << 13;
				num ^= num >> 17;
				num ^= num << 5;
				array[i] = (int)(num % 100);
			}
			return array;
		}

		public static uint MixSeed(int frameCount, int battleIndex)
		{
			uint num = (uint)(frameCount * -1640531535 + battleIndex * 40503 + 1);
			if (num != 0)
			{
				return num;
			}
			return 1u;
		}
	}
}
