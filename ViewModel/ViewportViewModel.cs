using DevExpress.Mvvm;
using System.ComponentModel;
using System.Windows.Input;
using TracerAI.ViewModel;

namespace TracerAI.ViewModel
{
    public class ViewportViewModel : ViewModelBase
    {
        // Свойство текущей активной ViewModel для отображения в ContentControl
        public INotifyPropertyChanged CurrentViewModel { get; set; }

        // Команда для кнопки "PCB"
        public ICommand OpenPcbCommand { get; }

        public ViewportViewModel()
        {
            OpenPcbCommand = new DelegateCommand(OpenPcb);

            // Если нужно открыть PCB сразу при запуске:
            OpenPcb();
        }

        private void OpenPcb()
        {
            CurrentViewModel = new PcbViewModel();
        }

        private void OpenPcbLib()
        {

        }
    }
}