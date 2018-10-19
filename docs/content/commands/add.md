## Adding to a project

By default packages are only added to the solution directory, but not on any of
its projects. It's possible to add the package to a specific project:

```sh
paket add <package ID> --project <project>
```

See also [`paket remove`](paket-remove.html).

## Example

Consider the following [`paket.dependencies` file](dependencies-file.html):

```paket
source https:/nuget.org/api/v2

nuget FAKE
```

Now we run `paket add NUnit --version '~> 2.6' --interactive` to install the
package:

```sh
$ paket add NUnit --version '~> 2.6' --interactive
Paket version 5.0.0
Adding NUnit ~> 2.6 to ~/Example/paket.dependencies into group Main
Resolving packages for group Main:
 - NUnit 2.6.4
 - FAKE 4.61.3
Locked version resolution written to ~/Example/paket.lock
Dependencies files saved to ~/Example/paket.dependencies
  Install to ~/Example/src/Foo/Foo.fsproj into group Main?
    [Y]es/[N]o => y

Adding package NUnit to ~/Example/src/Foo/paket.references into group Main
References file saved to ~/Example/src/Foo/paket.references
  Install to ~/Example/src/Bar/Bar.fsproj into group Main?
    [Y]es/[N]o => n

Performance:
 - Resolver: 12 seconds (1 runs)
    - Runtime: 214 milliseconds
    - Blocked (retrieving package details): 86 milliseconds (4 times)
    - Blocked (retrieving package versions): 3 seconds (4 times)
    - Not Blocked (retrieving package versions): 6 times
    - Not Blocked (retrieving package details): 2 times
 - Disk IO: 786 milliseconds
 - Average Request Time: 1 second
 - Number of Requests: 12
 - Runtime: 14 seconds
```

This will add the package to the selected
[`paket.references` files](references-files.html) and also to the
[`paket.dependencies` file](dependencies-file.html). Note that the version
constraint specified the in the above command was preserved.

```paket
source https:/nuget.org/api/v2

nuget FAKE
nuget NUnit ~> 2.6
```
