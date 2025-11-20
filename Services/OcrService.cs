using System;
using System.Diagnostics;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Online;
using TarkovPriceViewer.Configuration;

namespace TarkovPriceViewer.Services
{
    public interface IOcrService
    {
        void EnsureInitialized(string language);
        string RecognizeText(Mat textMat, char[] currencySplitChars);
    }

    public class OcrService : IOcrService
    {
        private readonly object _lock = new object();
        private RecognizationModel _languageModel;
        private PaddleOcrRecognizer _ocrRecognizer;

        public void EnsureInitialized(string language)
        {
            if (_ocrRecognizer != null)
            {
                return;
            }

            EnsureModel(language);

            if (_languageModel == null)
            {
                return;
            }

            lock (_lock)
            {
                if (_ocrRecognizer != null)
                {
                    return;
                }

                try
                {
                    _ocrRecognizer = new PaddleOcrRecognizer(_languageModel, PaddleDevice.Gpu());
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error creating PaddleOcrRecognizer GPU: " + e.Message);
                    try
                    {
                        _ocrRecognizer = new PaddleOcrRecognizer(_languageModel, PaddleDevice.Mkldnn());
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error creating PaddleOcrRecognizer CPU: " + ex.Message);
                        _ocrRecognizer = null;
                    }
                }
            }
        }

        private void EnsureModel(string language)
        {
            if (_languageModel != null)
            {
                return;
            }

            try
            {
                Debug.WriteLine("Download the paddle language model.");
                RecognizationModel model;
                if (language == "ko")
                {
                    model = LocalDictOnlineRecognizationModel.KoreanV4.DownloadAsync().GetAwaiter().GetResult();
                }
                else if (language == "cn")
                {
                    model = LocalDictOnlineRecognizationModel.ChineseV4.DownloadAsync().GetAwaiter().GetResult();
                }
                else if (language == "jp")
                {
                    model = LocalDictOnlineRecognizationModel.JapanV4.DownloadAsync().GetAwaiter().GetResult();
                }
                else
                {
                    model = LocalDictOnlineRecognizationModel.EnglishV4.DownloadAsync().GetAwaiter().GetResult();
                }

                lock (_lock)
                {
                    if (_languageModel == null)
                    {
                        Debug.WriteLine("language model setted.");
                        _languageModel = model;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error downloading Paddle model: " + ex.Message);
            }
        }

        public string RecognizeText(Mat textMat, char[] currencySplitChars)
        {
            if (textMat == null)
            {
                return string.Empty;
            }

            EnsureInitialized("en"); // language should be set by caller via EnsureInitialized beforehand

            if (_ocrRecognizer == null)
            {
                return string.Empty;
            }

            string text = string.Empty;
            try
            {
                lock (_lock)
                {
                    var result = _ocrRecognizer.Run(textMat);
                    if (result.Score > 0.5f)
                    {
                        text = result.Text.Replace("\n", " ").Split(currencySplitChars)[0].Trim();
                    }
                    Debug.WriteLine(result.Score + " Paddle Text : " + result.Text);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Paddle error: " + e.Message);
            }

            return text;
        }
    }
}
