(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"

(**
F# Project Scaffold
===================

Documentation

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The F# DataFrame library can be <a href="https://nuget.org/packages/FSharp.ProjectTemplate">installed from NuGet</a>:
      <pre>PM> Install-Package FSharp.ProjectTemplate</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

Example
-------

Assume we loaded [Titanic data set](http://www.kaggle.com/c/titanic-gettingStarted) 
into a data frame called `titanic` (the data frame has numerous columns including string 
`Sex` and Boolean `Survived`). Now we can calculate the survival rates for males and females:

*)
#r "FSharp.ProjectTemplate.dll"
open FSharp.ProjectTemplate

Library.hello 0
(**
Some more info

Samples & documentation
-----------------------

The library comes with comprehensible documentation. The tutorials and articles are 
automatically generated from `*.fsx` files in [the samples folder][samples]. The API
reference is automatically generated from Markdown comments in the library implementation.

 * [Stuff](stuff.html) has more stuff

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules
   and functions in the library. This includes additional brief samples on using most of the
   functions.
 
Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding new public API, please also 
consider adding [samples][samples] that can be turned into a documentation. You might
also want to read [library design notes](design.html) to understand how it works.

The library is available under **INSERT** license, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

  [samples]: https://github.com/fsharp/FSharp.ProjectScaffold/tree/master/samples
  [gh]: https://github.com/fsharp/FSharp.ProjectScaffold
  [issues]: https://github.com/fsharp/FSharp.ProjectScaffold/issues
  [readme]: https://github.com/fsharp/FSharp.ProjectScaffold/blob/master/README.md
  [license]: https://github.com/fsharp/FSharp.ProjectScaffold/blob/master/LICENSE.md
*)