using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using GameEngine.Editor.ImageGen;

namespace GameEngine.Editor.Controls;

public partial class ImageGeneratorControl : UserControl
{
    private readonly ComfyUiClient _client;
    private byte[]? _lastGeneratedImage;

    public ImageGeneratorControl()
    {
        InitializeComponent();

        _client = new ComfyUiClient();

        // Wire up events
        GenerateButton.Click += OnGenerateButtonClick;
        SaveButton.Click += OnSaveButtonClick;
        RandomSeedButton.Click += OnRandomSeedButtonClick;
        ModelTypeComboBox.SelectionChanged += OnModelTypeChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        // Get control references
        PromptTextBox = this.FindControl<TextBox>("PromptTextBox");
        NegativePromptTextBox = this.FindControl<TextBox>("NegativePromptTextBox");
        ModelTypeComboBox = this.FindControl<ComboBox>("ModelTypeComboBox");
        CheckpointTextBox = this.FindControl<TextBox>("CheckpointTextBox");
        RemoveBackgroundCheckBox = this.FindControl<CheckBox>("RemoveBackgroundCheckBox");
        WidthNumeric = this.FindControl<NumericUpDown>("WidthNumeric");
        HeightNumeric = this.FindControl<NumericUpDown>("HeightNumeric");
        StepsSlider = this.FindControl<Slider>("StepsSlider");
        CfgSlider = this.FindControl<Slider>("CfgSlider");
        BatchSizeNumeric = this.FindControl<NumericUpDown>("BatchSizeNumeric");
        SamplerComboBox = this.FindControl<ComboBox>("SamplerComboBox");
        SchedulerComboBox = this.FindControl<ComboBox>("SchedulerComboBox");
        SeedTextBox = this.FindControl<TextBox>("SeedTextBox");
        RandomSeedButton = this.FindControl<Button>("RandomSeedButton");
        GeneratedImage = this.FindControl<Image>("GeneratedImage");
        StatusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");
        ProgressBar = this.FindControl<ProgressBar>("ProgressBar");
        GenerateButton = this.FindControl<Button>("GenerateButton");
        SaveButton = this.FindControl<Button>("SaveButton");

        if (PromptTextBox != null)
            PromptTextBox.Text =
                "A small, hyper-realistic floating glass marble containing a lush green miniature world inside. Forests, mossy hills, tiny rivers, and glowing plants, all enclosed within the translucent sphere. Beautiful lighting, soft reflections, shallow depth of field, macro photography, photorealistic, high detail, 8k";

        if (SamplerComboBox != null)
            SamplerComboBox.SelectedIndex = 2;
    }
    

    private void OnModelTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModelTypeComboBox.SelectedIndex == 0) // SD 1.5
        {
            WidthNumeric.Value = 512;
            HeightNumeric.Value = 512;
            CheckpointTextBox.Text = "v1-5-pruned-emaonly-fp16.safetensors";
        }
        else if (ModelTypeComboBox.SelectedIndex == 1) // SDXL
        {
            WidthNumeric.Value = 1024;
            HeightNumeric.Value = 1024;
            CheckpointTextBox.Text = "SDXL\\sd_xl_base_1.0.safetensors";
        }
    }

    private void OnRandomSeedButtonClick(object? sender, RoutedEventArgs e)
    {
        var random = new Random();
        long seed = random.NextInt64(100000000000000, 999999999999999);
        SeedTextBox.Text = seed.ToString();
    }

    private async void OnGenerateButtonClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PromptTextBox.Text))
        {
            ShowStatus("Please enter a prompt", isError: true);
            return;
        }

        try
        {
            SetGenerating(true);
            ShowStatus("Generating image...");

            var request = BuildRequest();
            _lastGeneratedImage = await _client.GenerateImageAsync(request);

            // Display the image
            using var stream = new MemoryStream(_lastGeneratedImage);
            GeneratedImage.Source = new Bitmap(stream);

            ShowStatus("Image generated successfully!");
            await Task.Delay(3000);
            HideStatus();
        }
        catch (Exception ex)
        {
            ShowStatus($"Error: {ex.Message}", isError: true);
        }
        finally
        {
            SetGenerating(false);
        }
    }

    private async void OnSaveButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_lastGeneratedImage == null) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Generated Image",
                SuggestedFileName = $"generated_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                await stream.WriteAsync(_lastGeneratedImage);
                ShowStatus($"Image saved to {file.Name}");
                await Task.Delay(3000);
                HideStatus();
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error saving image: {ex.Message}", isError: true);
        }
    }

    private ImageGenerationRequest BuildRequest()
    {
        var request = new ImageGenerationRequest()
            .WithPrompt(PromptTextBox.Text ?? "")
            .WithNegativePrompt(NegativePromptTextBox.Text ?? "text, watermark")
            .WithSize((int)(WidthNumeric.Value ?? 512), (int)(HeightNumeric.Value ?? 512))
            .WithSteps((int)StepsSlider.Value)
            .WithCfg(CfgSlider.Value)
            .WithBatchSize((int)(BatchSizeNumeric.Value ?? 1))
            .WithCheckpoint(CheckpointTextBox.Text ?? "v1-5-pruned-emaonly-fp16.safetensors")
            .WithBackgroundRemoval(RemoveBackgroundCheckBox.IsChecked ?? true);

        // Set model type
        request.ModelType = ModelTypeComboBox.SelectedIndex switch
        {
            0 => ModelType.SD15,
            1 => ModelType.SDXL,
            _ => ModelType.Custom
        };

        // Set sampler
        var sampler = (SamplerComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "euler";
        var scheduler = (SchedulerComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "normal";
        request.WithSampler(sampler, scheduler);

        // Set seed if provided
        if (!string.IsNullOrWhiteSpace(SeedTextBox.Text) && long.TryParse(SeedTextBox.Text, out long seed))
        {
            request.WithSeed(seed);
        }

        return request;
    }

    private void SetGenerating(bool isGenerating)
    {
        GenerateButton.IsEnabled = !isGenerating;
        SaveButton.IsEnabled = !isGenerating && _lastGeneratedImage != null;
        ProgressBar.IsVisible = isGenerating;
    }

    private void ShowStatus(string message, bool isError = false)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = isError 
            ? Avalonia.Media.Brushes.Red 
            : Avalonia.Media.Brushes.LightBlue;
        StatusTextBlock.IsVisible = true;
    }

    private void HideStatus()
    {
        StatusTextBlock.IsVisible = false;
    }

    // Property to set the ComfyUI server URL
    public void SetServerUrl(string url)
    {
        // You would need to recreate the client with the new URL
        // For now, this is just a placeholder
    }
}