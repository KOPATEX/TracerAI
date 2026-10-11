using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using TracerAI.Model;
using TracerAI.ViewModel;

namespace TracerAI.Views
{
    public partial class ComponentPtoperties : UserControl
    {
        public ComponentPtoperties()
        {
            InitializeComponent();

            // Подписываемся на смену DataContext (когда передается ComponentPropertiesModel)
            this.DataContextChanged += (s, e) =>
            {
                if (DataContext is ComponentPropertiesModel model)
                {
                    // Слушаем изменения свойств в модели
                    model.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == nameof(ComponentPropertiesModel.SelectedElement))
                        {
                            RebuildFields();
                        }
                    };
                }
                RebuildFields();
            };
        }

        public void RebuildFields()
        {
            FieldsPanel.Children.Clear();

            if (DataContext is ComponentPropertiesModel model)
            {
                // 1. Если элемент выбран — генерируем свойства элемента (координаты, слой элемента и т.д.)
                if (model.SelectedElement != null)
                {
                    GenerateFieldsForTarget(model.SelectedElement);
                }
                // 2. Если элемент не выбран — показываем глобальные настройки инструмента (CurrentLayer, TrackWidth и т.д.)
                else
                {
                    GenerateFieldsForTarget(model);
                }
            }
        }

        private void MainGrid_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            GenerateFieldsForTarget(e.NewValue);
        }

        private void GenerateFieldsForTarget(object target)
        {
            FieldsPanel.Children.Clear();

            if (target == null)
                return;

            var properties = target.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite);

            foreach (var prop in properties)
            {
                // 1. Поля координат (Point2D)
                if (prop.PropertyType == typeof(Point2D))
                {
                    var groupPanel = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Margin = new Thickness(0, 0, 0, 12)
                    };

                    var groupLabel = new TextBlock
                    {
                        Text = prop.Name,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.Cyan,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    groupPanel.Children.Add(groupLabel);

                    var coordsPanel = new StackPanel { Orientation = Orientation.Horizontal };

                    // Текст X
                    coordsPanel.Children.Add(new TextBlock
                    {
                        Text = "X: ",
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    // Поле X
                    var txtX = new TextBox
                    {
                        Width = 60,
                        Height = 24,
                        Margin = new Thickness(0, 0, 12, 0),
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333")),
                        Foreground = Brushes.White,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(3, 0, 3, 0)
                    };
                    txtX.SetBinding(TextBox.TextProperty, new Binding($"{prop.Name}.X")
                    {
                        Source = target,
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                    });
                    txtX.KeyDown += (s, e) =>
                    {
                        if (e.Key == System.Windows.Input.Key.Enter)
                            System.Windows.Input.Keyboard.ClearFocus();
                    };
                    txtX.LostFocus += (s, e) => RedrawCanvas();
                    coordsPanel.Children.Add(txtX);

                    // Текст Y
                    coordsPanel.Children.Add(new TextBlock
                    {
                        Text = "Y: ",
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    // Поле Y
                    var txtY = new TextBox
                    {
                        Width = 60,
                        Height = 24,
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333")),
                        Foreground = Brushes.White,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(3, 0, 3, 0)
                    };
                    txtY.SetBinding(TextBox.TextProperty, new Binding($"{prop.Name}.Y")
                    {
                        Source = target,
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                    });
                    txtY.KeyDown += (s, e) =>
                    {
                        if (e.Key == System.Windows.Input.Key.Enter)
                            System.Windows.Input.Keyboard.ClearFocus();
                    };
                    txtY.LostFocus += (s, e) => RedrawCanvas();
                    coordsPanel.Children.Add(txtY);

                    groupPanel.Children.Add(coordsPanel);
                    FieldsPanel.Children.Add(groupPanel);
                }
                // 2. Логическое поле (bool)
                else if (prop.PropertyType == typeof(bool))
                {
                    var row = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    var checkBox = new CheckBox
                    {
                        Content = prop.Name,
                        Foreground = Brushes.White,
                        Margin = new Thickness(0, 4, 0, 0)
                    };
                    checkBox.SetBinding(CheckBox.IsCheckedProperty, new Binding(prop.Name)
                    {
                        Source = target,
                        Mode = BindingMode.TwoWay
                    });
                    row.Children.Add(checkBox);
                    FieldsPanel.Children.Add(row);
                }
                else if (prop.PropertyType == typeof(PcbLayer))
                {
                    var row = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 12) };

                    var label = new TextBlock
                    {
                        Text = "Layer",
                        Margin = new Thickness(0, 0, 0, 2),
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    };

                    var comboBox = new ComboBox
                    {
                        Height = 26,
                        DisplayMemberPath = "Name",
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333")),
                        Foreground = Brushes.White
                    };

                    // 1. Получаем список доступных слоев из DataContext панели
                    IEnumerable<PcbLayer> layersSource = null;
                    if (DataContext is ComponentPropertiesModel modelVM)
                    {
                        layersSource = modelVM.AvailableLayers;
                    }
                    else if (Application.Current.MainWindow?.DataContext is PcbViewModel mainVM)
                    {
                        layersSource = mainVM.AvailableLayers;
                    }

                    comboBox.ItemsSource = layersSource;

                    // 2. Если у объекта Layer еще не задан (null), принудительно ставим первый слой
                    var currentLayer = prop.GetValue(target) as PcbLayer;
                    if (currentLayer == null && layersSource != null && layersSource.Any())
                    {
                        currentLayer = layersSource.First();
                        prop.SetValue(target, currentLayer); // Сохраняем слой в объекте
                    }

                    comboBox.SetBinding(ComboBox.SelectedItemProperty, new Binding(prop.Name)
                    {
                        Source = target,
                        Mode = BindingMode.TwoWay
                    });

                    // --- Индикатор и поле ввода цвета ---
                    var colorPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };

                    colorPanel.Children.Add(new TextBlock
                    {
                        Text = "Цвет: ",
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0)
                    });

                    var colorPreview = new Border
                    {
                        Width = 20,
                        Height = 20,
                        BorderBrush = Brushes.Gray,
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 8, 0),
                        CornerRadius = new CornerRadius(2)
                    };

                    var hexInput = new TextBox
                    {
                        Width = 75,
                        Height = 24,
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333")),
                        Foreground = Brushes.White,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(3, 0, 3, 0)
                    };

                    // Метод обновления плашки и поля ввода
                    Action updateColorControls = () =>
                    {
                        if (prop.GetValue(target) is PcbLayer layer)
                        {
                            colorPreview.Background = new SolidColorBrush(layer.WpfColor);
                            hexInput.Text = layer.HexColor;
                        }
                    };

                    // Заполняем элементы данными
                    updateColorControls();

                    // При изменении слоя в ComboBox
                    comboBox.SelectionChanged += (s, e) =>
                    {
                        if (comboBox.SelectedItem is PcbLayer selectedLayer)
                        {
                            // 🟢 Если есть группа элементов, применяем слой ко всем сразу
                            if (DataContext is ComponentPropertiesModel model && model.MultiSelectedElements != null && model.MultiSelectedElements.Count > 1)
                            {
                                foreach (var el in model.MultiSelectedElements)
                                {
                                    el.Layer = selectedLayer;
                                }
                            }
                            else
                            {
                                prop.SetValue(target, selectedLayer);
                            }

                            updateColorControls();
                            RedrawCanvas();
                        }
                    };

                    // При изменении HEX-кода
                    hexInput.LostFocus += (s, e) =>
                    {
                        if (prop.GetValue(target) is PcbLayer layer)
                        {
                            layer.HexColor = hexInput.Text;
                            updateColorControls();
                            RedrawCanvas();
                        }
                    };

                    hexInput.KeyDown += (s, e) =>
                    {
                        if (e.Key == System.Windows.Input.Key.Enter)
                            System.Windows.Input.Keyboard.ClearFocus();
                    };

                    colorPanel.Children.Add(colorPreview);
                    colorPanel.Children.Add(hexInput);

                    row.Children.Add(label);
                    row.Children.Add(comboBox);
                    row.Children.Add(colorPanel);
                    FieldsPanel.Children.Add(row);
                }
                // 3. Числовые/строковые поля (float, int, string)
                else
                {
                    var row = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    var label = new TextBlock
                    {
                        Text = prop.Name,
                        Margin = new Thickness(0, 0, 0, 2),
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    };
                    var textBox = new TextBox
                    {
                        Height = 24,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(3, 0, 3, 0),
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333")),
                        Foreground = Brushes.White
                    };
                    textBox.SetBinding(TextBox.TextProperty, new Binding(prop.Name)
                    {
                        Source = target,
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                        ValidatesOnExceptions = true
                    });

                    // 🟢 Распространение изменений на всю группу при завершении ввода
                    textBox.LostFocus += (s, e) =>
                    {
                        if (DataContext is ComponentPropertiesModel model && model.MultiSelectedElements != null && model.MultiSelectedElements.Count > 1)
                        {
                            var currentValue = prop.GetValue(target);
                            foreach (var el in model.MultiSelectedElements)
                            {
                                var p = el.GetType().GetProperty(prop.Name);
                                if (p != null && p.CanWrite)
                                {
                                    try { p.SetValue(el, currentValue); } catch { }
                                }
                            }
                            RedrawCanvas();
                        }
                    };

                    textBox.KeyDown += (s, e) =>
                    {
                        if (e.Key == Key.Enter)
                            Keyboard.ClearFocus();
                    };
                    row.Children.Add(label);
                    row.Children.Add(textBox);
                    FieldsPanel.Children.Add(row);
                }
            }
        }

        private void RedrawCanvas()
        {
            var pagePcb = Application.Current.MainWindow?.FindName("Page_PCB") as Page_PCB;
            if (pagePcb != null)
            {
                var skiaCanvas = pagePcb.FindName("SkiaCanvas") as SkiaSharp.Views.WPF.SKElement;
                skiaCanvas?.InvalidateVisual();
            }
        }

        private void UpdatePropertiesPanel()
        {
            FieldsPanel.Children.Clear();

            if (DataContext is ComponentPropertiesModel modelVM)
            {
                // 1. Если элемент выбран — показываем свойства элемента
                if (modelVM.SelectedElement != null)
                {
                    GenerateFieldsForTarget(modelVM.SelectedElement);
                }
                // 2. Если элемент не выбран, но активен инструмент рисования (например, Дорожка)
                else
                {
                    // Передаем саму модель (modelVM), чтобы отобразить CurrentTrackWidth и CurrentLayer
                    GenerateFieldsForTarget(modelVM);
                }
            }
        }
    }
}