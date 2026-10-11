using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using DevExpress.Mvvm;
using SkiaSharp;
using TracerAI.Model;

namespace TracerAI.ViewModel
{
    // Наследуемся от ViewModelBase (DevExpress.Mvvm) для работы RaisePropertyChanged() и команд
    public class PcbViewModel : ViewModelBase
    {
        public string Title { get; set; } = "Редактор PCB";
        public ICommand DeleteSelectedCommand { get; }

        public ObservableCollection<PcbElement> MultiSelectedElements { get; } = new ObservableCollection<PcbElement>();

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


        public Point2DCustom GridSettings { get; set; } = new Point2DCustom();

        private Point2D _snappedWorldCursor;
        public Point2D SnappedWorldCursor
        {
            get => _snappedWorldCursor;
            set { _snappedWorldCursor = value; RaisePropertyChanged(); }
        }

        public Action RequestInvalidate { get; set; }
        public ICommand SelectConnectedCommand { get; }

        private List<Point2D> _boardContour = new List<Point2D>();

        public List<Point2D> BoardContour
        {
            get => _boardContour;
            set { _boardContour = value; RaisePropertyChanged(); }
        }

        
        public ICommand DefineBoardShapeCommand { get; }


        //Преобразование координат
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
            CurrentLayer = AvailableLayers[0]; // По умолчанию Top Layer

            DeleteSelectedCommand = new DelegateCommand(() =>
            {
                // Если есть группа выбранных элементов (цепочка по Tab или одиночный элемент)
                if (MultiSelectedElements != null && MultiSelectedElements.Count > 0)
                {
                    // Создаем копию коллекции (.ToList()), чтобы безопасно удалять элементы в цикле
                    foreach (var el in MultiSelectedElements.ToList())
                    {
                        Elements.Remove(el);
                    }
                    MultiSelectedElements.Clear();
                }
                else if (SelectedElement != null)
                {
                    Elements.Remove(SelectedElement);
                }

                SelectedElement = null;
                RequestInvalidate?.Invoke(); // 🟢 Принудительно обновляем холст после удаления
            }, () => SelectedElement != null || (MultiSelectedElements != null && MultiSelectedElements.Count > 0));

            SelectConnectedCommand = new DelegateCommand(() =>
            {
                if (SelectedElement != null)
                {
                    SelectConnectedElements();
                    System.Diagnostics.Debug.WriteLine(
                        $"[Tab] Выделено элементов цепи: {MultiSelectedElements.Count}"
                    );
                    RequestInvalidate?.Invoke();
                }
            }, () => SelectedElement != null);

