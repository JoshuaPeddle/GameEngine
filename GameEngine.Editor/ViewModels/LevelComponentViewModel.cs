using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using ReactiveUI;

namespace GameEngine.Editor.ViewModels
{
    // Helper view models for level editing
    public class LevelEntityViewModel : ReactiveObject
    {
        private string _tag = string.Empty;
        public string Tag
        {
            get => _tag;
            set => this.RaiseAndSetIfChanged(ref _tag, value);
        }

        public ObservableCollection<LevelComponentViewModel> Components { get; } = new();
    }

    public class LevelComponentViewModel : ReactiveObject
    {
        private bool _suppressSync;

        private string _type = string.Empty;
        public string Type
        {
            get => _type;
            set
            {
                var changed = _type != value;
                this.RaiseAndSetIfChanged(ref _type, value);
                if (changed)
                {
                    RaiseTypeFlags();
                    if (!_suppressSync)
                    {
                        ApplyTypeWithDefaults(_type);
                    }
                }
            }
        }

        private string _rawJson = string.Empty;
        public string RawJson
        {
            get => _rawJson;
            set
            {
                this.RaiseAndSetIfChanged(ref _rawJson, value);
                if (!_suppressSync)
                {
                    TryParseFromRawJson();
                }
            }
        }

        // Typed properties for known components
        private double _posX;
        public double PosX { get => _posX; set { this.RaiseAndSetIfChanged(ref _posX, value); UpdateRawJson(); } }
        private double _posY;
        public double PosY { get => _posY; set { this.RaiseAndSetIfChanged(ref _posY, value); UpdateRawJson(); } }

        private double _velX;
        public double VelX { get => _velX; set { this.RaiseAndSetIfChanged(ref _velX, value); UpdateRawJson(); } }
        private double _velY;
        public double VelY { get => _velY; set { this.RaiseAndSetIfChanged(ref _velY, value); UpdateRawJson(); } }

        private double _scaleX = 1;
        public double ScaleX { get => _scaleX; set { this.RaiseAndSetIfChanged(ref _scaleX, value); UpdateRawJson(); } }
        private double _scaleY = 1;
        public double ScaleY { get => _scaleY; set { this.RaiseAndSetIfChanged(ref _scaleY, value); UpdateRawJson(); } }

        private double _rotation;
        public double Rotation { get => _rotation; set { this.RaiseAndSetIfChanged(ref _rotation, value); UpdateRawJson(); } }

        private int _layer;
        public int Layer { get => _layer; set { this.RaiseAndSetIfChanged(ref _layer, value); UpdateRawJson(); } }

        private string _animationName = string.Empty;
        public string AnimationName { get => _animationName; set { this.RaiseAndSetIfChanged(ref _animationName, value); UpdateRawJson(); } }

        private double _width;
        public double Width { get => _width; set { this.RaiseAndSetIfChanged(ref _width, value); UpdateRawJson(); } }
        private double _height;
        public double Height { get => _height; set { this.RaiseAndSetIfChanged(ref _height, value); UpdateRawJson(); } }
        private bool _blockVision = false;
        public bool BlockVision { get => _blockVision; set { this.RaiseAndSetIfChanged(ref _blockVision, value); UpdateRawJson(); } }
        private bool _blockMovement = true;
        public bool BlockMovement { get => _blockMovement; set { this.RaiseAndSetIfChanged(ref _blockMovement, value); UpdateRawJson(); } }

        private double _speed;
        public double Speed { get => _speed; set { this.RaiseAndSetIfChanged(ref _speed, value); UpdateRawJson(); } }
        private double _maxSpeed;
        public double MaxSpeed { get => _maxSpeed; set { this.RaiseAndSetIfChanged(ref _maxSpeed, value); UpdateRawJson(); } }

        // Convenience flags for XAML
        public bool IsTransform => Type == "CTransform";
        public bool IsAnimation => Type == "CAnimation";
        public bool IsBoundingBox => Type == "CBoundingBox";
        public bool IsInput => Type == "CInput";
        public bool IsMovement => Type == "CMovement";

        public void ApplyTypeWithDefaults(string type)
        {
            _suppressSync = true;
            try
            {
                // set backing field directly to avoid recursion
                _type = type;
                this.RaisePropertyChanged(nameof(Type));
                RaiseTypeFlags();
                switch (type)
                {
                    case "CTransform":
                        _posX = 0; _posY = 0; _velX = 0; _velY = 0; _scaleX = 1; _scaleY = 1; _rotation = 0; _layer = 0;
                        break;
                    case "CAnimation":
                        _animationName = "Ball";
                        break;
                    case "CBoundingBox":
                        _width = 32; _height = 32; _blockVision = false; _blockMovement = true;
                        break;
                    case "CInput":
                        break;
                    case "CMovement":
                        _speed = 100; _maxSpeed = 120;
                        break;
                }
                // Raise property changes for edited fields
                this.RaisePropertyChanged(nameof(PosX));
                this.RaisePropertyChanged(nameof(PosY));
                this.RaisePropertyChanged(nameof(VelX));
                this.RaisePropertyChanged(nameof(VelY));
                this.RaisePropertyChanged(nameof(ScaleX));
                this.RaisePropertyChanged(nameof(ScaleY));
                this.RaisePropertyChanged(nameof(Rotation));
                this.RaisePropertyChanged(nameof(Layer));
                this.RaisePropertyChanged(nameof(AnimationName));
                this.RaisePropertyChanged(nameof(Width));
                this.RaisePropertyChanged(nameof(Height));
                this.RaisePropertyChanged(nameof(BlockVision));
                this.RaisePropertyChanged(nameof(BlockMovement));
                this.RaisePropertyChanged(nameof(Speed));
                this.RaisePropertyChanged(nameof(MaxSpeed));
                UpdateRawJson();
            }
            finally
            {
                _suppressSync = false;
            }
        }

