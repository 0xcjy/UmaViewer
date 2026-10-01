using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace uGIF
{
	public class Image
	{
		public int width;
		public int height;
		public Color32[] pixels;

		public Image (Texture2D f)
		{
			pixels = f.GetPixels32 ();
			width = f.width;
			height = f.height;
		}

		public Image (Image image)
		{
			pixels = image.pixels.Clone () as Color32[];
			width = image.width;
			height = image.height;
		}

		public Image (int width, int height)
		{
			this.width = width;
			this.height = height;
			pixels = new Color32[width * height];
		}

		public void DrawImage (Image image, int i, int i2)
		{
			throw new System.NotImplementedException ();
		}

		public Color32 GetPixel (int tw, int th)
		{
			var index = (th * width) + tw;
			return pixels [index];
		}

		public void Flip ()
		{
			if (pixels == null || pixels.Length == 0 || width <= 0 || height <= 1)
				return;

			using (var source = new NativeArray<Color32>(pixels, Allocator.TempJob))
			using (var destination = new NativeArray<Color32>(pixels.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			{
				new FlipVerticalJob
				{
					Source = source,
					Destination = destination,
					Width = width,
					Height = height
				}.Schedule(pixels.Length, 256).Complete();

				destination.CopyTo(pixels);
			}
		}

		public void Resize (int scale)
		{
			if (scale <= 1)
				return;
			var newWidth = width / scale;
			var newHeight = height / scale;
			if (newWidth <= 0 || newHeight <= 0)
				return;

			var newColors = new Color32[newWidth * newHeight];
			using (var source = new NativeArray<Color32>(pixels, Allocator.TempJob))
			using (var destination = new NativeArray<Color32>(newColors.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			{
				new PointResizeJob
				{
					Source = source,
					Destination = destination,
					SourceWidth = width,
					DestinationWidth = newWidth,
					Scale = scale
				}.Schedule(newColors.Length, 256).Complete();

				destination.CopyTo(newColors);
			}

			pixels = newColors;
			height = newHeight;
			width = newWidth;
		}

		public void ResizeBilinear (int newWidth, int newHeight)
		{
			if (newWidth == width && newHeight == height)
				return;
			if (newWidth <= 0 || newHeight <= 0 || width <= 0 || height <= 0)
				return;
			if (width == 1 || height == 1)
			{
				ResizeNearest(newWidth, newHeight);
				return;
			}

			var texColors = pixels;
			var newColors = new Color32[newWidth * newHeight];
			var ratioX = 1.0f / ((float)newWidth / (width - 1));
			var ratioY = 1.0f / ((float)newHeight / (height - 1));

			using (var source = new NativeArray<Color32>(texColors, Allocator.TempJob))
			using (var destination = new NativeArray<Color32>(newColors.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			{
				new BilinearResizeJob
				{
					Source = source,
					Destination = destination,
					SourceWidth = width,
					SourceHeight = height,
					DestinationWidth = newWidth,
					RatioX = ratioX,
					RatioY = ratioY
				}.Schedule(newColors.Length, 256).Complete();

				destination.CopyTo(newColors);
			}

			pixels = newColors;
			height = newHeight;
			width = newWidth;
		}

		private void ResizeNearest(int newWidth, int newHeight)
		{
			var newColors = new Color32[newWidth * newHeight];
			using (var source = new NativeArray<Color32>(pixels, Allocator.TempJob))
			using (var destination = new NativeArray<Color32>(newColors.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
			{
				new NearestResizeJob
				{
					Source = source,
					Destination = destination,
					SourceWidth = width,
					SourceHeight = height,
					DestinationWidth = newWidth,
					DestinationHeight = newHeight
				}.Schedule(newColors.Length, 256).Complete();

				destination.CopyTo(newColors);
			}

			pixels = newColors;
			width = newWidth;
			height = newHeight;
		}

		[BurstCompile]
		private struct FlipVerticalJob : IJobParallelFor
		{
			[ReadOnly] public NativeArray<Color32> Source;
			[WriteOnly] public NativeArray<Color32> Destination;
			public int Width;
			public int Height;

			public void Execute(int index)
			{
				int y = index / Width;
				int x = index - y * Width;
				Destination[index] = Source[(Height - 1 - y) * Width + x];
			}
		}

		[BurstCompile]
		private struct PointResizeJob : IJobParallelFor
		{
			[ReadOnly] public NativeArray<Color32> Source;
			[WriteOnly] public NativeArray<Color32> Destination;
			public int SourceWidth;
			public int DestinationWidth;
			public int Scale;

			public void Execute(int index)
			{
				int y = index / DestinationWidth;
				int x = index - y * DestinationWidth;
				Destination[index] = Source[(y * Scale) * SourceWidth + x * Scale];
			}
		}

		[BurstCompile]
		private struct NearestResizeJob : IJobParallelFor
		{
			[ReadOnly] public NativeArray<Color32> Source;
			[WriteOnly] public NativeArray<Color32> Destination;
			public int SourceWidth;
			public int SourceHeight;
			public int DestinationWidth;
			public int DestinationHeight;

			public void Execute(int index)
			{
				int y = index / DestinationWidth;
				int x = index - y * DestinationWidth;
				int sourceX = x * SourceWidth / DestinationWidth;
				int sourceY = y * SourceHeight / DestinationHeight;
				Destination[index] = Source[sourceY * SourceWidth + sourceX];
			}
		}

		[BurstCompile]
		private struct BilinearResizeJob : IJobParallelFor
		{
			[ReadOnly] public NativeArray<Color32> Source;
			[WriteOnly] public NativeArray<Color32> Destination;
			public int SourceWidth;
			public int SourceHeight;
			public int DestinationWidth;
			public float RatioX;
			public float RatioY;

			public void Execute(int index)
			{
				int y = index / DestinationWidth;
				int x = index - y * DestinationWidth;
				float sourceX = x * RatioX;
				float sourceY = y * RatioY;
				int xFloor = (int)sourceX;
				int yFloor = (int)sourceY;

				if (xFloor >= SourceWidth - 1)
					xFloor = SourceWidth - 2;
				if (yFloor >= SourceHeight - 1)
					yFloor = SourceHeight - 2;

				float xLerp = sourceX - xFloor;
				float yLerp = sourceY - yFloor;
				int row0 = yFloor * SourceWidth;
				int row1 = (yFloor + 1) * SourceWidth;

				Color32 top = LerpColor32(Source[row0 + xFloor], Source[row0 + xFloor + 1], xLerp);
				Color32 bottom = LerpColor32(Source[row1 + xFloor], Source[row1 + xFloor + 1], xLerp);
				Destination[index] = LerpColor32(top, bottom, yLerp);
			}

			private static Color32 LerpColor32(Color32 a, Color32 b, float t)
			{
				Color colorA = a;
				Color colorB = b;
				return new Color(
					colorA.r + (colorB.r - colorA.r) * t,
					colorA.g + (colorB.g - colorA.g) * t,
					colorA.b + (colorB.b - colorA.b) * t,
					colorA.a + (colorB.a - colorA.a) * t);
			}
		}

	}
}
