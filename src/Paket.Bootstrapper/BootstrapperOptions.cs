using System.Collections.Generic;
using System.Linq;

namespace Paket.Bootstrapper
{
    public class BootstrapperOptions
    {
        public BootstrapperOptions()
        {
            DownloadArguments = new DownloadArguments();
            RunArgs = Enumerable.Empty<string>();
            UnprocessedCommandArgs = Enumerable.Empty<string>();
        }

        public DownloadArguments DownloadArguments { get; set; }

        public SilentMode Silent { get; set; }
        public bool ForceNuget { get; set; }
        public bool PreferNuget { get; set; }
        public bool ShowHelp { get; set; }
        public bool Run { get; set; }
        public IEnumerable<string> RunArgs { get; set; }
        public IEnumerable<string> UnprocessedCommandArgs { get; set; }
    }
}