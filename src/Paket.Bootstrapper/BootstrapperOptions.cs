using System.Collections.Generic;
using System.Linq;

namespace Paket.Bootstrapper
{
    public class BootstrapperOptions
    {
        public BootstrapperOptions()
        {
            Verbosity = Verbosity.Normal;
            DownloadArguments = new DownloadArguments();
            RunArgs = new List<string>();
            UnprocessedCommandArgs = Enumerable.Empty<string>();
        }

        public DownloadArguments DownloadArguments { get; set; }
        
        public Verbosity Verbosity { get; set; }
        public bool ForceNuget { get; set; }
        public bool PreferNuget { get; set; }
        public bool ShowHelp { get; set; }
        public bool Run { get; set; }
        public List<string> RunArgs { get; set; }
        public IEnumerable<string> UnprocessedCommandArgs { get; set; }
    }
}