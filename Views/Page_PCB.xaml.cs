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
//using System.Drawing;
using System.Windows.Media.TextFormatting;

namespace TracerAI.Views
{
    public partial class Page_PCB : UserControl
    {
        private PcbViewModel ViewModel => DataContext as PcbViewModel;
        private Point _lastMousePosition;

        public Page_PCB()
        {
            InitializeComponent();
        }

        // 1. Отрисовка
        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColor.Parse("#1A1A1A"));

            if (ViewModel == null) return;

            // 1. Сетка
            DrawGrid(canvas, e.Info.Width, e.Info.Height);

            // 2. Начало координат (0,0)
            DrawOrigin(canvas);

            // 3. Прицел курсора
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
                effectiveStep *= 2f; // При сильном отдалении укрупняем шаг сетки (2x, 4x, 8x...)
            }

            // 3. Вычисляем границы отображения только для видимого экрана
            var topLeft = ViewModel.ScreenToWorld(new Point2D(0, 0));
            var bottomRight = ViewModel.ScreenToWorld(new Point2D(width, height));

            float startX = (float)Math.Floor(topLeft.X / effectiveStep) * effectiveStep;
            float endX = (float)Math.Ceiling(bottomRight.X / effectiveStep) * effectiveStep;
            float startY = (float)Math.Floor(topLeft.Y / effectiveStep) * effectiveStep;
            float endY = (float)Math.Ceiling(bottomRight.Y / effectiveStep) * effectiveStep;

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
            _lastMousePosition = e.GetPosition(SkiaCanvas); // Логическая точка для расчета дельты
            // Берем координаты с учетом DPI
            var currentPos = GetSkiaMousePosition(e);

            // Панорамирование (зажатое колесико)
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                var delta = new Point2D(
                    (currentPos.X - (float)_lastMousePosition.X)*0.1f,
                    (currentPos.Y - (float)_lastMousePosition.Y) * 0.1f
                );

                ViewModel.Pan(delta);
            }

            // Передаем точные физические координаты в ViewModel
            ViewModel.UpdateCursorPosition(currentPos);
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
        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            _lastMousePosition = e.GetPosition(SkiaCanvas);
        }

        
        

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
    }
}