using System.Windows.Forms;

namespace ProgressBarManager
{
    public class ProgressBarManager
    {
        private ProgressBar progressBar;

        public ProgressBarManager(ProgressBar progressBar)
        {
            this.progressBar = progressBar;
            InitializeProgressBar();
        }

        private void InitializeProgressBar()
        {
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            progressBar.Style = ProgressBarStyle.Blocks;
        }

        public void SetProgress(int value)
        {
            if (value < progressBar.Minimum)
            {
                progressBar.Value = progressBar.Minimum;
            }
            else if (value > progressBar.Maximum)
            {
                progressBar.Value = progressBar.Maximum;
            }
            else
            {
                progressBar.Value = value;
            }
        }
    }
}
