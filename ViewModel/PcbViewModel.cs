using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TracerAI.Model;


namespace TracerAI.ViewModel
{
    public class PcbViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        
        public string Title { get; set; } = "Редактор PCB";
        public double ZoomLevel { get; set; } = 1.0;

        

        public float Scale { get; set; } = 1.0f;
        public float OffsetX { get; set; } = 0.0f;
        public float OffsetY { get; set; } = 0.0f;
        public GridSettings GridSettings { get; set; } = new GridSettings();
        public Point2D SnappedWorldCursor { get; set; }




        public Point2D ScreenToWorld(Point2D screenPt)
        {
            float worldX = (screenPt.X - OffsetX) / Scale;
            float worldY = (screenPt.Y - OffsetY) / Scale;
            return new Point2D(worldX, worldY);
        }

        public Point2D WorldToScreen(Point2D worldPt)
        {
            float screenX = (worldPt.X * Scale) + OffsetX;
            float screenY = (worldPt.Y * Scale) + OffsetY;
            return new Point2D(screenX, screenY);
        }

        public Point2D Snap(Point2D worldPt)
        {
            if (!GridSettings.IsSnapEnabled || GridSettings.Step <= 0)
                return worldPt;

            float snappedX = (float)Math.Round(worldPt.X / GridSettings.Step) * GridSettings.Step;
            float snappedY = (float)Math.Round(worldPt.Y / GridSettings.Step) * GridSettings.Step;
            return new Point2D(snappedX, snappedY);
        }

        public void UpdateCursorPosition(Point2D screenPos)
        {
            var rawWorld = ScreenToWorld(screenPos);
            SnappedWorldCursor = Snap(rawWorld);
        }

        public void Zoom(float zoomFactor, Point2D screenCenter)
        {
            var worldBefore = ScreenToWorld(screenCenter);
            float newScale = Scale * zoomFactor;
            Scale = Math.Max(0.05f, Math.Min(newScale, 50.0f));

            OffsetX = screenCenter.X - (worldBefore.X * Scale);
            OffsetY = screenCenter.Y - (worldBefore.Y * Scale);
        }

        public void Pan(Point2D delta)
        {
            OffsetX += delta.X;
            OffsetY += delta.Y;
        }
    }
}
    