            DefineBoardShapeCommand = new DelegateCommand(() =>
            {
                DefineBoardShapeFromSelected();
            }, () => MultiSelectedElements != null && MultiSelectedElements.Count > 0);

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
                        End = DrawingPreviewPoint,
                        Layer = CurrentLayer ?? AvailableLayers[0]
                    });
                    CurrentTool = ToolMode.Edit;
                    break;

                case ToolMode.Track:
                    Elements.Add(new PcbTrack
                    {
                        Start = DrawingStartPoint,
                        End = DrawingPreviewPoint,
                        Width = CurrentTrackWidth, //  Сохраняем введенную ширину
                        Layer = CurrentLayer ?? AvailableLayers[0]
                    });
                    CurrentTool = ToolMode.Edit;
                    break;

                case ToolMode.Pad:
                    Elements.Add(new PcbPad
                    {
                        Position = DrawingPreviewPoint,
                        Size = new Point2D(1.5f, 1.5f),
                        HoleSize = 0.8f,
                        Layer = CurrentLayer ?? AvailableLayers[0]
                    });
                    CurrentTool = ToolMode.Edit;
                    break;

                case ToolMode.Circle:
                    float radius = (float)Math.Sqrt(
                        Math.Pow(DrawingPreviewPoint.X - DrawingStartPoint.X, 2) +
                        Math.Pow(DrawingPreviewPoint.Y - DrawingStartPoint.Y, 2)
                    );
                    Elements.Add(new PcbCircle
                    {
                        Center = DrawingStartPoint,
                        Radius = radius,
                        Layer = CurrentLayer ?? AvailableLayers[0]
                    });
                    CurrentTool = ToolMode.Edit;
                    break;
            }

            var newElement = Elements.LastOrDefault();
            if (newElement != null)
            {
                SelectedElement = newElement; // 🟢 Автоматически выделяем созданный элемент в инспекторе
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




        private PcbElement _hoveredElement;
        public PcbElement HoveredElement
        {
            get => _hoveredElement;
            set { _hoveredElement = value; RaisePropertyChanged(); }
        }

        // Допуск в экранных пикселях (независимо от зума)
        private const float HitTolerancePixels = 4f;

        public void UpdateHoveredElement(Point2D screenPos)
        {
            var worldPos = ScreenToWorld(screenPos);

            // Переводим допуск из пикселей в мировые единицы с учётом текущего масштаба
            float toleranceWorld = HitTolerancePixels / Scale;

            PcbElement found = null;

            foreach (var element in Elements)
            {
                if (element is PcbTrack track)
                {
                    float dist = DistancePointToSegment(worldPos, track.Start, track.End);
                    float halfWidth = track.Width*2;

                    if (dist <= halfWidth + toleranceWorld)
                    {
                        found = track;
                        break;
                    }
                }
                // сюда же можно добавить hit-test для PcbPad, PcbCircle и т.д.
            }

            HoveredElement = found;
        }

        // Расстояние от точки до отрезка (в тех же единицах, что и координаты)
        private static float DistancePointToSegment(Point2D p, Point2D a, Point2D b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float lengthSquared = dx * dx + dy * dy;

            if (lengthSquared == 0)
                return Distance(p, a);

            float t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared;
            t = Math.Max(0f, Math.Min(1f, t));

            var closest = new Point2D(a.X + t * dx, a.Y + t * dy);
            return Distance(p, closest);
        }

        private static float Distance(Point2D p1, Point2D p2)
        {
            float dx = p1.X - p2.X;
            float dy = p1.Y - p2.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private PcbElement _selectedElement;
        public PcbElement SelectedElement
        {
            get => _selectedElement;
            set
            {
                if (_selectedElement != value)
                {
                    _selectedElement = value;
                    RaisePropertyChanged(nameof(SelectedElement));

                    MultiSelectedElements.Clear();
                    if (_selectedElement != null)
                    {
                        MultiSelectedElements.Add(_selectedElement);
                    }
                }
            }
        }


        // Метод поиска и выделения элемента
        public void SelectElementAt(Point2D worldPoint)
        {
            // Обходим с конца (Z-order: сверху вниз)
            for (int i = Elements.Count - 1; i >= 0; i--)
            {
                if (Elements[i].IsHit(worldPoint))
                {
                    SelectedElement = Elements[i];
                    return;
                }
            }


            // Если кликнули в пустое место — снимаем выделение
            SelectedElement = null;

        }



        public ObservableCollection<PcbLayer> AvailableLayers { get; set; } = new ObservableCollection<PcbLayer>
        {
            new PcbLayer("Top Layer", SKColors.Red),
            new PcbLayer("Bottom Layer", SKColors.Blue),
            new PcbLayer("SilkScreen Top", SKColors.Yellow),
            new PcbLayer("Edge Cuts", SKColors.Green)
        };

        // Активный слой для новых элементов
        public PcbLayer CurrentLayer { get; set; }

        

        // Пример при создании дорожки:
        public void AddTrack(Point2D start, Point2D end)
        {
            var track = new PcbTrack
            {
                Start = start,
                End = end,
                Width = CurrentTrackWidth,
                Layer = CurrentLayer // Назначаем активный слой
            };
            Elements.Add(track);
        }


        public void SelectConnectedElements()
        {
            if (SelectedElement == null) return;

            var visited = new HashSet<PcbElement>();
            var queue = new Queue<PcbElement>();

            queue.Enqueue(SelectedElement);
            visited.Add(SelectedElement);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var element in Elements)
                {
                    if (!visited.Contains(element))
                    {
                        if (AreElementsConnected(current, element))
                        {
                            visited.Add(element);
                            queue.Enqueue(element);
                        }
                    }
                }
            }

            MultiSelectedElements.Clear();
            foreach (var el in visited)
            {
                MultiSelectedElements.Add(el);
            }
        }

        // Проверка: соприкасаются ли два элемента
        private bool AreElementsConnected(PcbElement e1, PcbElement e2)
        {
            float tolerance = 1.0f; // Увеличенный допуск для надежного определения стыков

            float Dist(Point2D p1, Point2D p2) =>
                (p1 == null || p2 == null) ? float.MaxValue : (float)Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));

            float DistPointToSegment(Point2D p, Point2D a, Point2D b)
            {
                float dx = b.X - a.X;
                float dy = b.Y - a.Y;
                float l2 = dx * dx + dy * dy;
                if (l2 == 0) return Dist(p, a);

                float t = Math.Max(0f, Math.Min(1f, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2));
                var proj = new Point2D(a.X + t * dx, a.Y + t * dy);
                return Dist(p, proj);
            }

            // 1. Трек и Трек
            if (e1 is PcbTrack t1 && e2 is PcbTrack t2)
            {
                return Dist(t1.Start, t2.Start) <= tolerance ||
                       Dist(t1.Start, t2.End) <= tolerance ||
                       Dist(t1.End, t2.Start) <= tolerance ||
                       Dist(t1.End, t2.End) <= tolerance ||
                       DistPointToSegment(t2.Start, t1.Start, t1.End) <= t1.Width / 2f + tolerance ||
                       DistPointToSegment(t2.End, t1.Start, t1.End) <= t1.Width / 2f + tolerance ||
                       DistPointToSegment(t1.Start, t2.Start, t2.End) <= t2.Width / 2f + tolerance ||
                       DistPointToSegment(t1.End, t2.Start, t2.End) <= t2.Width / 2f + tolerance;
            }

            // 2. Трек и Пад
            if (e1 is PcbTrack trackObj && e2 is PcbPad padObj)
            {
                float padRadius = padObj.Size.X / 2f;
                return Dist(trackObj.Start, padObj.Position) <= tolerance ||
                       Dist(trackObj.End, padObj.Position) <= tolerance ||
                       DistPointToSegment(padObj.Position, trackObj.Start, trackObj.End) <= trackObj.Width / 2f + padRadius + tolerance;
            }
            if (e1 is PcbPad pPadObj && e2 is PcbTrack tTrackObj)
            {
                return AreElementsConnected(tTrackObj, pPadObj);
            }

            // 3. Пад и Пад
            if (e1 is PcbPad pa1 && e2 is PcbPad pa2)
            {
                float r1 = pa1.Size.X / 2f;
                float r2 = pa2.Size.X / 2f;
                return Dist(pa1.Position, pa2.Position) <= r1 + r2 + tolerance;
            }

            return false;
        }

        // Массовое изменение слоя для всех выбранных элементов
        public void SetLayerForSelected(PcbLayer layer)
        {
            // 🟢 Проверим через отладку, видит ли инспектор те же элементы, что и холст
            System.Diagnostics.Debug.WriteLine($"Инспектор видит элементов на плате: {Elements.Count} ( MultiSelected: {MultiSelectedElements.Count} )");

            if (MultiSelectedElements == null || MultiSelectedElements.Count == 0) return;

            foreach (var element in MultiSelectedElements)
            {
                element.Layer = layer;
            }

            RequestInvalidate?.Invoke();
        }

        // Массовое изменение ширины (только для дорожек из выбранных)
        public void SetTrackWidthForSelected(float width)
        {
            if (MultiSelectedElements == null || MultiSelectedElements.Count == 0) return;

            foreach (var element in MultiSelectedElements)
            {
                if (element is PcbTrack track)
                {
                    track.Width = width;
                }
            }

            RequestInvalidate?.Invoke();
        }

        public void DefineBoardShapeFromSelected()
        {
            if (MultiSelectedElements == null || MultiSelectedElements.Count == 0) return;

            // Извлекаем дорожки (сегменты контура) из выделенных элементов
            var tracks = MultiSelectedElements.OfType<PcbTrack>().ToList();
            if (tracks.Count == 0) return;

            var contourPoints = ChainTracksIntoPolygon(tracks);
            if (contourPoints != null && contourPoints.Count >= 3)
            {
                BoardContour = contourPoints;
                RequestInvalidate?.Invoke();
            }
        }

        // Алгоритм упорядочивания сегментов дорожек в непрерывный замкнутый контур
        private List<Point2D> ChainTracksIntoPolygon(List<PcbTrack> tracks)
        {
            var remainingTracks = new List<PcbTrack>(tracks);
            var polygon = new List<Point2D>();

            var currentTrack = remainingTracks[0];
            remainingTracks.RemoveAt(0);

            polygon.Add(currentTrack.Start);
            var nextTarget = currentTrack.End;
            polygon.Add(nextTarget);

            float tolerance = 0.5f;

            while (remainingTracks.Count > 0)
            {
                bool foundNext = false;
                for (int i = 0; i < remainingTracks.Count; i++)
                {
                    var t = remainingTracks[i];
                    bool matchesStart = Math.Abs(t.Start.X - nextTarget.X) < tolerance && Math.Abs(t.Start.Y - nextTarget.Y) < tolerance;
                    bool matchesEnd = Math.Abs(t.End.X - nextTarget.X) < tolerance && Math.Abs(t.End.Y - nextTarget.Y) < tolerance;

                    if (matchesStart || matchesEnd)
                    {
                        nextTarget = matchesStart ? t.End : t.Start;
                        polygon.Add(nextTarget);
                        remainingTracks.RemoveAt(i);
                        foundNext = true;
                        break;
                    }
                }

                if (!foundNext) break;
            }

            // Удаляем дублирующуюся замыкающую точку, если контур замкнут
            if (polygon.Count >= 3)
            {
                var first = polygon[0];
                var last = polygon[polygon.Count - 1];
                if (Math.Abs(first.X - last.X) < tolerance && Math.Abs(first.Y - last.Y) < tolerance)
                {
                    polygon.RemoveAt(polygon.Count - 1);
                }
            }

            return polygon;
        }

    }
}