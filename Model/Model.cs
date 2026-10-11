using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;


namespace TracerAI.Model
{
    public class PcbLayer : INotifyPropertyChanged
    {
        private string _name;
        private SKColor _color;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public SKColor Color
        {
            get => _color;
            set
            {
                _color = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WpfColor));
                OnPropertyChanged(nameof(HexColor));
            }
        }

        // Цвет для WPF (привязки к Background/Fill)
        public System.Windows.Media.Color WpfColor
        {
            get => System.Windows.Media.Color.FromArgb(Color.Alpha, Color.Red, Color.Green, Color.Blue);
            set => Color = new SKColor(value.R, value.G, value.B, value.A);
        }

        // В файле PcbLayer.cs
        public string HexColor
        {
            get => $"#{Color.Red:X2}{Color.Green:X2}{Color.Blue:X2}";
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;

                string hex = value.Trim();
                if (!hex.StartsWith("#"))
                {
                    hex = "#" + hex;
                }

                if (SKColor.TryParse(hex, out var parsedColor))
                {
                    Color = parsedColor; // При изменении Color автоматически вызовутся OnPropertyChanged
                }
            }
        }

        public PcbLayer() { }

        public PcbLayer(string name, SKColor color)
        {
            Name = name;
            Color = color;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString() => Name;
    }


    
}

