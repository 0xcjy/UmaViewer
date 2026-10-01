/* NeuQuant Neural-Net Quantization Algorithm
 * ------------------------------------------
 *
 * Copyright (c) 1994 Anthony Dekker
 *
 * NeuQuant algorithm ported to a Burst-compatible, flat data layout. The
 * learning order and integer arithmetic intentionally match the previous
 * managed implementation so existing GIF output remains stable.
 */

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace uGIF
{
	public sealed class NeuQuant
	{
		private const int NetSize = 256;
		private const int NetworkStride = 4;
		private const int NetworkLength = NetSize * NetworkStride;
		private const int InitRad = NetSize >> 3;

		private readonly Color32[] picture;
		private readonly int lengthCount;
		private readonly int sampleFactor;
		private readonly int pictureWidth;
		private readonly int pictureHeight;
		private readonly bool flipVertical;

		private int[] network;
		private int[] netIndex;

		public NeuQuant(Color32[] thePicture, int length, int sample)
			: this(thePicture, length, sample, 0, 0, false)
		{
		}

		public NeuQuant(
			Color32[] thePicture,
			int length,
			int sample,
			int width,
			int height,
			bool shouldFlipVertical)
		{
			picture = thePicture ?? throw new ArgumentNullException(nameof(thePicture));
			lengthCount = Math.Min(length, thePicture.Length);
			sampleFactor = Math.Max(1, sample);
			pictureWidth = width;
			pictureHeight = height;
			flipVertical = shouldFlipVertical &&
				width > 0 && height > 0 && width * height == lengthCount;
		}

		public byte[] Process()
		{
			if (lengthCount <= 0)
				return new byte[NetSize * 3];

			network = new int[NetworkLength];
			netIndex = new int[NetSize];
			var colorMap = new byte[NetSize * 3];

			using (var nativePicture = new NativeArray<Color32>(picture, Allocator.TempJob))
			using (var nativeNetwork = new NativeArray<int>(NetworkLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			using (var nativeNetIndex = new NativeArray<int>(NetSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			using (var nativeBias = new NativeArray<int>(NetSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			using (var nativeFrequency = new NativeArray<int>(NetSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			using (var nativeRadiusPower = new NativeArray<int>(InitRad, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			using (var nativeColorMap = new NativeArray<byte>(colorMap.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			{
				new NeuQuantProcessJob
				{
					Picture = nativePicture,
					Network = nativeNetwork,
					NetIndex = nativeNetIndex,
					Bias = nativeBias,
					Frequency = nativeFrequency,
					RadiusPower = nativeRadiusPower,
					ColorMap = nativeColorMap,
					LengthCount = lengthCount,
					SampleFactor = sampleFactor,
					PictureWidth = pictureWidth,
					PictureHeight = pictureHeight,
					FlipVertical = flipVertical ? 1 : 0
				}.Schedule().Complete();

				nativeNetwork.CopyTo(network);
				nativeNetIndex.CopyTo(netIndex);
				nativeColorMap.CopyTo(colorMap);
			}

			return colorMap;
		}

		public void ProcessAndMap(
			bool useTransparency,
			int transparentIndex,
			out byte[] colorMap,
			out byte[] indexedPixels)
		{
			colorMap = new byte[NetSize * 3];
			indexedPixels = new byte[lengthCount];
			network = new int[NetworkLength];
			netIndex = new int[NetSize];

			if (lengthCount <= 0)
				return;

			using (var nativePicture = new NativeArray<Color32>(picture, Allocator.TempJob))
			using (var nativeNetwork = new NativeArray<int>(NetworkLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			using (var nativeNetIndex = new NativeArray<int>(NetSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			using (var nativeBias = new NativeArray<int>(NetSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			using (var nativeFrequency = new NativeArray<int>(NetSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			using (var nativeRadiusPower = new NativeArray<int>(InitRad, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			using (var nativeColorMap = new NativeArray<byte>(colorMap.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			using (var nativeResult = new NativeArray<byte>(indexedPixels.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			{
				JobHandle processHandle = new NeuQuantProcessJob
				{
					Picture = nativePicture,
					Network = nativeNetwork,
					NetIndex = nativeNetIndex,
					Bias = nativeBias,
					Frequency = nativeFrequency,
					RadiusPower = nativeRadiusPower,
					ColorMap = nativeColorMap,
					LengthCount = lengthCount,
					SampleFactor = sampleFactor,
					PictureWidth = pictureWidth,
					PictureHeight = pictureHeight,
					FlipVertical = flipVertical ? 1 : 0
				}.Schedule();

				JobHandle mapHandle = new MapPixelsJob
				{
					Pixels = nativePicture,
					Network = nativeNetwork,
					NetIndex = nativeNetIndex,
					ColorMap = nativeColorMap,
					Result = nativeResult,
					UseTransparency = useTransparency ? 1 : 0,
					TransparentIndex = transparentIndex,
					Width = pictureWidth,
					Height = pictureHeight,
					FlipVertical = flipVertical ? 1 : 0
				}.Schedule(lengthCount, 256, processHandle);

				mapHandle.Complete();
				nativeNetwork.CopyTo(network);
				nativeNetIndex.CopyTo(netIndex);
				nativeColorMap.CopyTo(colorMap);
				nativeResult.CopyTo(indexedPixels);
			}
		}

		public byte[] MapPixels(
			Color32[] sourcePixels,
			byte[] colorMap,
			bool useTransparency,
			int transparentIndex,
			int width,
			int height,
			bool shouldFlipVertical)
		{
			if (network == null || netIndex == null)
				throw new InvalidOperationException("Process() must be called before MapPixels().");
			if (sourcePixels == null)
				throw new ArgumentNullException(nameof(sourcePixels));
			if (colorMap == null)
				throw new ArgumentNullException(nameof(colorMap));

			var result = new byte[sourcePixels.Length];
			bool validFlip = shouldFlipVertical && width > 0 && height > 0 && width * height == sourcePixels.Length;

			using (var nativePixels = new NativeArray<Color32>(sourcePixels, Allocator.TempJob))
			using (var nativeNetwork = new NativeArray<int>(network, Allocator.TempJob))
			using (var nativeNetIndex = new NativeArray<int>(netIndex, Allocator.TempJob))
			using (var nativeColorMap = new NativeArray<byte>(colorMap, Allocator.TempJob))
			using (var nativeResult = new NativeArray<byte>(result.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			{
				new MapPixelsJob
				{
					Pixels = nativePixels,
					Network = nativeNetwork,
					NetIndex = nativeNetIndex,
					ColorMap = nativeColorMap,
					Result = nativeResult,
					UseTransparency = useTransparency ? 1 : 0,
					TransparentIndex = transparentIndex,
					Width = width,
					Height = height,
					FlipVertical = validFlip ? 1 : 0
				}.Schedule(result.Length, 256).Complete();

				nativeResult.CopyTo(result);
			}

			return result;
		}

		// Preserved for compatibility with callers outside GIFEncoder.
		public int Map(int b, int g, int r)
		{
			if (network == null || netIndex == null)
				throw new InvalidOperationException("Process() must be called before Map().");

			int bestDistance = 1000;
			int best = -1;
			int i = netIndex[g];
			int j = i - 1;

			while (i < NetSize || j >= 0)
			{
				if (i < NetSize)
				{
					int offset = i * NetworkStride;
					int distance = network[offset + 1] - g;
					if (distance >= bestDistance)
						i = NetSize;
					else
					{
						i++;
						if (distance < 0) distance = -distance;
						int delta = network[offset] - b;
						if (delta < 0) delta = -delta;
						distance += delta;
						if (distance < bestDistance)
						{
							delta = network[offset + 2] - r;
							if (delta < 0) delta = -delta;
							distance += delta;
							if (distance < bestDistance)
							{
								bestDistance = distance;
								best = network[offset + 3];
							}
						}
					}
				}

				if (j >= 0)
				{
					int offset = j * NetworkStride;
					int distance = g - network[offset + 1];
					if (distance >= bestDistance)
						j = -1;
					else
					{
						j--;
						if (distance < 0) distance = -distance;
						int delta = network[offset] - b;
						if (delta < 0) delta = -delta;
						distance += delta;
						if (distance < bestDistance)
						{
							delta = network[offset + 2] - r;
							if (delta < 0) delta = -delta;
							distance += delta;
							if (distance < bestDistance)
							{
								bestDistance = distance;
								best = network[offset + 3];
							}
						}
					}
				}
			}

			return best;
		}

		[BurstCompile]
		private struct NeuQuantProcessJob : IJob
		{
			private const int Prime1 = 499;
			private const int Prime2 = 491;
			private const int Prime3 = 487;
			private const int Prime4 = 503;
			private const int MinPictureBytes = 3 * Prime4;
			private const int NetBiasShift = 4;
			private const int NCycles = 100;
			private const int IntBiasShift = 16;
			private const int IntBias = 1 << IntBiasShift;
			private const int GammaShift = 10;
			private const int BetaShift = 10;
			private const int Beta = IntBias >> BetaShift;
			private const int BetaGamma = IntBias << (GammaShift - BetaShift);
			private const int RadiusBiasShift = 6;
			private const int RadiusBias = 1 << RadiusBiasShift;
			private const int InitRadius = InitRad * RadiusBias;
			private const int RadiusDecrease = 30;
			private const int AlphaBiasShift = 10;
			private const int InitialAlpha = 1 << AlphaBiasShift;
			private const int RadBiasShift = 8;
			private const int RadBias = 1 << RadBiasShift;
			private const int AlphaRadBias = 1 << (AlphaBiasShift + RadBiasShift);

			[ReadOnly] public NativeArray<Color32> Picture;
			public NativeArray<int> Network;
			public NativeArray<int> NetIndex;
			public NativeArray<int> Bias;
			public NativeArray<int> Frequency;
			public NativeArray<int> RadiusPower;
			[WriteOnly] public NativeArray<byte> ColorMap;
			public int LengthCount;
			public int SampleFactor;
			public int PictureWidth;
			public int PictureHeight;
			public int FlipVertical;

			public void Execute()
			{
				InitializeNetwork();
				Learn();
				UnbiasNetwork();
				BuildIndex();
				BuildColorMap();
			}

			private void InitializeNetwork()
			{
				for (int i = 0; i < NetSize; i++)
				{
					int offset = i * NetworkStride;
					int value = (i << (NetBiasShift + 8)) / NetSize;
					Network[offset] = value;
					Network[offset + 1] = value;
					Network[offset + 2] = value;
					Network[offset + 3] = 0;
					Frequency[i] = IntBias / NetSize;
					Bias[i] = 0;
				}
			}

			private void Learn()
			{
				int sampleFactor = SampleFactor;
				if (LengthCount < MinPictureBytes)
					sampleFactor = 1;

				int alphaDecrease = 30 + (sampleFactor - 1) / 3;
				int samplePixels = LengthCount / (3 * sampleFactor);
				int delta = samplePixels / NCycles;
				int alpha = InitialAlpha;
				int radius = InitRadius;
				int rad = radius >> RadiusBiasShift;
				if (rad <= 1) rad = 0;
				UpdateRadiusPower(rad, alpha);

				int step;
				if (LengthCount < MinPictureBytes)
					step = 3;
				else if (LengthCount % Prime1 != 0)
					step = 3 * Prime1;
				else if (LengthCount % Prime2 != 0)
					step = 3 * Prime2;
				else if (LengthCount % Prime3 != 0)
					step = 3 * Prime3;
				else
					step = 3 * Prime4;

				int pixelIndex = 0;
				int i = 0;
				while (i < samplePixels)
				{
					Color32 pixel = Picture[ResolvePictureIndex(pixelIndex)];
					int b = (pixel.r & 0xff) << NetBiasShift;
					int g = (pixel.g & 0xff) << NetBiasShift;
					int r = (pixel.b & 0xff) << NetBiasShift;
					int winner = Contest(b, g, r);

					AlterSingle(alpha, winner, b, g, r);
					if (rad != 0)
						AlterNeighbours(rad, winner, b, g, r);

					pixelIndex += step;
					if (pixelIndex >= LengthCount)
						pixelIndex -= LengthCount;

					i++;
					if (delta == 0) delta = 1;
					if (i % delta == 0)
					{
						alpha -= alpha / alphaDecrease;
						radius -= radius / RadiusDecrease;
						rad = radius >> RadiusBiasShift;
						if (rad <= 1) rad = 0;
						UpdateRadiusPower(rad, alpha);
					}
				}
			}

			private int ResolvePictureIndex(int logicalIndex)
			{
				if (FlipVertical == 0)
					return logicalIndex;

				int y = logicalIndex / PictureWidth;
				int x = logicalIndex - y * PictureWidth;
				return (PictureHeight - 1 - y) * PictureWidth + x;
			}

			private void UpdateRadiusPower(int rad, int alpha)
			{
				for (int i = 0; i < rad; i++)
					RadiusPower[i] = alpha * (((rad * rad - i * i) * RadBias) / (rad * rad));
			}

			private int Contest(int b, int g, int r)
			{
				int bestDistance = int.MaxValue;
				int bestBiasDistance = int.MaxValue;
				int bestPosition = -1;
				int bestBiasPosition = -1;

				for (int i = 0; i < NetSize; i++)
				{
					int offset = i * NetworkStride;
					int distance = Abs(Network[offset] - b) +
						Abs(Network[offset + 1] - g) +
						Abs(Network[offset + 2] - r);

					if (distance < bestDistance)
					{
						bestDistance = distance;
						bestPosition = i;
					}

					int biasDistance = distance - (Bias[i] >> (IntBiasShift - NetBiasShift));
					if (biasDistance < bestBiasDistance)
					{
						bestBiasDistance = biasDistance;
						bestBiasPosition = i;
					}

					int betaFrequency = Frequency[i] >> BetaShift;
					Frequency[i] -= betaFrequency;
					Bias[i] += betaFrequency << GammaShift;
				}

				Frequency[bestPosition] += Beta;
				Bias[bestPosition] -= BetaGamma;
				return bestBiasPosition;
			}

			private void AlterSingle(int alpha, int index, int b, int g, int r)
			{
				int offset = index * NetworkStride;
				Network[offset] -= alpha * (Network[offset] - b) / InitialAlpha;
				Network[offset + 1] -= alpha * (Network[offset + 1] - g) / InitialAlpha;
				Network[offset + 2] -= alpha * (Network[offset + 2] - r) / InitialAlpha;
			}

			private void AlterNeighbours(int rad, int index, int b, int g, int r)
			{
				int low = index - rad;
				if (low < -1) low = -1;
				int high = index + rad;
				if (high > NetSize) high = NetSize;

				int upper = index + 1;
				int lower = index - 1;
				int powerIndex = 1;
				while (upper < high || lower > low)
				{
					int power = RadiusPower[powerIndex++];
					if (upper < high)
						AlterNeighbour(upper++, power, b, g, r);
					if (lower > low)
						AlterNeighbour(lower--, power, b, g, r);
				}
			}

			private void AlterNeighbour(int index, int power, int b, int g, int r)
			{
				int offset = index * NetworkStride;
				Network[offset] -= power * (Network[offset] - b) / AlphaRadBias;
				Network[offset + 1] -= power * (Network[offset + 1] - g) / AlphaRadBias;
				Network[offset + 2] -= power * (Network[offset + 2] - r) / AlphaRadBias;
			}

			private void UnbiasNetwork()
			{
				for (int i = 0; i < NetSize; i++)
				{
					int offset = i * NetworkStride;
					Network[offset] >>= NetBiasShift;
					Network[offset + 1] >>= NetBiasShift;
					Network[offset + 2] >>= NetBiasShift;
					Network[offset + 3] = i;
				}
			}

			private void BuildIndex()
			{
				int previousColor = 0;
				int startPosition = 0;

				for (int i = 0; i < NetSize; i++)
				{
					int currentOffset = i * NetworkStride;
					int smallestPosition = i;
					int smallestValue = Network[currentOffset + 1];

					for (int j = i + 1; j < NetSize; j++)
					{
						int value = Network[j * NetworkStride + 1];
						if (value < smallestValue)
						{
							smallestPosition = j;
							smallestValue = value;
						}
					}

					if (i != smallestPosition)
					{
						int smallestOffset = smallestPosition * NetworkStride;
						for (int component = 0; component < NetworkStride; component++)
						{
							int temp = Network[smallestOffset + component];
							Network[smallestOffset + component] = Network[currentOffset + component];
							Network[currentOffset + component] = temp;
						}
					}

					if (smallestValue != previousColor)
					{
						NetIndex[previousColor] = (startPosition + i) >> 1;
						for (int j = previousColor + 1; j < smallestValue; j++)
							NetIndex[j] = i;
						previousColor = smallestValue;
						startPosition = i;
					}
				}

				NetIndex[previousColor] = (startPosition + NetSize - 1) >> 1;
				for (int j = previousColor + 1; j < NetSize; j++)
					NetIndex[j] = NetSize - 1;
			}

			private void BuildColorMap()
			{
				// Bias is no longer needed after learning, so reuse it as the color-order table.
				for (int i = 0; i < NetSize; i++)
					Bias[Network[i * NetworkStride + 3]] = i;

				int outputIndex = 0;
				for (int i = 0; i < NetSize; i++)
				{
					int offset = Bias[i] * NetworkStride;
					ColorMap[outputIndex++] = (byte)Network[offset];
					ColorMap[outputIndex++] = (byte)Network[offset + 1];
					ColorMap[outputIndex++] = (byte)Network[offset + 2];
				}
			}

			private static int Abs(int value)
			{
				return value < 0 ? -value : value;
			}
		}

		[BurstCompile]
		private struct MapPixelsJob : IJobParallelFor
		{
			[ReadOnly] public NativeArray<Color32> Pixels;
			[ReadOnly] public NativeArray<int> Network;
			[ReadOnly] public NativeArray<int> NetIndex;
			[ReadOnly] public NativeArray<byte> ColorMap;
			[WriteOnly] public NativeArray<byte> Result;
			public int UseTransparency;
			public int TransparentIndex;
			public int Width;
			public int Height;
			public int FlipVertical;

			public void Execute(int index)
			{
				int sourceIndex = index;
				if (FlipVertical != 0)
				{
					int y = index / Width;
					int x = index - y * Width;
					sourceIndex = (Height - 1 - y) * Width + x;
				}

				Color32 pixel = Pixels[sourceIndex];
				if (UseTransparency != 0 && pixel.a < 128)
				{
					Result[index] = (byte)TransparentIndex;
					return;
				}

				int mapped = MapColor(pixel.r & 0xff, pixel.g & 0xff, pixel.b & 0xff);
				if (UseTransparency != 0 && mapped == TransparentIndex)
					mapped = FindClosestOpaquePaletteIndex(pixel, TransparentIndex);

				Result[index] = (byte)mapped;
			}

			private int MapColor(int b, int g, int r)
			{
				int bestDistance = 1000;
				int best = -1;
				int i = NetIndex[g];
				int j = i - 1;

				while (i < NetSize || j >= 0)
				{
					if (i < NetSize)
					{
						int offset = i * NetworkStride;
						int distance = Network[offset + 1] - g;
						if (distance >= bestDistance)
							i = NetSize;
						else
						{
							i++;
							if (distance < 0) distance = -distance;
							int delta = Network[offset] - b;
							if (delta < 0) delta = -delta;
							distance += delta;
							if (distance < bestDistance)
							{
								delta = Network[offset + 2] - r;
								if (delta < 0) delta = -delta;
								distance += delta;
								if (distance < bestDistance)
								{
									bestDistance = distance;
									best = Network[offset + 3];
								}
							}
						}
					}

					if (j >= 0)
					{
						int offset = j * NetworkStride;
						int distance = g - Network[offset + 1];
						if (distance >= bestDistance)
							j = -1;
						else
						{
							j--;
							if (distance < 0) distance = -distance;
							int delta = Network[offset] - b;
							if (delta < 0) delta = -delta;
							distance += delta;
							if (distance < bestDistance)
							{
								delta = Network[offset + 2] - r;
								if (delta < 0) delta = -delta;
								distance += delta;
								if (distance < bestDistance)
								{
									bestDistance = distance;
									best = Network[offset + 3];
								}
							}
						}
					}
				}

				return best;
			}

			private int FindClosestOpaquePaletteIndex(Color32 color, int excludedIndex)
			{
				int bestIndex = 0;
				int bestDistance = int.MaxValue;
				int colorCount = ColorMap.Length / 3;

				if (colorCount > NetSize)
					colorCount = NetSize;

				for (int i = 0; i < colorCount; i++)
				{
					if (i == excludedIndex)
						continue;

					int offset = i * 3;
					int dr = color.r - (ColorMap[offset] & 0xff);
					int dg = color.g - (ColorMap[offset + 1] & 0xff);
					int db = color.b - (ColorMap[offset + 2] & 0xff);
					int distance = dr * dr + dg * dg + db * db;

					if (distance < bestDistance)
					{
						bestDistance = distance;
						bestIndex = i;
					}
				}

				return bestIndex;
			}
		}
	}
}
