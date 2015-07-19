## Creating NuGet-Packages

Consider the following [`paket.dependencies` file][depfile] in your project's root:

    source https://nuget.org/api/v2

    nuget Castle.Windsor ~> 3.2
    nuget NUnit

And one of your projects having a [`paket.references` file][reffile] like this:

    Castle.Windsor

Now, when you run `paket install`, your [`paket.lock` file][lockfile] would look like this:

    NUGET
      remote: https://nuget.org/api/v2
      specs:
        Castle.Core (3.3.3)
        Castle.Windsor (3.3.0)
          Castle.Core (>= 3.3.0)
        NUnit (2.6.4)

Now, when you are done programming and wish to create a NuGet-Package of your project, create a [`paket.template`][templatefile] file with `type project` and run:

    [lang=batch]
    paket pack output nugets version 1.0.0

Or, you could run:

    [lang=batch]
    paket pack output nugets version 1.0.0 lock-dependencies

Depending on which command you issue, Paket creates different version requirements of the packages you depend on in the resulting `.nuspec` file of your package:

<table>
  <thead>
    <th>Dependency</th>
    <th>Default</th>
    <th>With locked dependencies</th>
  </thead>
  <tbody>
    <tr>
      <td>Castle.Windsor</td>
      <td><code>[3.2,4.0)</code></td>
      <td><code>[3.3.0]</code></td>
    </tr>
  </tbody> 
</table>

As you see here, the first command (without the `lock-dependencies` parameter) creates version requirements as specified in your [`paket.dependencies` file][depfile]. The second command takes the currently resolved version from your [`paket.lock` file][lockfile] and "locks" it to this specific version.

  [lockfile]: lock-file.html
  [depfile]: dependencies-file.html
  [reffile]: references-files.html
  [templatefile]: template-files.html
