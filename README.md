F# ProjectScaffold
=======================

A prototypical F# library (file system layout and tooling), recommended by the F# Software Foundation.

This scaffolding can be used to generate the suggested structure of a typical F# library.
_(NOTE: this layout needs adjusting when authoring a Type Provider. In that case you should use a separate Tests solution)_ 

### Technologies 

| Area                      |  Technologies                             |
|:--------------------------|:------------------------------------------|
| Platforms                 | Linux, Windows, OSX                       |
| Build Automation          | [FAKE](http://fsharp.github.io/FAKE/)     |
| Unit Testing              | [NUnit](http://www.nunit.org/)            |
| Package Formats           | Nuget packages                            |
| Documentation Authoring   | Markdown, HTML and F# Literate Scripts    |
| Source Code Linking       | SourceLink                                |
| Continuous Build and Test | [Travis](http://travis-ci.org) (Linux/OSX) and [AppVeyor](http://appveyor.com) (Windows) |
| Default Package Hosting   | [nuget.org](http://nuget.org)             |
| Default Documentation Hosting  | [GitHub Pages](https://help.github.com/articles/what-are-github-pages)   |

### Initializing

Run 

    ./build.sh
or

    ./build.cmd

You will be prompted for the name of your project and some other details. The project structure will then be generated from templates.

### Build and Release

After initialization, you can 

- Open, edit, build and test using ``ProjectName.sln``
- Build and test release binaries using ``build.cmd`` or ``build.sh `` 
- Build and test release packages using ``build.cmd Release`` or ``build.sh Release`` 
- Build and publish release docs using ``build.cmd ReleaseDocs`` or ``build.sh ReleaseDocs`` 
- Publish packages using ``nuget/publish.cmd`` 

### Structure

<table>
  <caption>Summary of solution folders</caption>
  <thead>
    <tr>
      <th>Folder</th>
      <th>Description</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td><a href="../../tree/master/.nuget">.nuget</a></td>
      <td>
        <p>These files are used to get NuGet dependencies on a clean build and for other NuGet-related tasks including
        Visual Studio <em>Package Restore<em>.</p>
        <p>Do not put anything else in this directory.</p>
        <p>Do commit the contents of this directory to source control.</p>
      </td>
    </tr>
    <tr>
      <td>bin</td>
      <td>
        <p>The primary output directory for libraries and NuGet packages when using the build system 
        (i.e. <code>build.cmd</code> and <code>build.sh</code>). It is also the target directory when building in Visual Studio or Xamarin Studio
        (<em>Note: this has to be manually configured on a per-project basis, as has been done with the example project</em>).</p>
        <p>Do not put anything else in this directory.</p>
        <p>Do not commit the contents of this directory to source control.</p>
      </td>
    </tr>
    <tr>
      <td><a href="../../tree/master/docs/content">docs/content</a></td>
      <td>
        <p>Use this directory for all your literate documentation source files.
        These should be either F# scripts (ending in <code>.fsx</code>) or Markdown files (ending in <code>.md</code>).
        This project includes two sample documentation scripts. Feel free to extend and/or replace these files.
        For more information on generating documentation, please see <a href="http://tpetricek.github.io/FSharp.Formatting/" target="_blank">FSharp.Formatting</a>.</p>
      </td>
    </tr>
    <tr>
      <td><a href="../../tree/master/docs/files">docs/files</a></td>
      <td>
        <p>Contains supporting assets needed for documentation generation. 
        For instance, image files which are to be linked/embedded in the final documentation.</p>
      </td>
    </tr>
    <tr>
      <td><a href="../../tree/master/docs/output">docs/output</a></td>
      <td>
        <p>Contains the final artifacts for both narrative and API documentation. 
        This folder will be automatically created by the documenation generation process.</p>
        <p>Do not put anything else in this directory.</p>
        <p>Do not commit this directory to source control.</p>
      </td>
    </tr>
    <tr>
      <td><a href="../../tree/master/docs/tools">docs/tools</a></td>
      <td>
        <p>Contains tools used in the generation of both narrative documentation and API documentation.
        Edit <code>generate.fsx</code> to include the appropriate repository information
        (see the following table for more details).</p>
      </td>
    </tr>
    <tr>
      <td><a href="../../tree/master/docs/tools/templates">docs/tools/templates</a></td>
      <td>
        <p>Contains the (default) Razor template used as part of generating documentation. 
        You are encouraged to edit this template. You may also create additional templates, 
        but that will require making edits to <code>generate.fsx</code>.</p>
      </td>
    </tr>
    <tr>
      <td><a href="../../tree/master/lib">lib</a></td>
      <td>
        <p>Any <strong>libraries</strong> on which your project depends and which are <strong>NOT managed via NuGet</strong> should be kept <strong>in this directory</strong>.
        This typically includes custom builds of third-party software, private (i.e. to a company) codebases, and native libraries.</p>
      </td>
    </tr>
    <tr>
      <td><a href="../../tree/master/nuget">nuget</a></td>
      <td>
        <p>Stores the NuGet package specifications for your project, typically a <code>.nuspec</code> file.
        Also includes a <code>publish.cmd</code> file for manual deployment of packages to <a href="http://nuget.org" target="_blank">http://nuget.org</a>.</p>
      </td>
    </tr>
    <tr>
      <td>packages</td>
      <td>
        <p>Contains any NuGet packages on which your project depends will be downloaded to this directory.</p>
        <p>Do not put anything else in this directory.</p>
        <p>Do not commit the contents of this directory to source control.</p>
      </td>
    </tr>
    <tr>
    <tr>
      <td><a href="../../tree/master/src">src</a></td>
      <td>
        <p>The actual codebase (e.g. one, or more, F# projects) in your solution. 
        The project files are automatically renamed from (FSharp.ProjectTemplate) when initializing the project.</p>
        <p><em>NOTE: When you add projects to this directory, you will need to edit <code>build.fsx</code> and/or <code>generate.fsx</code>. 
        You will also need to update your <code>.sln</code> file(s).</em></p>
        <p><em>NOTE: Do not place testing projects in this path. Testing files belong in the <code>tests</code> directory.</em></p>
      </td>
    </tr>
    <tr>
      <td>temp</td>
      <td>
        <p>This directory is used by the build process as a "scratch", or working, area.</p>
        <p>Do not put anything else in this directory.</p>
        <p>Do not commit the contents of this directory to source control.</p>
      </td>
    </tr>
    <tr>
      <td><a href="../../tree/master/tests">tests</a></td>
      <td>
        <p>Contains any testing projects you might develop (i.e. libraries leveraging NUnit, xUnit, MBUnit, et cetera).
        The sample project included in this directory is configured to use NUnit. Further, <code>build.fsx</code> is coded to execute these test as part of the build process.</p>
        <p><em>NOTE: When you add aditional projects to this directory, you may need to edit <code>build.fsx</code> and/or <code>generate.fsx</code>.
        You will, likely, also need to update your <code>.sln</code> file(s).</em></p>
      </td>
    </tr>
  </tbody>
</table>

### Files

<table>
  <caption>Summary of important solution files</caption>
  <thead>
    <tr>
      <th>Path</th>
      <th>Description</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td><a href=".travis.yml">.travis.yml</a></td>
      <td><p>Specifies an automated continuous integration (CI) build-and-test on OSX using Travis. Enable your CI build at 
      <a href="http://travis-ci.org">travis-ci.org</a> by logging on there with your GitHub account and 
      activating the project.  If you enable this, then every pull request, commit, branch and tag 
      of your project on GitHub will be built automatically. Builds of open source projects are free.  The default
      build is on Mac OSX (you can also specify Linux by changing the default language).  </p></td>
    </tr>
    <tr>
      <td><a href=".travis.yml">appveyor.yml</a></td>
      <td><p>A specification of an automated continuous integration (CI) build-and-test on Windows using AppVeyor. 
      Enable your CI build at <a href="http://appveyor.com">appveyor.com</a>.  </p></td>
    </tr>
    <tr>
      <td><a href="build.cmd">build.cmd</a> and <a href="build.sh">build.sh</a> </td>
      <td>
        <p>A simple command script which allows the build to be started (i.e. calls <a href="build.fsx">build.fsx</a>) from the command prompt or the file system explorer.
        It also fetches the latest version of <a href="http://fsharp.github.io/FAKE/" target="_blank">F# Make</a>, if it's not detected in <a href="../../tree/master/packages">packages</a>.</p>
      </td>
    </tr>
    <tr>
      <td><a href="build.fsx">build.fsx</a></td>
      <td>
        <p>This <em>very important</em> file runs the build process. 
        It uses the <a href="http://fsharp.github.io/FAKE/" target="_blank">F# Make</a> library to manage many aspects of maintaining a solution.
        It contains a number of common tasks (i.e. build targets) such as directory cleaning, unit test execution, and NuGet package assembly.
        You are encouraged to adapt existing build targets and/or add new ones as necessary. However, if you are leveraging the default conventions,
        as setup in this scaffold project, you can start by simply supplying some values at the top of this file.
        They are as follows:</p>
        <dl>
          <dt><code>project</code></dt>
          <dd>The name of your project, which is used in serveral places: in the generation of AssemblyInfo, 
          as the name of a NuGet package, and for locating a directory under <a href="../../tree/master/src">src</a>.</dd>
          <dt><code>summary</code></dt>
          <dd>A short summary of your project, used as the description in AssemblyInfo. 
          It also provides a short summary for your NuGet package.</dd>
          <dt><code>description</code></dt>
          <dd>A longer description of the project used as a description for your NuGet package 
          <em>(Note: line breaks are automatically cleaned up)</em>.</dd>
          <dt><code>authors</code></dt>
          <dd>A list of authors' names, as should be displayed in the NuGet package metadata.</dd>
          <dt><code>tags</code></dt>
          <dd>A string containing space-separated tags, as should be included in the NuGet package metadata.</dd>
          <dt><code>solutionFile</code></dt>
          <dd>The name of your solution file (sans-extension). It is used as part of the build process.</dd>
          <dt><code>testAssemblies</code></dt>
          <dd>A list of <a href="http://fsharp.github.io/FAKE/" target="_blank">F# Make</a> globbing patterns to be searched for unit-test assemblies.</dd>
          <dt><code>gitHome</code></dt>
          <dd>The URL of user profile hosting this project's GitHub repository. This is used for publishing documentation.</dd>
          <dt><code>gitName</code></dt>
          <dd>The name of this project's GitHub repository. This is used for publishing documentation.</dd>
        </dl>
        <p><code>TODO: document list of included build targets.</code></p>
      </td>
    </tr>
    <tr>
      <td><a href="FSharp.ProjectScaffold.sln">FSharp.ProjectScaffold.sln</a></td>
      <td>
        <p>This is a standard Visual Studio solution file. Use it to collect you projects, including tests. 
        Additionally, this example solution includes many of the important non-project files.
        It is compatible with Visual Studio 2012 and Visual Studio 2013.</p></td>
    </tr>
    <tr>
      <td><a href="LICENSE.txt">LICENSE.txt</a></td>
      <td><p>This file contains all the relevant legal-ese for your project.</p></td>
    </tr>
    <tr>
      <td><a href="RELEASE_NOTES.md">RELEASE_NOTES.md</a></td>
      <td>
        <p>This file details verion-by-version changes in your code.
        It is used for documentation and to populate nuget package details.
        It uses a proper subset of Markdown, with a few simple conventions.
        More details of this format may be found 
        in the documenation for <a href="http://fsharp.github.io/FAKE/apidocs/fake-releasenoteshelper.html" target="_blank">F# Make's ReleaseNotesHelper</a>.</p>
      </td>
    </tr>
    <tr>
      <td><a href="README.md">README.md</a></td>
      <td><p>Use this file to provide an overview of your repository.</p></td>
    </tr>
    <tr>
      <td><a href="docs/content/index.fsx">docs/content/index.fsx</a></td>
      <td><p>Use this file to provide a narrative overview of your project.
      You can write actual, executable F# code in this file. Additionally,
      you may use Markdown comments. As part of the build process, this file
      (along with any other <code>*.fsx</code> or <code>*.md</code> files in the <a href="../../tree/master/docs/content">docs/content</a> directory) will be
      processed into HTML documentation. There is also a build target to deploy
      the generated documentation to a GitHub pages branch (assuming 
      one has been setup in your repository).</p> 
      <p>For further details about documentation generation, 
      please see the <a href="http://tpetricek.github.io/FSharp.Formatting/" target="_blank">FSharp.Formatting library</a>.</p></td>
    </tr>
    <tr>
      <td><a href="docs/content/tutorial.fsx">docs/content/tutorial.fsx</a></td>
      <td><p>This file follows the format of <a href="docs/content/index.fsx">docs/content/index.fsx</a>.
      It's mainly included to demonstrate that narrative documenation is not limited to a single file,
      and documentation files maybe hyperlinked to one another.</p></td>
    </tr>
    <tr>
      <td><a href="docs/tools/generate.fsx">docs/tools/generate.fsx</a></td>
      <td>
        <p>This file controls the generation of narrative and API documentation.
        In most projects, you'll simply need to edit some values located at the top of the file.
        They are as follows:</p>
        <dl>
          <dt><code>referenceBinaries</code></dt>
          <dd>A list of the binaries for which documentation should be cretaed.
          The files listed should each have a corresponding XMLDoc file, and reside in the <a href="../../tree/master/bin">bin</a> folder (as handled by the build process).</dd>
          <dt><code>website</code></dt>
          <dd>The root URL to which generated documenation should be uploaded. 
          In the included example, this points to the GitHub Pages root for this project.</dd>
          <dt><code>info</code></dt>
          <dd><p>A list of key/value pairs which further describe the details of your project.
          This list is exposed to <a href="docs/tools/templates/template.cshtml">template.cshtml</a> for data-binding purposes.
          You may include any information deemed necessary.</p>
          <p><em>Note: the pairs defined in the included example are 
          being used by the sample template.</em></p></dd>
        </dl>
      </td>
    </tr>
    <tr>
      <td><a href="docs/tools/templates/template.cshtml">docs/tools/templates/template.cshtml</a></td>
      <td>
        <p>This file provides the basic HTML layout for generated documentation.
        It uses the C# variant of the Razor templating engine, and leverages jQuery and Bootstrap.
        Change this file to alter the non-content portions of your documentation.</p>
        <p><em>Note: Much of the data passed to this template (i.e. items preceeded with '@') 
        is configured in <a href="docs/tools/generate.fsx">generate.fsx</a></em></p>
      </td>
    </tr>
  </tbody>
</table>

---

<a href="http://fsprojects.github.io/ProjectScaffold" target="_blank">Sample API documents available here.</a>
