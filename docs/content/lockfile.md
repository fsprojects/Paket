What is the Lockfile?
=====================

Consider the following [Packages file](packages_file.html):

    source "http://nuget.org/api/v2"

    nuget "Castle.Windsor-log4net" "~> 3.2"
    nuget "Rx-Main" "~> 2.0"

In this case we specify dependencies to `Castle.Windsor-log4net` and `Rx-Main`.
Both packages have dependencies to further NuGet packages. 
The `package.lock` file is a concrete resolution of all direct or indirect dependencies of your application.

    [lang=textfile]
    NUGET
      remote: http://nuget.org/api/v2
      specs:
        Castle.Windsor (2.1)
        Castle.Windsor-log4net (3.3)
        Rx-Core (2.1)
        Rx-Main (2.0)
        log (1.2)
        log4net (1.1)

Further runs of [paket install](packet_install.htm) will not analyze the `packages.fsx` file again.
So if you commit `package.lock` to your version control system, 
it ensures that other developers, as well as your build servers, 
will always use the same packages that you are using now.