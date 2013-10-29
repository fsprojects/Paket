FSharp.ProjectScaffold
=======================

A prototypical F# library (file system layout and tooling), recommended by the F# Foundation.

Overview
---

This sample demonstrates the suggested structure of a typical F# solution.
_(NOTE: this layout should NOT be used when authoring a Type Provider. 
For more details about doing so, see [read this.](http://link/needed))_

The general structure (on-disk) is laid out below,
followed by a detailed description. 

-	.nuget/
  *	Nuget.config
  *	Nuget.exe
  *	Nuget.targets
  *	packages.config
-	bin/
  *	[README.md](http://link/needed)
-	docs/
  -	content/
    *	index.fsx
    *	tutorial.fsx
  -	files/
    -	img/
      *	logo.png
  +	output/
  -	tools/
    -	templates/
      *	template.cshtml
    *	generate.fsx
    *	packages.config
-	lib/
  *	[README.md](http://link/needed)
-	nuget/
-	packages/
  *	[README.md](http://link/needed)
-	src/
  +	FSharp.ProjectTemplate/
-	temp/
  *	[README.md](http://link/needed)
-	tests/
  +	FSharp.ProjectTemplate.Tests/
*	build.cmd
*	build.fsx
*	FSharp.ProjectScaffold.sln
*	[LICENSE.txt](http://link/needed)
*	[RELEASE_NOTES.md](http://link/needed)
*	[README.md](http://link/needed)

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
      <td></td>
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
      <td></td>
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
      <td><a href="../../tree/master/tests">.nuget</a></td>
      <td></td>
    </tr>
  </tbody>
</table>

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
      <td>build.fsx</td>
      <td></td>
    </tr>
    <tr>
      <td>FSharp.ProjectScaffold.sln</td>
      <td></td>
    </tr>
    <tr>
      <td>LICENSE.txt</td>
      <td></td>
    </tr>
    <tr>
      <td>RELEASE_NOTES.md</td>
      <td></td>
    </tr>
    <tr>
      <td>README.md</td>
      <td></td>
    </tr>
    <tr>
      <td>/docs/content/index.fsx</td>
      <td></td>
    </tr>
    <tr>
      <td>/docs/content/tutorial.fsx</td>
      <td></td>
    </tr>
    <tr>
      <td>/docs/tools/generate.fsx</td>
      <td></td>
    </tr>
    <tr>
      <td>/docs/tools/templates/template.cshtml</td>
      <td></td>
    </tr>
  </tbody>
</table>

---

[Sample API documents available here](http://pblasucci.github.io/FSharp.ProjectScaffold)
