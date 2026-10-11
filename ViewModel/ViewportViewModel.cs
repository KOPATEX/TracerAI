using DevExpress.Mvvm;
using System.ComponentModel;
using System.Windows.Input;
using TracerAI.ViewModel;
using TracerAI.Views;

namespace TracerAI.ViewModel
{
    public class ViewportViewModel : ViewModelBase
    {
        // Свойство текущей активной ViewModel для отображения в ContentControl
        public INotifyPropertyChanged CurrentViewModel { get; set; }
        public INotifyPropertyChanged CurrentMenuModel { get; set; }

        // Команда для кнопки "PCB"
        public ICommand OpenPcbCommand { get; }
        public ICommand OpenComponentPropertiesCommand { get; }

        public ViewportViewModel()
        {
            OpenPcbCommand = new DelegateCommand(OpenPcb);

            OpenComponentPropertiesCommand= new DelegateCommand(OpenComponentProperties);
            // Если нужно открыть PCB сразу при запуске:
            OpenComponentPropertiesCommand = new DelegateCommand(() =>
            {
                if (CurrentViewModel is PcbViewModel pcbVm)
                {
                    // Передаем ссылку на PCB во ViewModel свойств
                    CurrentMenuModel = new ComponentPropertiesModel(pcbVm);
                }
            });
            OpenPcb();
            OpenComponentProperties();
        }

        private void OpenPcb()
        {
            CurrentViewModel = new PcbViewModel();
        }

        private void OpenPcbLib()
        {

        }

        private void OpenComponentProperties()
        {
            CurrentMenuModel = new ComponentPropertiesModel();
            
        }


    }
}