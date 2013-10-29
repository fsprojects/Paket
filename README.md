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
			<td>Folder</td>
			<td>Descritpion</td>
		</tr>
	</thead>
	<tbody>
		<tr>
			<td><a href="/tree/master/bin">bin</a></td>
			<td><p>This directory is the primary output directory for libraries and NuGet packages when using the build system 
(i.e. <code>build.cmd</code> or <code>build.fsx</code>). It is also the target directory when building in <em>Release</em> mode inside Visual Studio.
This directory is touched by many parts of the build process.</p>
<p><strong>It is strongly recommended that nothing be put into this directory.</strong></p>
<p>It is <strong>strongly advised</strong> that the <strong>contents of this directory not be committed</strong> to source control 
(with the sole exception being this <code>README.md</code> file).</p></td>
		</tr>
	</tbody>
</table>

<table>
	<caption>Summary of important solution files</caption>
	<thead>
		<tr>
			<td>Path</td>
			<td>File</td>
			<td>Descritpion</td>
		</tr>
	</thead>
	<tbody>
		<tr>
			<td></td>
			<td></td>
			<td></td>
		</tr>
	</tbody>
</table>

---

[Sample API documents available here](http://pblasucci.github.io/FSharp.ProjectScaffold)
