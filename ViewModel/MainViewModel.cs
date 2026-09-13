using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TracerAI.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // При присвоении нового значения UI сам перерисуется благодаря Fody
        public object CurrentViewModel { get; set; }

        public MainViewModel()
        {
            
            CurrentViewModel = new PcbViewModel();
        }

        // Метод/команда для переключения на PCB
        public void ShowPcbPage()
        {
            CurrentViewModel = new PcbViewModel();
        }
    }
}
