using System.Text;
using System.Text.Json;

namespace GameEngine.Editor.ImageGen;


// Node ID constants - organized in logical workflow order
internal static class NodeIds
{
    public const string CheckpointLoader = "1";
    public const string PositivePrompt = "2";
    public const string NegativePrompt = "3";
    public const string EmptyLatent = "4";
    public const string KSampler = "5";
    public const string VAEDecode = "6";
    public const string BackgroundRemoval = "7";
}

// Helper for creating node references
internal static class NodeRef
{
    public static object[] To(string nodeId, int output = 0) => new object[] { nodeId, output };
}

public class ImageGenerationRequest
{
    public string PromptText { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = "text, watermark";
    public long? Seed { get; set; }
    public int Width { get; set; } = 512;
    public int Height { get; set; } = 512;
    public int Steps { get; set; } = 20;
    public double Cfg { get; set; } = 8.0;
    public string SamplerName { get; set; } = "euler";
    public string Scheduler { get; set; } = "normal";
    public double Denoise { get; set; } = 1.0;
    public string CheckpointName { get; set; } = "v1-5-pruned-emaonly-fp16.safetensors";
    public ModelType ModelType { get; set; } = ModelType.SD15;
    public bool RemoveBackground { get; set; } = true;
    public int BatchSize { get; set; } = 1;
    
    public ImageGenerationRequest WithPrompt(string prompt)
    {
        PromptText = prompt;
        return this;
    }
    
    public ImageGenerationRequest WithNegativePrompt(string negativePrompt)
    {
        NegativePrompt = negativePrompt;
        return this;
    }
    
    public ImageGenerationRequest WithSeed(long seed)
    {
        Seed = seed;
        return this;
    }
    
    public ImageGenerationRequest WithSize(int width, int height)
    {
        Width = width;
        Height = height;
        return this;
    }
    
    public ImageGenerationRequest WithSteps(int steps)
    {
        Steps = steps;
        return this;
    }
    
    public ImageGenerationRequest WithCfg(double cfg)
    {
        Cfg = cfg;
        return this;
    }
    
    public ImageGenerationRequest WithSampler(string samplerName, string scheduler = "normal")
    {
        SamplerName = samplerName;
        Scheduler = scheduler;
        return this;
    }
    
    public ImageGenerationRequest WithCheckpoint(string checkpointName)
    {
        CheckpointName = checkpointName;
        return this;
    }
    
    public ImageGenerationRequest WithModelType(ModelType modelType)
    {
        ModelType = modelType;
        if (modelType == ModelType.SDXL)
        {
            Width = 1024;
            Height = 1024;
        }
        return this;
    }
    
    public ImageGenerationRequest WithBackgroundRemoval(bool remove)
    {
        RemoveBackground = remove;
        return this;
    }
    
    public ImageGenerationRequest WithBatchSize(int batchSize)
    {
        BatchSize = batchSize;
        return this;
    }
    
    public static ImageGenerationRequest SD15() => 
        new ImageGenerationRequest()
            .WithModelType(ModelType.SD15)
            .WithSize(512, 512);
    
    public static ImageGenerationRequest SDXL() =>
        new ImageGenerationRequest()
            .WithModelType(ModelType.SDXL)
            .WithSize(1024, 1024)
            .WithCheckpoint("SDXL\\sd_xl_base_1.0.safetensors");
    
    public static ImageGenerationRequest HighQuality() =>
        SDXL()
            .WithSteps(30)
            .WithCfg(7.5)
            .WithSampler("dpmpp_2m_sde", "karras");
}

public enum ModelType
{
    SD15,
    SDXL,
    Custom
}

public class ComfyUiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public const string BaseUrlEnvironmentVariable = "GAMEENGINE_COMFYUI_URL";
    public const string DefaultBaseUrl = "http://localhost:8000";

