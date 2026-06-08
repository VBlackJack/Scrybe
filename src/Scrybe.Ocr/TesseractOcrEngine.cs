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

using Scrybe.Core.Interfaces;
using Scrybe.Core.Models;
using Tesseract;

namespace Scrybe.Ocr;

/// <summary>
/// Tesseract 5 OCR engine kept warm across recognitions. The native engine is not thread-safe, so
/// access is serialized. Dictionaries are disabled so technical tokens (service names, paths, GUIDs,
/// flags, hashes) are read literally instead of being "corrected" into dictionary words.
/// </summary>
public sealed class TesseractOcrEngine : IOcrEngine, IDisposable
{
    private const float ConfidencePercentScale = 100f;

    private readonly string _tessdataPath;
    private readonly string _language;
    private readonly Lock _engineGate = new();

    private TesseractEngine? _engine;
    private bool _isDisposed;

    /// <summary>Initializes the engine for a tessdata directory and language.</summary>
    /// <param name="tessdataPath">Directory containing the <c>{language}.traineddata</c> file.</param>
    /// <param name="language">Language code (for example <c>eng</c>).</param>
    public TesseractOcrEngine(string tessdataPath, string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tessdataPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        _tessdataPath = tessdataPath;
        _language = language;
    }

    /// <inheritdoc />
    public void Prewarm()
    {
        lock (_engineGate)
        {
            EnsureEngine();
        }
    }

    /// <inheritdoc />
    public Task<OcrResult> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        return Task.Run(
            () =>
            {
                lock (_engineGate)
                {
                    TesseractEngine engine = EnsureEngine();
                    using Pix pix = Pix.LoadFromMemory(imageBytes);
                    using Page page = engine.Process(pix);
                    string text = page.GetText() ?? string.Empty;
                    float confidence = page.GetMeanConfidence() * ConfidencePercentScale;
                    return new OcrResult(text, confidence);
                }
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_engineGate)
        {
            _engine?.Dispose();
            _engine = null;
            _isDisposed = true;
        }
    }

    private TesseractEngine EnsureEngine()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_engine is null)
        {
            _engine = new TesseractEngine(_tessdataPath, _language, EngineMode.LstmOnly);
            _engine.SetVariable("load_system_dawg", false);
            _engine.SetVariable("load_freq_dawg", false);
            _engine.SetVariable("load_number_dawg", false);
            _engine.SetVariable("load_punc_dawg", false);
            _engine.DefaultPageSegMode = PageSegMode.SingleBlock;
        }

        return _engine;
    }
}
