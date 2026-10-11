using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WinForms;


namespace scottPlotTest
{
    public partial class Form1 : Form
    {
        private ScottPlot.WinForms.FormsPlot myPlot;
        public Form1()
        {
            InitializeComponent();

            var sig = formsPlot1.Plot.Add.Signal(ScottPlot.Generate.Sin());
        }
    }
}
