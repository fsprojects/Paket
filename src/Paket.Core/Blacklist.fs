namespace Paket

type Blacklist = 
    { TargetsFiles : string list
      LibraryFiles : string list }

    // default set of blacklisted references we don't want to install
    static member TheBlacklist = 
        { TargetsFiles = 
            [
                "Microsoft.Bcl.Build.targets" // would install a targets file causing the build to fail in VS if no packages.config files
            ] 
          LibraryFiles = [] }


