// Adapted from the WinML MNIST sample and Rene Schulte's repo 
// https://github.com/microsoft/Windows-Machine-Learning/tree/master/Samples/MNIST
// https://github.com/reneschulte/WinMLExperiments/

using System;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEngine.Networking;
using System.Net.Http;
using System.Net;
using System.Threading;
// using Windows.Storage.Streams;
// using Windows.Web.Http;

public class NetworkBehaviour : MonoBehaviour
{
    // Public fields
    public float ProbabilityThreshold = 0.3f;
    public Vector2 InputFeatureSize = new Vector2(224, 224);
    public Text StatusBlock;

    // Private fields
    private NetworkModel _networkModel;
    private MediaCaptureUtility _mediaCaptureUtility;
    private bool _isRunning = false;

    private static ManualResetEvent allDone = new ManualResetEvent(false);

    #region UnityMethods
    async void Start()
    {
        try
        {
            // Create a new instance of the network model class
            // and asynchronously load the onnx model
            _networkModel = new NetworkModel();
            await _networkModel.LoadModelAsync();
            StatusBlock.text = $"Loaded model. Starting camera...";

#if ENABLE_WINMD_SUPPORT
            // Configure camera to return frames fitting the model input size
            try
            {
                UnityEngine.Debug.Log("Creating MediaCaptureUtility and initializing frame reader.");
                _mediaCaptureUtility = new MediaCaptureUtility();
                await _mediaCaptureUtility.InitializeMediaFrameReaderAsync(
                    (uint)InputFeatureSize.x, (uint)InputFeatureSize.y);
                StatusBlock.text = $"Camera started. Running!";

                UnityEngine.Debug.Log("Successfully initialized frame reader.");
            }
            catch (Exception ex)
            {
                StatusBlock.text = $"Failed to start camera: {ex.Message}. Using loaded/picked image.";

            }

            // Run processing loop in separate parallel Task, get the latest frame
            // and asynchronously evaluate
            UnityEngine.Debug.Log("Begin performing inference in frame grab loop.");
            _isRunning = true;
            await Task.Run(async () =>
            {
                
                while (_isRunning)
                {
                    if (_mediaCaptureUtility.IsCapturing)
                    {
                        using (var videoFrame = _mediaCaptureUtility.GetLatestFrame())
                        {

                            await EvaluateFrame(videoFrame);
 

                        }
                    }
                    else
                    {
                        return;
                    }
                    
                }
            });
#endif 
        }
        catch (Exception ex)
        {
            StatusBlock.text = $"Error init: {ex.Message}";
            UnityEngine.Debug.LogError($"Failed to start model inference: {ex}");
        }
    }

    private async void OnDestroy()
    {
        _isRunning = false;
        if (_mediaCaptureUtility != null)
        {
            await _mediaCaptureUtility.StopMediaFrameReaderAsync();
        }
    }
    #endregion

#if ENABLE_WINMD_SUPPORT
    private async Task EvaluateFrame(Windows.Media.VideoFrame videoFrame)
    {
        try
        {
            // Get the current network prediction from model and input frame
            var result = await _networkModel.EvaluateVideoFrameAsync(videoFrame);
            // Update the UI with prediction
            UnityEngine.WSA.Application.InvokeOnAppThread(async () =>
            {
                    StatusBlock.text = $"Prediction: {result.PredictionLabel}, " +
                    $"Probability: {Math.Round(result.PredictionProbability, 3) * 100}% " +
                    $"Inference time: {result.PredictionTime} ms";
                    
                    await TryPostJsonAsync(result.PredictionLabel);
            }
            , false);

        }
        catch (Exception ex)
        {
            UnityEngine.Debug.Log($"Exception {ex}");
        }
    }
#endif

    private async Task TryPostJsonAsync(string predLabel)
    {
        try
        {
            double [] coordinates = new double[2];
            coordinates[0] = GetRandomNumber(0.0f, 50.0f);
            coordinates[1] = GetRandomNumber(0.0f, 50.0f);

            // Construct the HttpClient and Uri. This endpoint is for test purposes only.
            HttpClient httpClient = new HttpClient();
            Uri uri = new Uri("http://eagle5.di.uoa.gr:8082/topics/SCP476");

            if(predLabel == "None" || predLabel == "No prediction exceeded probability threshold.") return;
            else predLabel = predLabel.Substring(0, predLabel.Length - 1);

            // Construct the JSON to post.
            string json = "{\"records\":[{\"value\":{\"coordinates\":[" + string.Join(",", coordinates) + "], \"prediction\": " + predLabel + " }}]}";
            var content = new System.Net.Http.StringContent(json, UnicodeEncoding.UTF8, "application/vnd.kafka.json.v2+json");

            // Post the JSON and wait for a response.
            HttpResponseMessage httpResponseMessage = await httpClient.PostAsync(uri, content);

            // Make sure the post succeeded, and write out the response.
            httpResponseMessage.EnsureSuccessStatusCode();
            var httpResponseBody = await httpResponseMessage.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Write out any exceptions.
            Debug.WriteLine(ex);
        }
    }



    public double GetRandomNumber(double minimum, double maximum)
    { 
        System.Random random = new System.Random();
        return random.NextDouble() * (maximum - minimum) + minimum;
    }
    


}