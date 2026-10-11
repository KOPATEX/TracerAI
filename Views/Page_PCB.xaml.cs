using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using TracerAI.Model;     
using TracerAI.ViewModel;
using System.Windows.Media;
using System.Numerics;
using System.Windows.Media.TextFormatting;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;
using DevExpress.Mvvm.POCO;

namespace TracerAI.Views
{
    public partial class Page_PCB : UserControl
    {
        private PcbViewModel ViewModel => DataContext as PcbViewModel;
        private Point2D _lastMousePosition;

        public Page_PCB()
        {
            InitializeComponent();

            this.DataContextChanged += (s, e) =>
            {
                if (ViewModel != null)
                {
                    ViewModel.RequestInvalidate = () =>
                    {
                        SkiaCanvas.InvalidateVisual();
                    };
                }
            };

        }

        // 1. Отрисовка
        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColor.Parse("#1A1A1A"));

            if (ViewModel == null) return;

            DrawGrid(canvas, e.Info.Width, e.Info.Height);
            DrawPcbElements(canvas); // Здесь уже происходит вся отрисовка элементов
            DrawOrigin(canvas);
            DrawCrosshair(canvas);
        }

        private void DrawGrid(SKCanvas canvas, int width, int height)
        {
            float baseStep = ViewModel.GridSettings.Step;
            if (baseStep <= 0 || !ViewModel.GridSettings.IsSnapEnabled) return;

            // 1. Вычисляем текущий масштаб (сколько пикселей экрана в 1 мировой единице)
            var p0 = ViewModel.WorldToScreen(new Point2D(0, 0));
            var p1 = ViewModel.WorldToScreen(new Point2D(1, 0));
            float pixelsPerWorldUnit = (float)Math.Abs(p1.X - p0.X);

            if (pixelsPerWorldUnit <= 0) return;

            // 2. Адаптивный шаг: гарантируем, что визуальное расстояние между точками не менее 15px
            float minPixelSpacing = 15.0f;
            float effectiveStep = baseStep;

            while (effectiveStep * pixelsPerWorldUnit < minPixelSpacing)
            {
                effectiveStep *= 2f; // При сильном отдалении укрупняем шаг сетки 
            }

            // 3. Вычисляем границы отображения только для видимого экрана
            // После переворота оси Y мировой minY находится внизу экрана (height), maxY — вверху
            var topLeft = ViewModel.ScreenToWorld(new Point2D(0, 0));
            var bottomRight = ViewModel.ScreenToWorld(new Point2D(width, height));

            float startY = (float)Math.Floor(Math.Min(topLeft.Y, bottomRight.Y) / effectiveStep) * effectiveStep;
            float endY = (float)Math.Ceiling(Math.Max(topLeft.Y, bottomRight.Y) / effectiveStep) * effectiveStep;

            float startX = (float)Math.Floor(topLeft.X / effectiveStep) * effectiveStep;
            float endX = (float)Math.Ceiling(bottomRight.X / effectiveStep) * effectiveStep;

            // 4. Отрисовка с отключенным IsAntialias для обычных точек (кратно увеличивает FPS)
            using (var dotPaint = new SKPaint { Color = SKColor.Parse("#404040"), IsAntialias = false })
            using (var majorDotPaint = new SKPaint { Color = SKColor.Parse("#808080"), IsAntialias = true })
            {
                for (float x = startX; x <= endX; x += effectiveStep)
                {
                    for (float y = startY; y <= endY; y += effectiveStep)
                    {
                        var screenPt = ViewModel.WorldToScreen(new Point2D(x, y));

                        // Подсветка узловых (Major) точек
                        bool isMajor = (Math.Abs(x % (effectiveStep * 10)) < 0.001f) &&
                                       (Math.Abs(y % (effectiveStep * 10)) < 0.001f);

                        var paint = isMajor ? majorDotPaint : dotPaint;
                        float radius = isMajor ? 2.0f : 1.0f;

                        canvas.DrawCircle((float)screenPt.X, (float)screenPt.Y, radius, paint);
                    }
                }
            }
        }

        private void DrawCrosshair(SKCanvas canvas)
        {
            // 1. Переменные мировых координат (не зависят от Zoom, в мм/милах)
            if (ViewModel?.SnappedWorldCursor == null) return;

            Point2D worldPos = ViewModel.SnappedWorldCursor;
            float worldX = (float)worldPos.X;
            float worldY = (float)worldPos.Y;

            // 2. Экранные координаты для отрисовки самого прицела (в пикселях)
            var screenCursor = ViewModel.WorldToScreen(worldPos);
            float screenX = (float)screenCursor.X;
            float screenY = (float)screenCursor.Y;

            // 3. Форматирование строки (F2 задает 2 знака после запятой, например "X: 12.50; Y: -4.25")
            string coordText = $"X: {worldX:F2}; Y: {worldY:F2}";

            using (var paint = new SKPaint
            {
                Color = SKColors.Cyan,
                StrokeWidth = 1f,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            })
            {
                // Отрисовка перекрестия
                canvas.DrawLine(screenX - 10, screenY, screenX + 10, screenY, paint);
                canvas.DrawLine(screenX, screenY - 10, screenX, screenY + 10, paint);
                canvas.DrawCircle(screenX, screenY, 4, paint);

                // Отрисовка текста координат
                using (var font = new SKFont(SKTypeface.Default, 14f))
                {
                    paint.Style = SKPaintStyle.Fill;
                    canvas.DrawText(coordText, screenX + 8, screenY - 8, SKTextAlign.Left, font, paint);
                }
            }
        }

        // 2. Движение мыши
        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (ViewModel == null) return;

            var currentPos = GetSkiaMousePosition(e);

            // Панорамирование колесиком
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                var delta = new Point2D(currentPos.X - _lastMousePosition.X, currentPos.Y - _lastMousePosition.Y);
                ViewModel.Pan(delta);
            }

            _lastMousePosition = currentPos;
            var pos = e.GetPosition((IInputElement)sender);
            var screenPoint = new Point2D((float)pos.X, (float)pos.Y);

            ViewModel.UpdateCursorPosition(screenPoint); // у вас уже есть
            ViewModel.UpdateHoveredElement(screenPoint); // новый вызов

            
            // Логика предпросмотра нарисованной линии
            if (ViewModel.IsDrawing)
            {
                if (ViewModel.CurrentTool == ToolMode.Track)
                {
                    // Корректируем превью под угол 45°
                    ViewModel.DrawingPreviewPoint = ViewModel.SnapTo45Degrees(
                        ViewModel.DrawingStartPoint,
                        ViewModel.SnappedWorldCursor
                    );

                }
                else
                {
                    ViewModel.DrawingPreviewPoint = ViewModel.SnappedWorldCursor;
                }

            }


            SkiaCanvas.InvalidateVisual();
        }

        private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel == null) return;

            var pos = GetSkiaMousePosition(e);
            float zoomFactor = e.Delta > 0 ? 1.15f : 0.85f;

            ViewModel.Zoom(zoomFactor, pos);
            SkiaCanvas.InvalidateVisual();
        }

        // 3. Нажатие кнопки мыши (Исправлен тип параметров: MouseButtonEventArgs)
        




        private void DrawOrigin(SKCanvas canvas)
        {
            if (ViewModel == null) return;

            // Переводим мировую точку начала координат (0,0) в экранные пиксели
            var originScreen = ViewModel.WorldToScreen(new Point2D(0, 0));
            
            float x = (float)originScreen.X;
            float y = (float)originScreen.Y;
            

            float axisLength = 40f; // Длина осей в пикселях

            // 1. Ось X (Красная)
            using (var redPaint = new SKPaint { Color = SKColors.IndianRed, StrokeWidth = 2f, IsAntialias = true })
            {
                canvas.DrawLine(x, y, x + axisLength, y, redPaint);
            }

            // 2. Ось Y (Зеленая) — направлена вверх в мировой системе координат
            using (var greenPaint = new SKPaint { Color = SKColors.MediumSeaGreen, StrokeWidth = 2f, IsAntialias = true })
            {
                canvas.DrawLine(x, y, x, y - axisLength, greenPaint);
            }

            // 3. Маркер центра (Окружность с перекрестием)
            using (var centerPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true })
            {
                
                canvas.DrawCircle(x, y, 6f, centerPaint);
                canvas.DrawLine(x - 3f, y, x + 3f, y, centerPaint);
                canvas.DrawLine(x, y - 3f, x, y + 3f, centerPaint);
            }
        }
        private void OnCanvasMouseEnter(object sender, MouseEventArgs e)
        {
            // Скрываем системный курсор при входе на холст
            Mouse.OverrideCursor = Cursors.None;
        }

        private void OnCanvasMouseLeave(object sender, MouseEventArgs e)
        {
            // Возвращаем стандартный системный курсор при выходе за пределы холста
            Mouse.OverrideCursor = null;
        }
        private Point2D GetSkiaMousePosition(MouseEventArgs e)
        {
            // Получаем логические координаты WPF
            var pos = e.GetPosition(SkiaCanvas);

            // Получаем текущий коэффициент масштабирования Windows (например, 1.25 для 125%)
            var dpi = VisualTreeHelper.GetDpi(SkiaCanvas);

            // Переводим в физические пиксели SkiaSharp
            return new Point2D(
                (float)(pos.X * dpi.DpiScaleX),
                (float)(pos.Y * dpi.DpiScaleY)
            );
        }


        // создание элементов платы 
        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Focus();
            Keyboard.Focus(SkiaCanvas);

            _lastMousePosition = GetSkiaMousePosition(e);
            if (ViewModel == null) return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var cursor = ViewModel.SnappedWorldCursor;

                // 1. Выделение элемента в режиме Edit или None (инструмент не выбран)
                if (ViewModel.CurrentTool == ToolMode.None || ViewModel.CurrentTool == ToolMode.Edit)
                {
                    ViewModel.SelectElementAt(cursor);
                    SkiaCanvas.InvalidateVisual();
                    return;
                }

                // 2. Рисование дорожки Track
                if (ViewModel.CurrentTool == ToolMode.Track)
                {
                    if (ViewModel.IsDrawing)
                    {
                        cursor = ViewModel.SnapTo45Degrees(ViewModel.DrawingStartPoint, cursor);
                    }

                    if (!ViewModel.IsDrawing)
                    {
                        ViewModel.IsDrawing = true;
                        ViewModel.DrawingStartPoint = cursor;
                        ViewModel.DrawingPreviewPoint = cursor;
                    }
                    else
                    {
                        // 🟢 Создаем трек с передачей слоя, ширины и страховкой через индекс
                        var newTrack = new PcbTrack
                        {
                            Start = ViewModel.DrawingStartPoint,
                            End = cursor,
                            Width = ViewModel.CurrentTrackWidth,
                            Layer = ViewModel.CurrentLayer ?? ViewModel.AvailableLayers[0]
                        };

                        ViewModel.Elements.Add(newTrack);

                        // 🟢 Сразу делаем элемент выбранным, чтобы инспектор отобразил его параметры
                        ViewModel.SelectedElement = newTrack;

                        ViewModel.DrawingStartPoint = cursor;
                        ViewModel.DrawingPreviewPoint = cursor;
                    }
                }
                else // 3. Остальные инструменты (Rectangle, Pad, Circle)
                {
                    if (!ViewModel.IsDrawing)
                    {
                        ViewModel.IsDrawing = true;
                        ViewModel.DrawingStartPoint = cursor;
                        ViewModel.DrawingPreviewPoint = cursor;
                    }
                    else
                    {
                        ViewModel.DrawingPreviewPoint = cursor;
                        ViewModel.CommitCurrentElement();
                    }
                }

                SkiaCanvas.InvalidateVisual();
            }

            // Завершение по ПКМ
            if (e.RightButton == MouseButtonState.Pressed && ViewModel.IsDrawing)
            {
                ViewModel.CancelDrawing();
                SkiaCanvas.InvalidateVisual();
            }
        }

        private void DrawPcbElements(SKCanvas canvas)
        {
            if (ViewModel == null || ViewModel.Elements == null) return;
            DrawBoardOutline(canvas);

            // Базовые кисти (цвета будут переопределяться под слой элемента)
            using (var rectPaint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true })
            using (var padPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true })
            using (var padHolePaint = new SKPaint { Color = SKColor.Parse("#1A1A1A"), Style = SKPaintStyle.Fill, IsAntialias = true })
            using (var circlePaint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true })
            using (var trackPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true
            })
            using (var previewPaint = new SKPaint
            {
                Color = SKColors.White,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash(new float[] { 6, 6 }, 0)
            })
            {
                // ----------------------------------------------------
                // А. Отрисовка СОЗДАННЫХ элементов с учетом цвета их слоя
                // ----------------------------------------------------
                foreach (var element in ViewModel.Elements)
                {
                    // Берем цвет слоя или белый по умолчанию, если слой не назначен
                    SKColor layerColor = element.Layer?.Color ?? SKColors.White;

                    switch (element)
                    {
                        case PcbTrack track:
                            trackPaint.Color = layerColor;
                            var tStart = ViewModel.WorldToScreen(track.Start);
                            var tEnd = ViewModel.WorldToScreen(track.End);
                            trackPaint.StrokeWidth = track.Width * ViewModel.Scale;
                            canvas.DrawLine((float)tStart.X, (float)tStart.Y, (float)tEnd.X, (float)tEnd.Y, trackPaint);
                            break;

                        case PcbRectangle rect:
                            rectPaint.Color = layerColor;
                            var rStart = ViewModel.WorldToScreen(rect.Start);
                            var rEnd = ViewModel.WorldToScreen(rect.End);
                            float left = (float)Math.Min(rStart.X, rEnd.X);
                            float top = (float)Math.Min(rStart.Y, rEnd.Y);
                            float width = (float)Math.Abs(rEnd.X - rStart.X);
                            float height = (float)Math.Abs(rEnd.Y - rStart.Y);
                            canvas.DrawRect(left, top, width, height, rectPaint);
                            break;

                        case PcbPad pad:
                            padPaint.Color = layerColor;
                            var padScreen = ViewModel.WorldToScreen(pad.Position);
                            float padSizePx = (float)(pad.Size.X * ViewModel.Scale);
                            float holeSizePx = (float)(pad.HoleSize * ViewModel.Scale);

                            canvas.DrawCircle((float)padScreen.X, (float)padScreen.Y, padSizePx / 2f, padPaint);
                            canvas.DrawCircle((float)padScreen.X, (float)padScreen.Y, holeSizePx / 2f, padHolePaint);
                            break;

                        case PcbCircle circle:
                            circlePaint.Color = layerColor;
                            var cCenter = ViewModel.WorldToScreen(circle.Center);
                            float radiusPx = circle.Radius * ViewModel.Scale;
                            canvas.DrawCircle((float)cCenter.X, (float)cCenter.Y, radiusPx, circlePaint);
                            break;
                    }
                }

                // ----------------------------------------------------
                // Б. Отрисовка ФАНТОМА при создании нового элемента
                // ----------------------------------------------------
                if (ViewModel.IsDrawing)
                {
                    var pStart = ViewModel.WorldToScreen(ViewModel.DrawingStartPoint);
                    var pPreview = ViewModel.WorldToScreen(ViewModel.DrawingPreviewPoint);

                    switch (ViewModel.CurrentTool)
                    {
                        case ToolMode.Track:
                            canvas.DrawLine((float)pStart.X, (float)pStart.Y, (float)pPreview.X, (float)pPreview.Y, previewPaint);
                            break;

                        case ToolMode.Rectangle:
                            float pLeft = (float)Math.Min(pStart.X, pPreview.X);
                            float pTop = (float)Math.Min(pStart.Y, pPreview.Y);
                            float pWidth = (float)Math.Abs(pPreview.X - pStart.X);
                            float pHeight = (float)Math.Abs(pPreview.Y - pStart.Y);
                            canvas.DrawRect(pLeft, pTop, pWidth, pHeight, previewPaint);
                            break;

                        case ToolMode.Circle:
                            float pRadius = (float)Math.Sqrt(
                                Math.Pow(pPreview.X - pStart.X, 2) +
                                Math.Pow(pPreview.Y - pStart.Y, 2)
                            );
                            canvas.DrawCircle((float)pStart.X, (float)pStart.Y, pRadius, previewPaint);
                            break;
                    }
                }
            }

            // Подсветка выбранного элемента
            if (ViewModel.MultiSelectedElements != null && ViewModel.MultiSelectedElements.Count > 0)
            {
                using (var selectPaint = new SKPaint
                {
                    Color = SKColors.Cyan,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2f,
                    IsAntialias = true,
                    PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0)
                })
                {
                    foreach (var selectedElement in ViewModel.MultiSelectedElements)
                    {
                        switch (selectedElement)
                        {
                            case PcbTrack track:
                                var s1 = ViewModel.WorldToScreen(track.Start);
                                var s2 = ViewModel.WorldToScreen(track.End);
                                canvas.DrawLine((float)s1.X, (float)s1.Y, (float)s2.X, (float)s2.Y, selectPaint);
                                break;

                            case PcbPad pad:
                                var pPos = ViewModel.WorldToScreen(pad.Position);
                                float pRad = (pad.Size.X * ViewModel.Scale) / 2f + 4;
                                canvas.DrawCircle((float)pPos.X, (float)pPos.Y, pRad, selectPaint);
                                break;

                            case PcbCircle circle:
                                var cCenter = ViewModel.WorldToScreen(circle.Center);
                                float cRad = (circle.Radius * ViewModel.Scale) + 3;
                                canvas.DrawCircle((float)cCenter.X, (float)cCenter.Y, cRad, selectPaint);
                                break;

                            case PcbRectangle rect:
                                var r1 = ViewModel.WorldToScreen(rect.Start);
                                var r2 = ViewModel.WorldToScreen(rect.End);
                                float l = (float)Math.Min(r1.X, r2.X) - 3;
                                float t = (float)Math.Min(r1.Y, r2.Y) - 3;
                                float w = (float)Math.Abs(r2.X - r1.X) + 6;
                                float h = (float)Math.Abs(r2.Y - r1.Y) + 6;
                                canvas.DrawRect(l, t, w, h, selectPaint);
                                break;
                        }
                    }
                }
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.Focus();
        }

        // Обработка нажатия клавиш на холсте/панели
        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel == null) return;

            // 1. Удаление по Delete или Backspace
            if ((e.Key == Key.Delete || e.Key == Key.Back) && ViewModel.SelectedElement != null)
            {
                ViewModel.Elements.Remove(ViewModel.SelectedElement);
                ViewModel.SelectedElement = null;
                SkiaCanvas.InvalidateVisual();
                e.Handled = true;
            }

            // 2. Выделение всех связанных элементов по клавише Tab
            if (e.Key == Key.Tab && ViewModel.SelectedElement != null)
            {
                ViewModel.SelectConnectedElements();

                System.Diagnostics.Debug.WriteLine($"Выделено элементов цепи: {ViewModel.MultiSelectedElements.Count}");
                SkiaCanvas.InvalidateVisual();
                e.Handled = true; // Предотвращаем стандартный переход фокуса по Tab
            }

        }

        private void Page_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel == null) return;

            // Проверяем, не вводит ли пользователь текст в поле инспектора в данный момент
            bool isEditingText = Keyboard.FocusedElement is TextBox;

            // 1. Удаление по Delete или Backspace (только если не редактируем текст)
            if ((e.Key == Key.Delete || e.Key == Key.Back) && ViewModel.SelectedElement != null && !isEditingText)
            {
                ViewModel.Elements.Remove(ViewModel.SelectedElement);
                ViewModel.SelectedElement = null;
                SkiaCanvas.InvalidateVisual();
                e.Handled = true;
            }

            // 2. Выделение всех связанных элементов по клавише Tab
            if (e.Key == Key.Tab && ViewModel.SelectedElement != null && !isEditingText)
            {
                ViewModel.SelectConnectedElements(); // 🟢 Заполняет MultiSelectedElements всей цепью
                SkiaCanvas.InvalidateVisual();
                e.Handled = true; // Предотвращаем стандартный увод фокуса по Tab в WPF
            }
        }

        private void DrawBoardOutline(SKCanvas canvas)
        {
            if (ViewModel?.BoardContour == null || ViewModel.BoardContour.Count < 3) return;

            using (var path = new SKPath())
            {
                var firstPt = ViewModel.WorldToScreen(ViewModel.BoardContour[0]);
                path.MoveTo((float)firstPt.X, (float)firstPt.Y);

                for (int i = 1; i < ViewModel.BoardContour.Count; i++)
                {
                    var pt = ViewModel.WorldToScreen(ViewModel.BoardContour[i]);
                    path.LineTo((float)pt.X, (float)pt.Y);
                }
                path.Close();

                // 1. Заливка тела платы (темно-зеленый текстолит)
                using (var fillPaint = new SKPaint { Color = SKColor.Parse("#112611"), Style = SKPaintStyle.Fill, IsAntialias = true })
                {
                    canvas.DrawPath(path, fillPaint);
                }

                // 2. Обводка границы платы
                using (var borderPaint = new SKPaint { Color = SKColors.LimeGreen, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true })
                {
                    canvas.DrawPath(path, borderPaint);
                }
            }
        }
    }
}