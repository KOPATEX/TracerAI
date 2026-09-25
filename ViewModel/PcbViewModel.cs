using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using DevExpress.Mvvm;
using TracerAI.Model;

namespace TracerAI.ViewModel
{
    // Наследуемся от ViewModelBase (DevExpress.Mvvm) для работы RaisePropertyChanged() и команд
    public class PcbViewModel : ViewModelBase
    {
        public string Title { get; set; } = "Редактор PCB";

        // --- Настройки камеры и сетки ---
        private float _scale = 1.0f;
        public float Scale
        {
            get => _scale;
            set { _scale = value; RaisePropertyChanged(); }
        }

        private float _offsetX = 0.0f;
        public float OffsetX
        {
            get => _offsetX;
            set { _offsetX = value; RaisePropertyChanged(); }
        }

        private float _offsetY = 0.0f;
        public float OffsetY
        {
            get => _offsetY;
            set { _offsetY = value; RaisePropertyChanged(); }
        }

        public GridSettings GridSettings { get; set; } = new GridSettings();

        private Point2D _snappedWorldCursor;
        public Point2D SnappedWorldCursor
        {
            get => _snappedWorldCursor;
            set { _snappedWorldCursor = value; RaisePropertyChanged(); }
        }

        // --- Преобразование координат ---
        public Point2D ScreenToWorld(Point2D screenPt)
        {
            float worldX = (screenPt.X - OffsetX) / Scale;
            float worldY = (screenPt.Y - OffsetY) / -Scale;
            return new Point2D(worldX, worldY);
        }

        public Point2D WorldToScreen(Point2D worldPt)
        {
            float screenX = (worldPt.X * Scale) + OffsetX;
            float screenY = -(worldPt.Y * Scale) + OffsetY;
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
            OffsetY = screenCenter.Y + (worldBefore.Y * Scale);
        }

        public void Pan(Point2D delta)
        {
            OffsetX += delta.X;
            OffsetY += delta.Y;
        }

        // --- Создание элементов платы ---

        // 1. Текущий выбранный инструмент
        private ToolMode _currentTool = ToolMode.None;
        public ToolMode CurrentTool
        {
            get => _currentTool;
            set
            {
                _currentTool = value;
                RaisePropertyChanged();
                CancelDrawing(); // Сбрасываем незавершенное рисование при смене инструмента
            }
        }

        // 2. Коллекция созданных элементов
        public ObservableCollection<PcbElement> Elements { get; } = new ObservableCollection<PcbElement>();

        // 3. Состояние процесса рисования
        private bool _isDrawing;
        public bool IsDrawing
        {
            get => _isDrawing;
            set { _isDrawing = value; RaisePropertyChanged(); }
        }

        private Point2D _drawingStartPoint;
        public Point2D DrawingStartPoint
        {
            get => _drawingStartPoint;
            set { _drawingStartPoint = value; RaisePropertyChanged(); }
        }

        private Point2D _drawingPreviewPoint;
        public Point2D DrawingPreviewPoint
        {
            get => _drawingPreviewPoint;
            set { _drawingPreviewPoint = value; RaisePropertyChanged(); }
        }

        // 4. Команда выбора инструмента из UI (Меню / Кнопки)
        public ICommand SelectToolCommand { get; }

        public PcbViewModel()
        {
            SelectToolCommand = new DelegateCommand<ToolMode>(tool =>
            {
                CurrentTool = tool;
            });
        }

        // Фиксация нарисованного элемента в коллекции
        public void CommitCurrentElement()
        {
            if (!IsDrawing) return;

            switch (CurrentTool)
            {
                case ToolMode.Rectangle:
                    Elements.Add(new PcbRectangle
                    {
                        Start = DrawingStartPoint,
                        End = DrawingPreviewPoint
                    });
                    break;

                case ToolMode.Track:
                    Elements.Add(new PcbTrack
                    {
                        Start = DrawingStartPoint,
                        End = DrawingPreviewPoint,
                        Width = 0.5f
                    });
                    break;

                case ToolMode.Pad:
                    Elements.Add(new PcbPad
                    {
                        Position = DrawingPreviewPoint,
                        Size = new Point2D(1.5f, 1.5f),
                        HoleSize = 0.8f
                    });
                    break;

                case ToolMode.Circle:
                    float radius = (float)Math.Sqrt(
                        Math.Pow(DrawingPreviewPoint.X - DrawingStartPoint.X, 2) +
                        Math.Pow(DrawingPreviewPoint.Y - DrawingStartPoint.Y, 2)
                    );
                    Elements.Add(new PcbCircle
                    {
                        Center = DrawingStartPoint,
                        Radius = radius
                    });
                    break;

                case ToolMode.None:
                    break;
            }

            IsDrawing = false;
        }

        // Отмена текущего процесса построения (по Esc или ПКМ)
        public void CancelDrawing()
        {
            IsDrawing = false;
        }

        private float _currentTrackWidth = 0.5f;
        public float CurrentTrackWidth
        {
            get => _currentTrackWidth;
            set { _currentTrackWidth = value; RaisePropertyChanged(); }
        }

        // Метод привязки к углам 0°, 45°, 90°
        public Point2D SnapTo45Degrees(Point2D start, Point2D target)
        {
            float dx = target.X - start.X;
            float dy = target.Y - start.Y;

            float absDx = Math.Abs(dx);
            float absDy = Math.Abs(dy);

            // 1. Горизонтальная линия
            if (absDx > 2 * absDy)
                return new Point2D(target.X, start.Y);

            // 2. Вертикальная линия
            if (absDy > 2 * absDx)
                return new Point2D(start.X, target.Y);

            // 3. Диагональ 45 градусов
            float minDist = Math.Max(absDx, absDy);
            return new Point2D(start.X + Math.Sign(dx) * minDist, start.Y + Math.Sign(dy) * minDist);
        }
    }
}