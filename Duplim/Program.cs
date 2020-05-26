using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Duplim
{
	class Program
	{
		static void Main(string[] args)
		{
			try
			{
				Console.WriteLine("Getting catalogue of jpeg files from source directory '{0}'.", args[0]);
				HashSet<string> jpegFiles = new HashSet<string>(
					Directory.GetFiles(args[0], "*.jpg", SearchOption.AllDirectories)
				);

				Console.WriteLine("Creating comparison objects for files.");
				double onePercent = jpegFiles.Count / 100.0d;
				double nextPercent = onePercent;
				HashSet<ComparisonData> cdfiles = new HashSet<ComparisonData>();
				MyTimer timer = new MyTimer();
				foreach (string file in jpegFiles)
				{
					cdfiles.Add(new ComparisonData(file, 128));
					if (cdfiles.Count > nextPercent)
					{
						double multiplier = (double)jpegFiles.Count / (double)cdfiles.Count;
						double truePercentComplete = (double)cdfiles.Count / (double)jpegFiles.Count;
						double estimatedTimeRemaining = timer.getEstimatedTime(cdfiles.Count, jpegFiles.Count) / 1000.0d;
						Console.WriteLine("Created {0} of {1} comparison objects ({2:F2}%) - {3:F2} seconds remaining (estimated)    ", 
							cdfiles.Count, jpegFiles.Count, truePercentComplete * 100.0d, estimatedTimeRemaining);
						Console.SetCursorPosition(0, Console.CursorTop - 1);
						nextPercent += onePercent;
					}
				}
				Console.WriteLine("\nDone creating comparison objects.");
				Console.WriteLine("Finding duplicate files.  Please be patient, this could take a long time!");
				LinkedList<ComparisonData> cdFilesToRemove = new LinkedList<ComparisonData>();
				LinkedList<ComparisonData> cdFilesDone = new LinkedList<ComparisonData>();
				nextPercent = onePercent;
				timer.restart();
				foreach (ComparisonData cdfile in cdfiles)
				{
					if (!cdFilesDone.Contains(cdfile) && !cdFilesToRemove.Contains(cdfile))
					{
						int filesDone = cdFilesDone.Count + cdFilesToRemove.Count + 1;
						int filesRemaining = cdfiles.Count - filesDone;
						if (filesDone >= nextPercent)
						{
							double estimatedTimeRemaining = timer.getEstimatedTime(filesDone, jpegFiles.Count) / 1000.0d;
							Console.WriteLine("Found duplicates for {0} files ({1} remaining) - {2:F2} seconds remaining (estimated)    ",
								filesDone, filesRemaining, estimatedTimeRemaining);
							Console.SetCursorPosition(0, Console.CursorTop - 1);
							nextPercent += onePercent;
						}
						
						foreach (ComparisonData compfile in cdfiles)
						{
							if (!cdFilesToRemove.Contains(compfile) && !cdFilesDone.Contains(compfile) && compfile != cdfile)
							{
								if (compfile.compareTo(cdfile) > 0.6) cdFilesToRemove.AddLast(compfile);
							}
						}
						cdFilesDone.AddLast(cdfile);
					}
				}
				timer.stop();
				Console.WriteLine("\nDone finding duplicate images.");
				Console.WriteLine("Moving files to duplicates folder.");
				foreach (ComparisonData cdFileToRemove in cdFilesToRemove)
				{
					int firstSlash = cdFileToRemove.filename.IndexOf('\\');
					string destPath = args[1] + cdFileToRemove.filename.Substring(firstSlash);
					try
					{
						string parentPath = destPath.Substring(0, destPath.LastIndexOf('\\'));
						if (!Directory.Exists(parentPath)) Directory.CreateDirectory(parentPath);
						File.Move(cdFileToRemove.filename, destPath);
					} catch (IOException ioex)
					{
						Console.WriteLine("Failed to move file '{0}': {1}", cdFileToRemove.filename, ioex.ToString());
					}
				}
				cdFilesToRemove.Clear();
				cdFilesDone.Clear();
				cdfiles.Clear();
				jpegFiles.Clear();
				Console.WriteLine("Done moving duplicate files.  Moved {1} files to '{0}'.  Please press any key.",
					args[1], cdFilesToRemove.Count);
			}
			catch	(Exception ex)
			{
				Console.WriteLine("The process failed: {0}", ex.ToString());
			}
			Console.ReadKey();
		}
	}

	sealed internal class MyTimer
	{
		public MyTimer()
		{
			sw = Stopwatch.StartNew();
		}

		public double getEstimatedTime(double itemsDone, double totalItems)
		{
			double itemAverage = sw.ElapsedMilliseconds / itemsDone;
			return (totalItems - itemsDone) * itemAverage;
		}

		public void stop() => sw.Stop();

		public void restart()
		{
			sw.Stop();
			sw = Stopwatch.StartNew();
		}

		private Stopwatch sw;
	}

	sealed internal class YCCSample
	{
		public YCCSample()
		{
			this.Y = this.Cr = this.Cb = 0;
		}
		public YCCSample(byte red, byte green, byte blue)
		{
			this.Y = (byte)(lumConstants[red, 0] + lumConstants[green, 1] + lumConstants[blue, 2]);
			this.Cr = (byte)((crConstants[red, 0] + crConstants[green, 1] + crConstants[blue, 2]) + 128);
			this.Cb = (byte)((cbConstants[red, 0] + cbConstants[green, 1] + cbConstants[blue, 2]) + 128);
		}

		public bool withinTolerances(YCCSample value2)
		{
			if (value2 == null) return false;
			byte maxY = (byte) ((Y + 48) > 255 ? 255 : Y + 48);
			byte minY = (byte) ((Y - 48) < 0 ? 0 : Y - 48);
			byte maxCr = (byte)((Cr + 32) > 255 ? 255 : Cr + 32);
			byte minCr = (byte)((Cr - 32) < 0 ? 0 : Cr - 32);
			byte maxCb = (byte)((Cb + 32) > 255 ? 255 : Cb + 32);
			byte minCb = (byte)((Cb - 32) < 0 ? 0 : Cb - 32);
			return (value2.Y >= minY && value2.Y <= maxY && value2.Cr >= minCr && value2.Cr <= maxCr
				&& value2.Cb >= minCb && value2.Cb <= maxCb);
		}

		static YCCSample()
		{
			for (int i = 0; i < 256; i++)
			{
				lumConstants[i, 0] = (sbyte)(i * 0.299);
				lumConstants[i, 1] = (sbyte)(i * 0.587);
				lumConstants[i, 2] = (sbyte)(i * 0.114);
				crConstants[i, 0] = (sbyte)(i * 0.5);
				crConstants[i, 1] = (sbyte)(i * -0.419);
				crConstants[i, 2] = (sbyte)(i * -0.081);
				cbConstants[i, 0] = (sbyte)(i * -0.169);
				cbConstants[i, 1] = (sbyte)(i * -0.331);
				cbConstants[i, 2] = (sbyte)(i * 0.5);
			}
		}

		public byte Y, Cr, Cb;
		private static sbyte[,] lumConstants = new sbyte[256, 3];
		private static sbyte[,] crConstants = new sbyte[256, 3];
		private static sbyte[,] cbConstants = new sbyte[256, 3];
	}

	sealed internal class ComparisonData
	{
		public ComparisonData(String filename, int scaledImageHeight)
		{
			if (filename != null)
			{
				if (scaledImageHeight < 128) scaledImageHeight = 128;
				else if (scaledImageHeight > 512) scaledImageHeight = 512;
				else
				{
					for (int allowedScaledHeight = 128; allowedScaledHeight <= 512; allowedScaledHeight *= 2)
					{
						if (scaledImageHeight < allowedScaledHeight)
						{
							scaledImageHeight = allowedScaledHeight;
							break;
						}
					}
				}

				// Load the full size image into memory.
				try
				{
					Bitmap originalImage = new Bitmap(filename);

					// Create a high quality scaled version of the image if it's larger than the required scaled height;
					Bitmap scaledImage = null;
					if (originalImage.Height > scaledImageHeight)
					{
						double scale = (double)scaledImageHeight / (double)originalImage.Height;
						int scaledImageWidth = (int)(originalImage.Width * scale);
						Rectangle scaledSize = new Rectangle(0, 0, scaledImageWidth, scaledImageHeight);
						scaledImage = new Bitmap(scaledImageWidth, scaledImageHeight);
						using (var graphics = Graphics.FromImage(scaledImage))
						{
							graphics.CompositingMode = CompositingMode.SourceCopy;
							graphics.CompositingQuality = CompositingQuality.HighSpeed;
							graphics.InterpolationMode = InterpolationMode.Bicubic;
							graphics.SmoothingMode = SmoothingMode.HighSpeed;
							graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
							using (var wrapMode = new ImageAttributes())
							{
								wrapMode.SetWrapMode(WrapMode.TileFlipXY);
								graphics.DrawImage(originalImage, scaledSize, 0, 0, originalImage.Width, originalImage.Height,
									GraphicsUnit.Pixel, wrapMode);
							}
							originalImage.Dispose();
						}
					}
					else scaledImage = originalImage;

					// Convert the scaled image's RGB data to YCC samples.
					int columns = scaledImage.Width / 16;
					int rows = scaledImage.Height / 16;
					if (scaledImage.Width / 16 < scaledImage.Width / 16.0) columns++;
					if (scaledImage.Height / 16 < scaledImage.Height / 16.0) rows++;
					data = new YCCSample[columns * rows];
					columnAverages = new YCCSample[columns];
					int d = 0, c = 0;
					for (int x = 0; x < scaledImage.Width; x += 16)
					{
						int colY = 0, colCr = 0, colCb = 0;
						int blockWidth = (x + 16 >= scaledImage.Width) ? scaledImage.Width - x : 16;
						for (int y = 0; y < scaledImage.Height; y += 16)
						{
							int blockHeight = (y + 16 >= scaledImage.Height) ? scaledImage.Height - y : 16;
							int totalRed = 0, totalGreen = 0, totalBlue = 0;
							for (int k = y; k < y + blockHeight; k++)
							{
								for (int j = x; j < x + blockWidth; j++)
								{
									Color pixel = scaledImage.GetPixel(j, k);
									totalRed += pixel.R;
									totalGreen += pixel.G;
									totalBlue += pixel.B;
								}
							}
							int blockSize = blockWidth * blockHeight;
							byte averageRed = (byte)(totalRed / blockSize);
							byte averageGreen = (byte)(totalGreen / blockSize);
							byte averageBlue = (byte)(totalBlue / blockSize);
							YCCSample blockAvg = new YCCSample(averageRed, averageGreen, averageBlue);
							data[d++] = blockAvg;
							colY += blockAvg.Y;
							colCr += blockAvg.Cr;
							colCb += blockAvg.Cb;
						}
						YCCSample colAvg = new YCCSample
						{
							Y = (byte)(colY / rows),
							Cr = (byte)(colCr / rows),
							Cb = (byte)(colCb / rows)
						};
						columnAverages[c++] = colAvg;
					}
					// Release any resources used by the scaled image.
					scaledImage.Dispose();
					// Save a copy of the file path
					this.filename = new string(filename.ToCharArray());
				} catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
			}
		}

		public double compareTo(ComparisonData otherImage)
		{
			double rating = 0, target_rating = 0.6;
			if (otherImage != null)
			{
				if (otherImage == this) rating = 1.0d;
				else
				{
					for (int i = 0; i < this.columnAverages.Length && rating < target_rating; i++)
					{
						for (int j = 0; j < otherImage.columnAverages.Length && rating <= target_rating; j++)
						{
							if (otherImage.columnAverages[j].withinTolerances(columnAverages[i]))
							{
								int consecutiveMatches = 0;
								int n = j, m = i;
								for (int k = 0; k < otherImage.columnAverages.Length && m < this.columnAverages.Length; k++)
								{
									if (otherImage.columnAverages[n].withinTolerances(columnAverages[m]))
									{
										int match = compareBlocks(m, otherImage, n);
										if (match == 0 && m > 0) match = compareBlocks(m - 1, otherImage, n);
										if (match == 0 && m < columnAverages.Length - 1) match = compareBlocks(m + 1, otherImage, n);
										if (match == 0 && m - 1 > 0) match = compareBlocks(m - 2, otherImage, n);
										if (match == 0 && m + 1 < columnAverages.Length - 1) match = compareBlocks(m + 2, otherImage, n);
										consecutiveMatches += match;
									}
									n++;
									if (n == otherImage.columnAverages.Length) n = 0;
									if (n == j) break;
									m++;
								}
								rating = consecutiveMatches / (double)this.columnAverages.Length;
							}
						}
					}
				}
			}
			return rating;
		}

		public int compareBlocks(int thisColumn, ComparisonData other, int otherColumn)
		{
			int match = 0;
			if (other != null)
			{
				if (other == this && thisColumn == otherColumn) match = 1;
				else
				{
					int thisStride = columnAverages.Length;
					int otherStride = other.columnAverages.Length;
					int matches = 0;
					int requiredMatches = (int) ((data.Length / thisStride) * 0.333);
					int i = thisColumn;
					int k = otherColumn;
					while (i < data.Length)
					{
						bool found = false;
						while (k < other.data.Length && !found)
						{
							if (other.data[k].withinTolerances(data[i]))
							{
								matches = 1;
								found = true;
							}
							k += otherStride;
						}
						i += thisStride;
						while (k < other.data.Length && i < data.Length && other.data[k].withinTolerances(data[i]))
						{
							k += otherStride;
							i += thisStride;
							matches++;
						}
					}
					if (matches >= requiredMatches) match = 1;
				}
			}
			return match;
		}

		public String filename = null;
		public YCCSample[] data = null;
		public YCCSample[] columnAverages = null;
	}
}