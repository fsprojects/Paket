# ProjectScaffold

This project can be used to scaffold a prototypical .NET solution including file system layout and tooling. This includes a build process which: 

* updates all AssemblyInfo files
* compiles the application and runs all test projects
* generates [SourceLinks](https://github.com/ctaggart/SourceLink)
* generates API docs based on XML document tags
* generates [documentation based on Markdown files](writing-docs.html)
* denerates [NuGet](http://www.nuget.org) packages
* and allows a simple [one step release process](release-process.html). 

In order to start the scaffolding process run 

    $ build.cmd // on windows    
    $ build.sh  // on mono
    
Read the [Getting started tutorial](tutorial.html) to learn more.

Documentation: http://fsprojects.github.io/ProjectScaffold
