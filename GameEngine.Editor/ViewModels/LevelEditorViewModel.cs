using Avalonia.Media;
using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive;

namespace GameEngine.Editor.ViewModels
{
    public class LevelEditorViewModel : ViewModelBase
    {
        public ObservableCollection<TileViewModel> Tiles { get; }
        public int GridColumns { get; } = 50;
        public int GridRows { get; } = 50;

        public double TileSize => 32;

        public double TotalWidth => GridColumns * TileSize;
        public double TotalHeight => GridRows * TileSize;


        public LevelEditorViewModel()
        {
            Tiles = new ObservableCollection<TileViewModel>();
            for (int i = 0; i < GridColumns * GridRows; i++)
            {
                Tiles.Add(new TileViewModel { Color = Brushes.Black, Content = "das" });
                this.RaisePropertyChanged(nameof(Tiles));
            }
        }

        private double _zoomLevel = 1.0;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set => this.RaiseAndSetIfChanged(ref _zoomLevel, value);
        }

    }

    public class TileViewModel : ReactiveObject
    {
        private IBrush _color;
        private string _content;
        private Bitmap _image = new Random().Next(1, 100) >1 ? null : new Bitmap("C:\\Users\\Josh\\source\\repos\\GameEngine\\GameEngine.Demo\\assets\\images\\jeep.png");

        public IBrush Color
        {
            get => _color;
            set => this.RaiseAndSetIfChanged(ref _color, value);
        }

        public string Content
        {
            get => _content;
            set => this.RaiseAndSetIfChanged(ref _content, value);
        }

        public int ZIndex = new Random().Next(1, 100);

        public Bitmap Image
        {
            get => _image;
            set => this.RaiseAndSetIfChanged(ref _image, value);
        }

        public ReactiveCommand<Unit, Unit> PlaceTileCommand { get; }

        public TileViewModel()
        {
            PlaceTileCommand = ReactiveCommand.Create(PlaceTile);
        }

        private void PlaceTile()
        {
            // Logic to place a tile, e.g., change color or content
            Color = Brushes.Gray; // Example: Change color to indicate a tile is placed
            Content = "Tile"; // Example: Set content to indicate a tile is placed
        }
    }
}
