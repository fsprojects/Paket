# ProjectScaffold

This project can be used to scaffold a prototypical .NET solution including file system layout and tooling. This includes a build process that: 

* updates all AssemblyInfo files
* compiles the application and runs all test projects
* generates [SourceLinks](https://github.com/ctaggart/SourceLink)
* generates API docs based on XML document tags
* generates [documentation based on Markdown files](http://fsprojects.github.io/ProjectScaffold/writing-docs.html)
* generates [NuGet](http://www.nuget.org) packages
* and allows a simple [one step release process](http://fsprojects.github.io/ProjectScaffold/release-process.html).

In order to start the scaffolding process run 

    $ build.cmd // on windows    
    $ build.sh  // on mono
    
Read the [Getting started tutorial](http://fsprojects.github.io/ProjectScaffold/index.html#Getting-started) to learn more.

Documentation: http://fsprojects.github.io/ProjectScaffold