        private void UpdateRawJson()
        {
            _suppressSync = true;
            try
            {
                using var ms = new MemoryStream();
                using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteString("type", Type);

                    switch (Type)
                    {
                        case "CTransform":
                            writer.WritePropertyName("position");
                            writer.WriteStartObject();
                            writer.WriteNumber("x", PosX);
                            writer.WriteNumber("y", PosY);
                            writer.WriteEndObject();

                            writer.WritePropertyName("velocity");
                            writer.WriteStartObject();
                            writer.WriteNumber("x", VelX);
                            writer.WriteNumber("y", VelY);
                            writer.WriteEndObject();

                            writer.WritePropertyName("scale");
                            writer.WriteStartObject();
                            writer.WriteNumber("x", ScaleX);
                            writer.WriteNumber("y", ScaleY);
                            writer.WriteEndObject();

                            writer.WriteNumber("rotation", Rotation);
                            writer.WriteNumber("layer", Layer);
                            break;
                        case "CAnimation":
                            writer.WriteString("animationName", AnimationName);
                            break;
                        case "CBoundingBox":
                            writer.WritePropertyName("size");
                            writer.WriteStartObject();
                            writer.WriteNumber("x", Width);
                            writer.WriteNumber("y", Height);
                            writer.WriteEndObject();
                            writer.WriteBoolean("blockVision", BlockVision);
                            writer.WriteBoolean("blockMovement", BlockMovement);
                            break;
                        case "CInput":
                            // no properties
                            break;
                        case "CMovement":
                            writer.WriteNumber("speed", Speed);
                            writer.WriteNumber("maxSpeed", MaxSpeed);
                            break;
                    }

                    writer.WriteEndObject();
                }
                RawJson = Encoding.UTF8.GetString(ms.ToArray());
            }
            finally
            {
                _suppressSync = false;
            }
        }

        private void TryParseFromRawJson()
        {
            try
            {
                using var doc = JsonDocument.Parse(RawJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var typeProp))
                {
                    var t = typeProp.GetString();
                    if (!string.IsNullOrWhiteSpace(t) && t != Type)
                    {
                        _suppressSync = true;
                        try { _type = t!; this.RaisePropertyChanged(nameof(Type)); RaiseTypeFlags(); } finally { _suppressSync = false; }
                    }
                }

                switch (Type)
                {
                    case "CTransform":
                        if (root.TryGetProperty("position", out var pos))
                        {
                            PosX = pos.TryGetProperty("x", out var x) ? x.GetDouble() : 0;
                            PosY = pos.TryGetProperty("y", out var y) ? y.GetDouble() : 0;
                        }
                        if (root.TryGetProperty("velocity", out var vel))
                        {
                            VelX = vel.TryGetProperty("x", out var vx) ? vx.GetDouble() : 0;
                            VelY = vel.TryGetProperty("y", out var vy) ? vy.GetDouble() : 0;
                        }
                        if (root.TryGetProperty("scale", out var scale))
                        {
                            ScaleX = scale.TryGetProperty("x", out var sx) ? sx.GetDouble() : 1;
                            ScaleY = scale.TryGetProperty("y", out var sy) ? sy.GetDouble() : 1;
                        }
                        Rotation = root.TryGetProperty("rotation", out var rot) ? rot.GetDouble() : 0;
                        Layer = root.TryGetProperty("layer", out var lay) ? lay.GetInt32() : 0;
                        break;
                    case "CAnimation":
                        AnimationName = root.TryGetProperty("animationName", out var anim) ? anim.GetString() ?? string.Empty : string.Empty;
                        break;
                    case "CBoundingBox":
                        if (root.TryGetProperty("size", out var size))
                        {
                            Width = size.TryGetProperty("x", out var w) ? w.GetDouble() : 0;
                            Height = size.TryGetProperty("y", out var h) ? h.GetDouble() : 0;
                        }
                        BlockVision = root.TryGetProperty("blockVision", out var bv) && bv.GetBoolean();
                        BlockMovement = root.TryGetProperty("blockMovement", out var bm) ? bm.GetBoolean() : true;
                        break;
                    case "CInput":
                        break;
                    case "CMovement":
                        Speed = root.TryGetProperty("speed", out var sp) ? sp.GetDouble() : 0;
                        MaxSpeed = root.TryGetProperty("maxSpeed", out var ms) ? ms.GetDouble() : 0;
                        break;
                }
            }
            catch
            {
                // Ignore parse errors; keep current typed fields
            }
        }

        private void RaiseTypeFlags()
        {
            this.RaisePropertyChanged(nameof(IsTransform));
            this.RaisePropertyChanged(nameof(IsAnimation));
            this.RaisePropertyChanged(nameof(IsBoundingBox));
            this.RaisePropertyChanged(nameof(IsInput));
            this.RaisePropertyChanged(nameof(IsMovement));
        }
    }
}
