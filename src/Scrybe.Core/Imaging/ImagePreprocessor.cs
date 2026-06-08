/*
 * Copyright 2026 Julien Bombled
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Scrybe.Core.Models;

namespace Scrybe.Core.Imaging;

/// <summary>
/// Pure, deterministic, UI-free OCR pre-processing pipeline for console captures. The proven lever is
/// automatic polarity inversion: OCR engines are trained mostly on dark-on-light text, so the
/// light-on-dark output of a console must be inverted first. Grayscale, conditional integer upscale,
/// contrast normalization and (single-pass) Otsu binarization complete the chain.
/// </summary>
public static class ImagePreprocessor
{
    private const int BytesPerPixel = 4;
    private const double RedWeight = 0.299;
    private const double GreenWeight = 0.587;
    private const double BlueWeight = 0.114;
    private const int LuminanceLevels = 256;
    private const byte MaxLuminance = 255;

    /// <summary>Runs the pipeline over <paramref name="input"/> and returns the processed image and its decisions.</summary>
    /// <param name="input">The cropped region to process.</param>
    /// <param name="options">Tunable parameters for the pipeline.</param>
    /// <param name="forceInvert">When set, overrides automatic polarity detection (used by tests).</param>
    public static PreprocessingResult Process(PixelBuffer input, PreprocessingOptions options, bool? forceInvert = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);

        int width = input.Width;
        int height = input.Height;
        byte[] gray = ToGrayscale(input);

        bool invert = forceInvert ?? ShouldInvert(gray, width, height, options.InversionLuminanceThreshold);
        if (invert)
        {
            Invert(gray);
        }

        int upscaleFactor = DetermineUpscaleFactor(gray, width, height, options);
        if (upscaleFactor > 1)
        {
            (gray, width, height) = Upscale(gray, width, height, upscaleFactor);
        }

        NormalizeContrast(gray);
        if (options.ApplyBinarization)
        {
            Binarize(gray);
        }

        PixelBuffer output = ToBgra(gray, width, height);
        return new PreprocessingResult(output, invert, upscaleFactor);
    }

    private static byte[] ToGrayscale(PixelBuffer input)
    {
        int pixelCount = input.Width * input.Height;
        byte[] gray = new byte[pixelCount];
        byte[] source = input.Bgra;

        for (int i = 0; i < pixelCount; i++)
        {
            int offset = i * BytesPerPixel;
            double luminance = (source[offset + 2] * RedWeight)
                + (source[offset + 1] * GreenWeight)
                + (source[offset] * BlueWeight);
            gray[i] = (byte)Math.Clamp((int)Math.Round(luminance), 0, MaxLuminance);
        }

        return gray;
    }

    private static bool ShouldInvert(byte[] gray, int width, int height, double luminanceThreshold)
    {
        long sum = 0;
        long count = 0;

        for (int x = 0; x < width; x++)
        {
            sum += gray[x];
            sum += gray[((height - 1) * width) + x];
            count += 2;
        }

        for (int y = 0; y < height; y++)
        {
            sum += gray[y * width];
            sum += gray[(y * width) + (width - 1)];
            count += 2;
        }

        double borderMean = (double)sum / count;
        return borderMean < luminanceThreshold;
    }

    private static void Invert(byte[] gray)
    {
        for (int i = 0; i < gray.Length; i++)
        {
            gray[i] = (byte)(MaxLuminance - gray[i]);
        }
    }

    private static int DetermineUpscaleFactor(byte[] gray, int width, int height, PreprocessingOptions options)
    {
        int estimatedLineHeight = EstimateLineHeight(gray, width, height, options);
        return estimatedLineHeight > 0 && estimatedLineHeight <= options.MinLineHeightPx
            ? options.UpscaleFactor
            : 1;
    }

    private static int EstimateLineHeight(byte[] gray, int width, int height, PreprocessingOptions options)
    {
        int minInkPerRow = (int)Math.Ceiling(options.RowInkMinFraction * width);
        int bandCount = 0;
        int textRowTotal = 0;
        bool inBand = false;

        for (int y = 0; y < height; y++)
        {
            int inkInRow = 0;
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                if (gray[rowOffset + x] <= options.InkLuminanceThreshold)
                {
                    inkInRow++;
                }
            }

            bool isTextRow = inkInRow >= minInkPerRow;
            if (isTextRow)
            {
                textRowTotal++;
                if (!inBand)
                {
                    inBand = true;
                    bandCount++;
                }
            }
            else
            {
                inBand = false;
            }
        }

        return bandCount == 0 ? 0 : (int)Math.Round((double)textRowTotal / bandCount);
    }

    private static (byte[] Gray, int Width, int Height) Upscale(byte[] gray, int width, int height, int factor)
    {
        int newWidth = width * factor;
        int newHeight = height * factor;
        byte[] destination = new byte[newWidth * newHeight];

        // Bilinear interpolation: smooth edges preserve the antialiased thin strokes the LSTM relies on.
        for (int y = 0; y < newHeight; y++)
        {
            double sourceY = ((y + 0.5) / factor) - 0.5;
            int topRow = (int)Math.Floor(sourceY);
            double weightY = sourceY - topRow;
            int topRowClamped = Math.Clamp(topRow, 0, height - 1);
            int bottomRowClamped = Math.Clamp(topRow + 1, 0, height - 1);
            int destinationRow = y * newWidth;

            for (int x = 0; x < newWidth; x++)
            {
                double sourceX = ((x + 0.5) / factor) - 0.5;
                int leftColumn = (int)Math.Floor(sourceX);
                double weightX = sourceX - leftColumn;
                int leftClamped = Math.Clamp(leftColumn, 0, width - 1);
                int rightClamped = Math.Clamp(leftColumn + 1, 0, width - 1);

                double top = (gray[(topRowClamped * width) + leftClamped] * (1.0 - weightX))
                    + (gray[(topRowClamped * width) + rightClamped] * weightX);
                double bottom = (gray[(bottomRowClamped * width) + leftClamped] * (1.0 - weightX))
                    + (gray[(bottomRowClamped * width) + rightClamped] * weightX);
                double value = (top * (1.0 - weightY)) + (bottom * weightY);

                destination[destinationRow + x] = (byte)Math.Clamp((int)Math.Round(value), 0, MaxLuminance);
            }
        }

        return (destination, newWidth, newHeight);
    }

    private static void NormalizeContrast(byte[] gray)
    {
        byte min = MaxLuminance;
        byte max = 0;

        foreach (byte value in gray)
        {
            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        if (max <= min)
        {
            return;
        }

        int range = max - min;
        for (int i = 0; i < gray.Length; i++)
        {
            gray[i] = (byte)((gray[i] - min) * MaxLuminance / range);
        }
    }

    private static void Binarize(byte[] gray)
    {
        int threshold = OtsuThreshold(gray);
        for (int i = 0; i < gray.Length; i++)
        {
            gray[i] = gray[i] <= threshold ? (byte)0 : MaxLuminance;
        }
    }

    private static int OtsuThreshold(byte[] gray)
    {
        int[] histogram = new int[LuminanceLevels];
        foreach (byte value in gray)
        {
            histogram[value]++;
        }

        int total = gray.Length;
        double sumAll = 0;
        for (int t = 0; t < LuminanceLevels; t++)
        {
            sumAll += (double)t * histogram[t];
        }

        double sumBackground = 0;
        int weightBackground = 0;
        double maxVariance = 0;
        int threshold = 0;

        for (int t = 0; t < LuminanceLevels; t++)
        {
            weightBackground += histogram[t];
            if (weightBackground == 0)
            {
                continue;
            }

            int weightForeground = total - weightBackground;
            if (weightForeground == 0)
            {
                break;
            }

            sumBackground += (double)t * histogram[t];
            double meanBackground = sumBackground / weightBackground;
            double meanForeground = (sumAll - sumBackground) / weightForeground;
            double betweenVariance = (double)weightBackground * weightForeground
                * (meanBackground - meanForeground) * (meanBackground - meanForeground);

            if (betweenVariance > maxVariance)
            {
                maxVariance = betweenVariance;
                threshold = t;
            }
        }

        return threshold;
    }

    private static PixelBuffer ToBgra(byte[] gray, int width, int height)
    {
        byte[] bgra = new byte[width * height * BytesPerPixel];
        for (int i = 0; i < gray.Length; i++)
        {
            int offset = i * BytesPerPixel;
            byte value = gray[i];
            bgra[offset] = value;
            bgra[offset + 1] = value;
            bgra[offset + 2] = value;
            bgra[offset + 3] = MaxLuminance;
        }

        return new PixelBuffer(bgra, width, height);
    }
}
