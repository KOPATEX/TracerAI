using DevExpress.Mvvm;
using System.Collections.ObjectModel;
using System.Xml.Linq;
using TracerAI.Model;

namespace TracerAI.ViewModel
{
    public class ComponentPropertiesModel : ViewModelBase
    {
        private readonly PcbViewModel _pcbViewModel;
        public ObservableCollection<PcbLayer> AvailableLayers => _pcbViewModel?.AvailableLayers;
        public ObservableCollection<PcbElement> MultiSelectedElements => _pcbViewModel?.MultiSelectedElements;
        public ComponentPropertiesModel(PcbViewModel pcbViewModel)
        {
            _pcbViewModel = pcbViewModel;

            // Подписываемся на изменения в PcbViewModel, чтобы пробрасывать их в UI
            if (_pcbViewModel != null)
            {
                _pcbViewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(PcbViewModel.SelectedElement))
                    {
                        RaisePropertyChanged(nameof(SelectedElement));

                        // 🟢 КРИТИЧЕСКИ ВАЖНО: уведомляем инспектор, что значения свойств изменились при смене выбора
                        RaisePropertyChanged(nameof(CurrentLayer));
                        RaisePropertyChanged(nameof(CurrentTrackWidth));
                        RaisePropertyChanged(nameof(TrackWidth));
                    }
                };
            }
        }

        public ComponentPropertiesModel() { }

        // Публичное свойство для привязки в инспекторе
        public PcbElement SelectedElement => _pcbViewModel?.SelectedElement;

        public float TrackWidth
        {
            get => _pcbViewModel?.CurrentTrackWidth ?? 0.5f;
            set
            {
                if (_pcbViewModel != null && _pcbViewModel.CurrentTrackWidth != value)
                {
                    _pcbViewModel.CurrentTrackWidth = value;
                    RaisePropertyChanged();
                }
            }
        }

        public float GridStep
        {
            get => _pcbViewModel?.GridSettings.Step ?? 1.0f;
            set
            {
                if (_pcbViewModel != null && _pcbViewModel.GridSettings.Step != value)
                {
                    _pcbViewModel.GridSettings.Step = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool SnapToGrid
        {
            get => _pcbViewModel?.GridSettings.IsSnapEnabled ?? true;
            set
            {
                if (_pcbViewModel != null && _pcbViewModel.GridSettings.IsSnapEnabled != value)
                {
                    _pcbViewModel.GridSettings.IsSnapEnabled = value;
                    RaisePropertyChanged();
                }
            }
        }

        public PcbLayer CurrentLayer
        {
            get => _pcbViewModel?.SelectedElement?.Layer ?? _pcbViewModel?.CurrentLayer;
            set
            {
                if (_pcbViewModel != null)
                {
                    // Если есть мульти-выделение (цепочка по Tab), применяем ко ВСЕМ элементам группы
                    if (_pcbViewModel.MultiSelectedElements != null && _pcbViewModel.MultiSelectedElements.Count > 0)
                    {
                        foreach (var el in _pcbViewModel.MultiSelectedElements)
                        {
                            el.Layer = value;
                        }
                    }
                    else if (_pcbViewModel.SelectedElement != null)
                    {
                        _pcbViewModel.SelectedElement.Layer = value;
                    }

                    _pcbViewModel.CurrentLayer = value;

                    // 🟢 Обязательно запрашиваем перерисовку холста
                    _pcbViewModel.RequestInvalidate?.Invoke();

                    // 🟢 Уведомляем инспектор об изменении свойства
                    RaisePropertyChanged();
                }
            }
        }

        public float CurrentTrackWidth
        {
            get => (_pcbViewModel?.SelectedElement as PcbTrack)?.Width ?? _pcbViewModel?.CurrentTrackWidth ?? 0.5f;
            set
            {
                if (_pcbViewModel != null)
                {
                    // Если есть мульти-выделение, применяем ко ВСЕМ дорожкам в группе
                    if (_pcbViewModel.MultiSelectedElements != null && _pcbViewModel.MultiSelectedElements.Count > 0)
                    {
                        foreach (var el in _pcbViewModel.MultiSelectedElements)
                        {
                            if (el is PcbTrack track)
                            {
                                track.Width = value;
                            }
                        }
                    }
                    else if (_pcbViewModel.SelectedElement is PcbTrack selectedTrack)
                    {
                        selectedTrack.Width = value;
                    }

                    _pcbViewModel.CurrentTrackWidth = value;

                    // 🟢 Обязательно запрашиваем перерисовку холста
                    _pcbViewModel.RequestInvalidate?.Invoke();

                    // 🟢 Уведомляем инспектор об изменении свойства
                    RaisePropertyChanged();
                }
            }
        }


    }
}