    public static string ResolveBaseUrl()
    {
        var configured = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configured) ? DefaultBaseUrl : configured;
    }

    public ComfyUiClient(string? baseUrl = null)
    {
        _baseUrl = (baseUrl ?? ResolveBaseUrl()).TrimEnd('/');
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    public async Task<byte[]> GenerateImageAsync(string promptText)
    {
        var request = new ImageGenerationRequest().WithPrompt(promptText);
        return await GenerateImageAsync(request);
    }
    
    public async Task<byte[]> GenerateImageAsync(ImageGenerationRequest request)
    {
        string promptId = await SubmitPromptAsync(request);
        string filename = await WaitForCompletionAsync(promptId);
        byte[] imageData = await DownloadImageAsync(filename);
        return imageData;
    }
    
    public async Task<Stream> GenerateImageStreamAsync(ImageGenerationRequest request)
    {
        string promptId = await SubmitPromptAsync(request);
        string filename = await WaitForCompletionAsync(promptId);
        Stream imageStream = await DownloadImageStreamAsync(filename);
        return imageStream;
    }

    private async Task<string> SubmitPromptAsync(ImageGenerationRequest request)
    {
        var workflow = BuildWorkflow(request);
        
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        string json = JsonSerializer.Serialize(new { prompt = workflow }, jsonOptions);
        
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_baseUrl}/prompt", content);
        response.EnsureSuccessStatusCode();

        string responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("prompt_id").GetString()
            ?? throw new InvalidOperationException("ComfyUI accepted the prompt but returned no prompt_id.");
    }

    private Dictionary<string, object> BuildWorkflow(ImageGenerationRequest request)
    {
        var workflow = new Dictionary<string, object>();
        long seed = request.Seed ?? new Random().NextInt64(100000000000000, 999999999999999);

        // 1. Checkpoint Loader - Load the model first
        workflow[NodeIds.CheckpointLoader] = new
        {
            inputs = new { ckpt_name = request.CheckpointName },
            class_type = "CheckpointLoaderSimple",
            _meta = new { title = "Load Checkpoint" }
        };

        // 2. Positive Prompt - Encode positive conditioning
        workflow[NodeIds.PositivePrompt] = new
        {
            inputs = new
            {
                text = request.PromptText,
                clip = NodeRef.To(NodeIds.CheckpointLoader, output: 1)
            },
            class_type = "CLIPTextEncode",
            _meta = new { title = "CLIP Text Encode (Prompt)" }
        };

        // 3. Negative Prompt - Encode negative conditioning
        workflow[NodeIds.NegativePrompt] = new
        {
            inputs = new
            {
                text = request.NegativePrompt,
                clip = NodeRef.To(NodeIds.CheckpointLoader, output: 1)
            },
            class_type = "CLIPTextEncode",
            _meta = new { title = "CLIP Text Encode (Prompt)" }
        };

        // 4. Empty Latent Image - Create the latent space
        workflow[NodeIds.EmptyLatent] = new
        {
            inputs = new
            {
                width = request.Width,
                height = request.Height,
                batch_size = request.BatchSize
            },
            class_type = "EmptyLatentImage",
            _meta = new { title = "Empty Latent Image" }
        };

        // 5. KSampler - Do the actual sampling/generation
        workflow[NodeIds.KSampler] = new
        {
            inputs = new
            {
                seed = seed,
                steps = request.Steps,
                cfg = request.Cfg,
                sampler_name = request.SamplerName,
                scheduler = request.Scheduler,
                denoise = request.Denoise,
                model = NodeRef.To(NodeIds.CheckpointLoader, output: 0),
                positive = NodeRef.To(NodeIds.PositivePrompt, output: 0),
                negative = NodeRef.To(NodeIds.NegativePrompt, output: 0),
                latent_image = NodeRef.To(NodeIds.EmptyLatent, output: 0)
            },
            class_type = "KSampler",
            _meta = new { title = "KSampler" }
        };

        // 6. VAE Decode - Convert latent to image
        workflow[NodeIds.VAEDecode] = new
        {
            inputs = new
            {
                samples = NodeRef.To(NodeIds.KSampler, output: 0),
                vae = NodeRef.To(NodeIds.CheckpointLoader, output: 2)
            },
            class_type = "VAEDecode",
            _meta = new { title = "VAE Decode" }
        };

        // 7. Background Removal - Optional post-processing
        if (request.RemoveBackground)
        {
            workflow[NodeIds.BackgroundRemoval] = new
            {
                inputs = new
                {
                    rem_mode = "BEN2",
                    image_output = "Save",
                    save_prefix = "ComfyUI",
                    torchscript_jit = false,
                    add_background = "none",
                    refine_foreground = false,
                    images = NodeRef.To(NodeIds.VAEDecode, output: 0)
                },
                class_type = "easy imageRemBg",
                _meta = new { title = "Image Remove Bg" }
            };
        }

        return workflow;
    }

    private async Task<string> WaitForCompletionAsync(string promptId, int maxAttempts = 60, int delayMs = 2000)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/history/{promptId}");
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty(promptId, out var promptData))
            {
                if (promptData.TryGetProperty("outputs", out var outputs))
                {
                    foreach (var output in outputs.EnumerateObject())
                    {
                        if (output.Value.TryGetProperty("images", out var images))
                        {
                            var firstImage = images.EnumerateArray().First();
                            return firstImage.GetProperty("filename").GetString()
                                ?? throw new InvalidOperationException("ComfyUI returned an image with no filename.");
                        }
                    }
                }
            }

            await Task.Delay(delayMs);
        }

        throw new TimeoutException($"Image generation timed out after {maxAttempts * delayMs / 1000} seconds");
    }

    private async Task<byte[]> DownloadImageAsync(string filename)
    {
        var response = await _httpClient.GetAsync(
            $"{_baseUrl}/view?filename={Uri.EscapeDataString(filename)}&subfolder=&type=output");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task<Stream> DownloadImageStreamAsync(string filename)
    {
        var response = await _httpClient.GetAsync(
            $"{_baseUrl}/view?filename={Uri.EscapeDataString(filename)}&subfolder=&type=output");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
