using System.Collections.Generic;

namespace Paket.Bootstrapper
{
    public class BootstrapperOptions
    {
        public BootstrapperOptions()
        {
            DownloadArguments = new DownloadArguments();
        }

        public DownloadArguments DownloadArguments { get; set; }

        public bool Silent { get; set; }
        public bool ForceNuget { get; set; }
        public bool PreferNuget { get; set; }
        public bool ShowHelp { get; set; }
        public bool Run { get; set; }
        public IEnumerable<string> RunArgs { get; set; }
        public IEnumerable<string> UnprocessedCommandArgs { get; set; }
    }
}