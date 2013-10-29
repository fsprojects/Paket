FSharp.ProjectScaffold
=======================

A prototypical F# library (file system layout and tooling), recommended by the F# Foundation.

This sample demonstrates the suggested structure of a typical F# solution.
_(NOTE: this layout should NOT be used when authoring a Type Provider. 
For more details about doing so, please [read this.](../../wiki/Suggestions-for-Building-a-Type-Provider))_ 
It also showcase a few popular F#-centric libraries (e.g. FSharp.Formatting), 
as well as using some more general libraries (e.g. NUnit) from within F#.

---

<a id="SolutionFoldersTable"/>
<table>
  <caption>Summary of solution folders</caption>
  <thead>
    <tr>
      <th>Folder</th>
      <th>Descritpion</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td><a href="../../tree/master/.nuget">.nuget</a></td>
      <td>
        <p>This directory, and the files it contains, are used to bootstrap the build process. 
        Specifically, it enables the acquistion of NuGet dependencies on a clean build.
        It is further used by the build process when performing other NuGet-related tasks. 
        These tools also facilitate the <em>Package Restore</em> functionality inside of Visual Studio.</p>
        <p><strong>It is strongly recommended that nothing be put into this directory.</strong></p>
        <p>It is <strong>strongly advised</strong> that the <strong>contents of this directory be committed</strong> to source control.</p>
      </td>
    </tr>
    <tr>
      <td><a href="../../tree/master/bin">bin</a></td>
      <td>
        <p>This directory is the primary output directory for libraries and NuGet packages when using the build system 
        (i.e. <code>build.cmd</code> or <code>build.fsx</code>). It is also the target directory when building in <em>Release</em> mode inside Visual Studio.
        This directory is touched by many parts of the build process.</p>
        <p><strong>It is strongly recommended that nothing be put into this directory.</strong></p>
        <p>It is <strong>strongly advised</strong> that the <strong>contents of this directory NOT be committed</strong> to source control.</p>
      </td>
    </tr>
    <tr>
      <td><a href="../../tree/master/docs/content">docs/content</a></td>
      <td></td>
    </tr>
    <tr>
      <td><a href="../../tree/master/docs/files">docs/files</a></td>
      <td></td>
    </tr>
    <tr>
      <td><a href="../../tree/master/docs/output">docs/output</a></td>
      <td></td>
    </tr>
    <tr>
      <td><a href="../../tree/master/docs/tools">docs/tools</a></td>
      <td></td>
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
        <p>You should use this directory to store any artifacts required to produce a NuGet package for your project.
        This typically includes a <code>.nuspec</code> file and some <code>.ps1</code> scripts, for instance.
        Additionally, this example project includes a <code>.cmd</code> file suitable for manual deployment of packages to <a href="http://nuget.org" target="_blank">http://nuget.org</a>.</p>
      </td>
    </tr>
    <tr>
      <td><a href="../../tree/master/packages">packages</a></td>
      <td>
        <p>Any NuGet packages on which your project depends will be downloaded to this directory.
        Additionally, packages required by the build process will be stored here.</p>
        <p>It is <strong>strongly advised</strong> that the <strong>contents of this directory NOT be committed<strong> to source control.</p>
      </td>
    </tr>
    <tr>
    <tr>
      <td><a href="../../tree/master/src">.nuget</a></td>
      <td>
        <p>Use this directory to house the actual codebase (e.g. one, or more, Visual Studio F# projects) in your solution. 
        A good way to get started is to rename the project included in this sample (FSharp.ProjectTemplate). 
        Alternately, delete the sample project and create your own.</p>
        <p><em>NOTE: When you rename the sample project, or add aditional projects to this directory, you may need to edit <code>build.fsx</code> and/or <code>generate.fsx</code>. 
        You will, likely, also need to update your <code>.sln</code> file(s).
        Please see the following <a href="#SolutionFilesTable">table</a> for more details.</em></p>
        <p><em>NOTE: you should NOT place testing porjects in this path. Testing files belong in the <code>tests</code> directory.</em></p>
      </td>
    </tr>
    <tr>
      <td><a href="../../tree/master/temp">temp</a></td>
      <td>
        <p>This directory is used by the build process as a "scratch", or working, area.</p>
        <p><strong>It is strongly recommended that nothing be put into this directory.</strong></p>
        <p>It is <strong>strongly advised</strong> that the <strong>contents of this directory NOT be committed</strong> to source control.</p>
      </td>
    </tr>
    <tr>
      <td><a href="../../tree/master/tests">tests</a></td>
      <td>
        <p>Use this directory to house any testing projects you might develop (i.e. libraries leveraging NUnit, xUnit, MBUnit, et cetera).
        The sample project included in this directory is configured to use NUnit. Further, <code>build.fsx</code> is coded to execute these test as part of the build process.</p>
        <p><em>NOTE: When you rename the sample project, or add aditional projects to this directory, you may need to edit <code>build.fsx</code> and/or <code>generate.fsx</code>.
        You will, likely, also need to update your <code>.sln</code> file(s).
        Please see the following <a href="#SolutionFilesTable">table</a> for more details.</em></p>
      </td>
    </tr>
  </tbody>
</table>

<a id="SolutionFilesTable"/>
<table>
  <caption>Summary of important solution files</caption>
  <thead>
    <tr>
      <th>Path</th>
      <th>Descritpion</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td><a href="build.cmd">build.cmd</a></td>
      <td></td>
    </tr>
    <tr>
      <td><a href="build.fsx">build.fsx</a></td>
      <td></td>
    </tr>
    <tr>
      <td><a href="FSharp.ProjectScaffold.sln">FSharp.ProjectScaffold.sln</a></td>
      <td></td>
    </tr>
    <tr>
      <td><a href="LICENSE.txt">LICENSE.txt</a></td>
      <td></td>
    </tr>
    <tr>
      <td><a href="RELEASE_NOTES.md">RELEASE_NOTES.md</a></td>
      <td></td>
    </tr>
    <tr>
      <td><a href="README.md">README.md</a></td>
      <td></td>
    </tr>
    <tr>
      <td><a href="docs/content/index.fsx">docs/content/index.fsx</a></td>
      <td></td>
    </tr>
    <tr>
      <td><a href="docs/content/tutorial.fsx">docs/content/tutorial.fsx</a></td>
      <td></td>
    </tr>
    <tr>
      <td><a href="docs/tools/generate.fsx">docs/tools/generate.fsx</a></td>
      <td></td>
    </tr>
    <tr>
      <td><a href="docs/tools/templates/template.cshtml">docs/tools/templates/template.cshtml</a></td>
      <td></td>
    </tr>
  </tbody>
</table>

---

[Sample API documents available here](http://pblasucci.github.io/FSharp.ProjectScaffold)
